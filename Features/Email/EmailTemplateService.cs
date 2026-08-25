using Dapper;
using MainProject.Application.DTO.Email;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;
using System.Security.Cryptography;

namespace MainProject.Application.UseCases.Admin;

public sealed class EmailTemplateService
{
    private const int DefaultConfigId = 1;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SmtpEmailSender _emailSender;
    private readonly SmtpPasswordProtector _smtpPasswordProtector;

    public EmailTemplateService(
        IDbConnectionFactory connectionFactory,
        SmtpEmailSender emailSender,
        SmtpPasswordProtector smtpPasswordProtector)
    {
        _connectionFactory = connectionFactory;
        _emailSender = emailSender;
        _smtpPasswordProtector = smtpPasswordProtector;
    }

    public async Task<EmailMessageSettings> GetMessageAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<EmailMessageSettings>(new CommandDefinition(
            """
            SELECT
                recipient_emails AS To,
                subject_text AS Subject,
                body_text AS Content
            FROM public.email_config
            WHERE id_config = @ConfigId
            LIMIT 1;
            """,
            new { ConfigId = DefaultConfigId },
            cancellationToken: cancellationToken)) ?? new EmailMessageSettings();
    }

    public async Task<EmailSenderSettings> GetSenderAsync(CancellationToken cancellationToken = default)
    {
        var stored = await LoadStoredSenderAsync(cancellationToken);

        return stored == null
            ? new EmailSenderSettings()
            : MapStoredSender(stored, includePassword: false);
    }

    public async Task SaveMessageAsync(
        EmailMessageSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidateMessage(settings);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.email_config
                (id_config, recipient_emails, subject_text, body_text)
            VALUES
                (@ConfigId, @To, @Subject, @Content)
            ON CONFLICT (id_config) DO UPDATE
            SET
                recipient_emails = EXCLUDED.recipient_emails,
                subject_text = EXCLUDED.subject_text,
                body_text = EXCLUDED.body_text;
            """,
            new
            {
                ConfigId = DefaultConfigId,
                normalized.To,
                normalized.Subject,
                normalized.Content
            },
            cancellationToken: cancellationToken));
    }

    public async Task SaveSenderAsync(
        EmailSenderSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidateSender(settings, requirePassword: false);
        if (string.IsNullOrWhiteSpace(normalized.SmtpPassword))
        {
            var stored = await LoadStoredSenderAsync(cancellationToken);
            try
            {
                normalized.SmtpPassword = _smtpPasswordProtector.Unprotect(stored?.SmtpPassword);
            }
            catch (CryptographicException)
            {
                throw new EmailTemplateValidationException(
                    ["Не удалось расшифровать сохранённый пароль SMTP. Введите новый пароль."]);
            }
        }

        if (string.IsNullOrWhiteSpace(normalized.SmtpPassword))
        {
            throw new EmailTemplateValidationException(["Введите новый пароль: сохранённый пароль отсутствует."]);
        }

        var protectedPassword = _smtpPasswordProtector.Protect(normalized.SmtpPassword);

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.email_config
            (
                id_config, smtp_host, smtp_port, smtp_enable_ssl,
                smtp_user_name, smtp_password, from_address, from_display_name
            )
            VALUES
            (
                @ConfigId, @SmtpHost, @SmtpPort, @SmtpEnableSsl,
                @SmtpUserName, @SmtpPassword, @FromAddress, @FromDisplayName
            )
            ON CONFLICT (id_config) DO UPDATE
            SET
                smtp_host = EXCLUDED.smtp_host,
                smtp_port = EXCLUDED.smtp_port,
                smtp_enable_ssl = EXCLUDED.smtp_enable_ssl,
                smtp_user_name = EXCLUDED.smtp_user_name,
                smtp_password = EXCLUDED.smtp_password,
                from_address = EXCLUDED.from_address,
                from_display_name = EXCLUDED.from_display_name;
            """,
            new
            {
                ConfigId = DefaultConfigId,
                normalized.SmtpHost,
                normalized.SmtpPort,
                normalized.SmtpEnableSsl,
                normalized.SmtpUserName,
                SmtpPassword = protectedPassword,
                normalized.FromAddress,
                normalized.FromDisplayName
            },
            cancellationToken: cancellationToken));
    }

    public async Task<int> SendAsync(
        EmailMessageSettings settings,
        CancellationToken cancellationToken = default)
    {
        var message = NormalizeAndValidateMessage(settings);
        var storedSender = await LoadStoredSenderAsync(cancellationToken);
        EmailSenderSettings sender;
        try
        {
            sender = NormalizeAndValidateSender(storedSender == null
                ? new EmailSenderSettings()
                : MapStoredSender(storedSender, includePassword: true));
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "Не удалось расшифровать сохранённый пароль SMTP. Сохраните новый пароль в настройках отправителя.",
                exception);
        }

        return await _emailSender.SendAsync(message, sender, cancellationToken);
    }

    private async Task<StoredEmailSenderSettings?> LoadStoredSenderAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<StoredEmailSenderSettings>(new CommandDefinition(
            """
            SELECT
                smtp_host AS SmtpHost,
                smtp_port AS SmtpPort,
                smtp_enable_ssl AS SmtpEnableSsl,
                smtp_user_name AS SmtpUserName,
                smtp_password AS SmtpPassword,
                from_address AS FromAddress,
                from_display_name AS FromDisplayName
            FROM public.email_config
            WHERE id_config = @ConfigId
            LIMIT 1;
            """,
            new { ConfigId = DefaultConfigId },
            cancellationToken: cancellationToken));
    }

    private EmailSenderSettings MapStoredSender(
        StoredEmailSenderSettings stored,
        bool includePassword)
        => new()
        {
            SmtpHost = stored.SmtpHost ?? string.Empty,
            SmtpPort = stored.SmtpPort > 0 ? stored.SmtpPort : 587,
            SmtpEnableSsl = stored.SmtpEnableSsl,
            SmtpUserName = stored.SmtpUserName ?? string.Empty,
            SmtpPassword = includePassword ? _smtpPasswordProtector.Unprotect(stored.SmtpPassword) : string.Empty,
            FromAddress = stored.FromAddress ?? string.Empty,
            FromDisplayName = stored.FromDisplayName ?? string.Empty
        };

    private static EmailMessageSettings NormalizeAndValidateMessage(EmailMessageSettings? settings)
    {
        if (settings == null)
        {
            throw new EmailTemplateValidationException(["Параметры письма не переданы."]);
        }

        var normalized = new EmailMessageSettings
        {
            To = string.Join("; ", EmailAddressParser.Split(settings.To)),
            Subject = (settings.Subject ?? string.Empty).Trim(),
            Content = (settings.Content ?? string.Empty).Trim()
        };

        var errors = new List<string>();
        var recipients = EmailAddressParser.Split(normalized.To);
        if (recipients.Count == 0)
        {
            errors.Add("Укажите хотя бы одну эл. почту получателя.");
        }
        else
        {
            var invalidRecipients = recipients.Where(static email => !EmailAddressParser.IsValid(email)).ToArray();
            if (invalidRecipients.Length > 0)
            {
                errors.Add($"Проверьте эл. почту получателя: {string.Join(", ", invalidRecipients)}.");
            }
        }

        if (string.IsNullOrWhiteSpace(normalized.Subject))
        {
            errors.Add("Введите тему письма.");
        }

        if (string.IsNullOrWhiteSpace(normalized.Content))
        {
            errors.Add("Введите текст письма.");
        }

        ThrowIfInvalid(errors);
        return normalized;
    }

    private static EmailSenderSettings NormalizeAndValidateSender(
        EmailSenderSettings? settings,
        bool requirePassword = true)
    {
        if (settings == null)
        {
            throw new EmailTemplateValidationException(["Настройки отправителя не переданы."]);
        }

        var normalized = new EmailSenderSettings
        {
            SmtpHost = (settings.SmtpHost ?? string.Empty).Trim(),
            SmtpPort = settings.SmtpPort,
            SmtpEnableSsl = settings.SmtpEnableSsl,
            SmtpUserName = (settings.SmtpUserName ?? string.Empty).Trim(),
            SmtpPassword = settings.SmtpPassword ?? string.Empty,
            FromAddress = (settings.FromAddress ?? string.Empty).Trim(),
            FromDisplayName = (settings.FromDisplayName ?? string.Empty).Trim()
        };

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(normalized.SmtpHost))
        {
            errors.Add("Введите SMTP сервер.");
        }

        if (normalized.SmtpPort < 1 || normalized.SmtpPort > 65535)
        {
            errors.Add("Порт SMTP должен быть числом от 1 до 65535.");
        }

        if (string.IsNullOrWhiteSpace(normalized.FromAddress))
        {
            errors.Add("Введите эл. почту отправителя.");
        }
        else if (!EmailAddressParser.IsValid(normalized.FromAddress))
        {
            errors.Add("Проверьте эл. почту отправителя.");
        }

        if (string.IsNullOrWhiteSpace(normalized.SmtpUserName))
        {
            errors.Add("Введите логин SMTP.");
        }

        if (requirePassword && string.IsNullOrWhiteSpace(normalized.SmtpPassword))
        {
            errors.Add("Введите пароль SMTP.");
        }

        if (string.IsNullOrWhiteSpace(normalized.FromDisplayName))
        {
            errors.Add("Введите имя отправителя.");
        }

        ThrowIfInvalid(errors);
        return normalized;
    }

    private static void ThrowIfInvalid(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new EmailTemplateValidationException(errors);
        }
    }
}

internal sealed class StoredEmailSenderSettings
{
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; }
    public bool SmtpEnableSsl { get; init; }
    public string? SmtpUserName { get; init; }
    public string? SmtpPassword { get; init; }
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
}
