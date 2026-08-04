using Dapper;
using MainProject.Application.DTO.Email;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;

namespace MainProject.Application.UseCases.Admin;

public sealed class EmailTemplateService
{
    private const int DefaultConfigId = 1;
    private const string SmtpPasswordProtectionPurpose = "MainProject.EmailTemplate.SmtpPassword";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SmtpEmailSender _emailSender;
    private readonly IDataProtector _passwordProtector;

    public EmailTemplateService(
        IDbConnectionFactory connectionFactory,
        SmtpEmailSender emailSender,
        IDataProtectionProvider dataProtectionProvider)
    {
        _connectionFactory = connectionFactory;
        _emailSender = emailSender;
        _passwordProtector = dataProtectionProvider.CreateProtector(SmtpPasswordProtectionPurpose);
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
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var stored = await connection.QueryFirstOrDefaultAsync<StoredEmailSenderSettings>(new CommandDefinition(
            """
            SELECT
                smtp_host AS SmtpHost,
                smtp_port AS SmtpPort,
                smtp_enable_ssl AS SmtpEnableSsl,
                smtp_user_name AS SmtpUserName,
                smtp_password AS ProtectedSmtpPassword,
                from_address AS FromAddress,
                from_display_name AS FromDisplayName
            FROM public.email_config
            WHERE id_config = @ConfigId
            LIMIT 1;
            """,
            new { ConfigId = DefaultConfigId },
            cancellationToken: cancellationToken));

        return stored == null
            ? new EmailSenderSettings()
            : new EmailSenderSettings
            {
                SmtpHost = stored.SmtpHost ?? string.Empty,
                SmtpPort = stored.SmtpPort > 0 ? stored.SmtpPort : 587,
                SmtpEnableSsl = stored.SmtpEnableSsl,
                SmtpUserName = stored.SmtpUserName ?? string.Empty,
                SmtpPassword = UnprotectPassword(stored.ProtectedSmtpPassword),
                FromAddress = stored.FromAddress ?? string.Empty,
                FromDisplayName = stored.FromDisplayName ?? string.Empty
            };
    }

    public async Task SaveMessageAsync(
        EmailMessageSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidateMessage(settings);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.email_config (id_config, recipient_emails, subject_text, body_text)
            VALUES (@ConfigId, @To, @Subject, @Content)
            ON CONFLICT (id_config) DO UPDATE
            SET
                recipient_emails = EXCLUDED.recipient_emails,
                subject_text = EXCLUDED.subject_text,
                body_text = EXCLUDED.body_text;
            """,
            new { ConfigId = DefaultConfigId, normalized.To, normalized.Subject, normalized.Content },
            cancellationToken: cancellationToken));
    }

    public async Task SaveSenderAsync(
        EmailSenderSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidateSender(settings);
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
                @SmtpUserName, @ProtectedSmtpPassword, @FromAddress, @FromDisplayName
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
                ProtectedSmtpPassword = ProtectPassword(normalized.SmtpPassword),
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
        var sender = NormalizeAndValidateSender(await GetSenderAsync(cancellationToken));
        return await _emailSender.SendAsync(message, sender, cancellationToken);
    }

    private string ProtectPassword(string? password)
    {
        return string.IsNullOrWhiteSpace(password)
            ? string.Empty
            : _passwordProtector.Protect(password);
    }

    private string UnprotectPassword(string? protectedPassword)
    {
        return string.IsNullOrWhiteSpace(protectedPassword)
            ? string.Empty
            : _passwordProtector.Unprotect(protectedPassword);
    }

    private static EmailMessageSettings NormalizeAndValidateMessage(EmailMessageSettings? settings)
    {
        if (settings == null)
        {
            throw new EmailTemplateValidationException(["Параметры письма не переданы"]);
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
            errors.Add("Поле \"Кому\" должно содержать хотя бы одну эл. почту");
        }
        else
        {
            var invalidRecipients = recipients.Where(static email => !EmailAddressParser.IsValid(email)).ToArray();
            if (invalidRecipients.Length > 0)
            {
                errors.Add($"Поле \"Кому\" содержит некорректную эл. почту: {string.Join(", ", invalidRecipients)}");
            }
        }

        if (string.IsNullOrWhiteSpace(normalized.Subject))
        {
            errors.Add("Поле \"Тема\" обязательно");
        }

        if (string.IsNullOrWhiteSpace(normalized.Content))
        {
            errors.Add("Поле \"Содержание\" обязательно");
        }

        ThrowIfInvalid(errors);
        return normalized;
    }

    private static EmailSenderSettings NormalizeAndValidateSender(EmailSenderSettings? settings)
    {
        if (settings == null)
        {
            throw new EmailTemplateValidationException(["Настройки отправителя не переданы"]);
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
            errors.Add("Поле \"SMTP сервер\" обязательно");
        }

        if (normalized.SmtpPort < 1 || normalized.SmtpPort > 65535)
        {
            errors.Add("Поле \"Порт SMTP\" должно быть числом от 1 до 65535");
        }

        if (string.IsNullOrWhiteSpace(normalized.FromAddress))
        {
            errors.Add("Поле \"Эл. почта отправителя\" обязательно");
        }
        else if (!EmailAddressParser.IsValid(normalized.FromAddress))
        {
            errors.Add("Поле \"Эл. почта отправителя\" заполнено некорректно");
        }

        if (string.IsNullOrWhiteSpace(normalized.SmtpUserName))
        {
            errors.Add("Поле \"Логин SMTP\" обязательно");
        }

        if (string.IsNullOrWhiteSpace(normalized.SmtpPassword))
        {
            errors.Add("Поле \"Пароль SMTP\" обязательно");
        }

        if (string.IsNullOrWhiteSpace(normalized.FromDisplayName))
        {
            errors.Add("Поле \"Имя отправителя\" обязательно");
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
    public string? ProtectedSmtpPassword { get; init; }
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
}
