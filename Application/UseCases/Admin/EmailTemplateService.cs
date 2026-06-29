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

    public async Task<EmailTemplateSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetEmailConfigAsync(DefaultConfigId, cancellationToken);

        if (row == null)
        {
            return new EmailTemplateSettings();
        }

        return new EmailTemplateSettings
        {
            To = row.To ?? string.Empty,
            Subject = row.Subject ?? string.Empty,
            Content = row.Content ?? string.Empty,
            SmtpHost = row.SmtpHost ?? string.Empty,
            SmtpPort = row.SmtpPort > 0 ? row.SmtpPort : 587,
            SmtpEnableSsl = row.SmtpEnableSsl,
            SmtpUserName = row.SmtpUserName ?? string.Empty,
            SmtpPassword = row.SmtpPassword ?? string.Empty,
            FromAddress = row.FromAddress ?? string.Empty,
            FromDisplayName = row.FromDisplayName ?? string.Empty
        };
    }

    public async Task SaveAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(settings);
        await SaveEmailConfigAsync(DefaultConfigId, normalized, cancellationToken);
    }

    public Task<int> SendAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(settings);
        return _emailSender.SendAsync(normalized, cancellationToken);
    }

    private async Task<EmailTemplateSettings?> GetEmailConfigAsync(int configId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var storedRecord = await connection.QueryFirstOrDefaultAsync<StoredEmailConfigRecord>(new CommandDefinition(
            """
            SELECT
                recipient_emails AS To,
                subject_text AS Subject,
                body_text AS Content,
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
            new { ConfigId = configId },
            cancellationToken: cancellationToken));

        return storedRecord == null
            ? null
            : new EmailTemplateSettings
            {
                To = storedRecord.To ?? string.Empty,
                Subject = storedRecord.Subject ?? string.Empty,
                Content = storedRecord.Content ?? string.Empty,
                SmtpHost = storedRecord.SmtpHost ?? string.Empty,
                SmtpPort = storedRecord.SmtpPort,
                SmtpEnableSsl = storedRecord.SmtpEnableSsl,
                SmtpUserName = storedRecord.SmtpUserName ?? string.Empty,
                SmtpPassword = UnprotectPassword(storedRecord.ProtectedSmtpPassword),
                FromAddress = storedRecord.FromAddress ?? string.Empty,
                FromDisplayName = storedRecord.FromDisplayName ?? string.Empty
            };
    }

    private async Task SaveEmailConfigAsync(
        int configId,
        EmailTemplateSettings settings,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.email_config
            (
                id_config, recipient_emails, subject_text, body_text, smtp_host, smtp_port,
                smtp_enable_ssl, smtp_user_name, smtp_password, from_address, from_display_name
            )
            VALUES
            (
                @ConfigId, @To, @Subject, @Content, @SmtpHost, @SmtpPort,
                @SmtpEnableSsl, @SmtpUserName, @ProtectedSmtpPassword, @FromAddress, @FromDisplayName
            )
            ON CONFLICT (id_config) DO UPDATE
            SET
                recipient_emails = EXCLUDED.recipient_emails,
                subject_text = EXCLUDED.subject_text,
                body_text = EXCLUDED.body_text,
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
                ConfigId = configId,
                settings.To,
                settings.Subject,
                settings.Content,
                settings.SmtpHost,
                settings.SmtpPort,
                settings.SmtpEnableSsl,
                settings.SmtpUserName,
                ProtectedSmtpPassword = ProtectPassword(settings.SmtpPassword),
                settings.FromAddress,
                settings.FromDisplayName
            },
            cancellationToken: cancellationToken));
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

    private EmailTemplateSettings NormalizeAndValidate(EmailTemplateSettings? settings)
    {
        if (settings == null)
        {
            throw new EmailTemplateValidationException(["Параметры письма не переданы"]);
        }

        var normalized = new EmailTemplateSettings
        {
            To = string.Join("; ", EmailAddressParser.Split(settings.To)),
            Subject = (settings.Subject ?? string.Empty).Trim(),
            Content = (settings.Content ?? string.Empty).Trim(),
            SmtpHost = (settings.SmtpHost ?? string.Empty).Trim(),
            SmtpPort = settings.SmtpPort,
            SmtpEnableSsl = settings.SmtpEnableSsl,
            SmtpUserName = (settings.SmtpUserName ?? string.Empty).Trim(),
            SmtpPassword = settings.SmtpPassword ?? string.Empty,
            FromAddress = (settings.FromAddress ?? string.Empty).Trim(),
            FromDisplayName = (settings.FromDisplayName ?? string.Empty).Trim()
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

        if (string.IsNullOrWhiteSpace(normalized.SmtpHost))
        {
            errors.Add("Поле \"SMTP сервер\" обязательно");
        }

        if (normalized.SmtpPort < 1 || normalized.SmtpPort > 65535)
        {
            errors.Add("Поле \"Порт SMTP\" должно быть числом от 1 до 65535");
        }

        if (!EmailAddressParser.IsValid(normalized.FromAddress))
        {
            errors.Add("Поле \"Эл. почта отправителя\" заполнено некорректно");
        }

        var hasUserName = !string.IsNullOrWhiteSpace(normalized.SmtpUserName);
        var hasPassword = !string.IsNullOrWhiteSpace(normalized.SmtpPassword);
        if (hasUserName != hasPassword)
        {
            errors.Add("Логин SMTP и пароль SMTP должны быть заполнены вместе");
        }

        if (errors.Count > 0)
        {
            throw new EmailTemplateValidationException(errors);
        }

        return normalized;
    }

}

internal sealed class StoredEmailConfigRecord
{
    public string? To { get; init; }
    public string? Subject { get; init; }
    public string? Content { get; init; }
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; }
    public bool SmtpEnableSsl { get; init; }
    public string? SmtpUserName { get; init; }
    public string? ProtectedSmtpPassword { get; init; }
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
}
