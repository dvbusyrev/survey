using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Email;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.Persistence;

namespace MainProject.Application.UseCases.Admin;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private const string DefaultTemplateKey = "default";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDataProtector _passwordProtector;
    private readonly SmtpEmailSender _emailSender;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(
        IDbConnectionFactory connectionFactory,
        IDataProtectionProvider dataProtectionProvider,
        SmtpEmailSender emailSender,
        ILogger<EmailTemplateService> logger)
    {
        _connectionFactory = connectionFactory;
        _passwordProtector = dataProtectionProvider.CreateProtector("MainProject.EmailTemplate.SmtpPassword");
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<EmailTemplateSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<EmailTemplateSettingsRow>(
            new CommandDefinition(
                """
                SELECT
                    recipient_emails AS To,
                    subject_text AS Subject,
                    body_text AS Content,
                    smtp_host AS SmtpHost,
                    smtp_port AS SmtpPort,
                    smtp_enable_ssl AS SmtpEnableSsl,
                    smtp_user_name AS SmtpUserName,
                    smtp_password AS SmtpPasswordEncrypted,
                    from_address AS FromAddress,
                    from_display_name AS FromDisplayName
                FROM public.email_template
                WHERE template_key = @templateKey
                LIMIT 1;
                """,
                new { templateKey = DefaultTemplateKey },
                cancellationToken: cancellationToken));

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
            SmtpPassword = DecryptPassword(row.SmtpPasswordEncrypted),
            FromAddress = row.FromAddress ?? string.Empty,
            FromDisplayName = row.FromDisplayName ?? string.Empty
        };
    }

    public async Task SaveAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(settings);
        var encryptedPassword = EncryptPassword(normalized.SmtpPassword);

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.email_template
                (
                    template_key,
                    recipient_emails,
                    subject_text,
                    body_text,
                    smtp_host,
                    smtp_port,
                    smtp_enable_ssl,
                    smtp_user_name,
                    smtp_password,
                    from_address,
                    from_display_name
                )
                VALUES
                (
                    @TemplateKey,
                    @To,
                    @Subject,
                    @Content,
                    @SmtpHost,
                    @SmtpPort,
                    @SmtpEnableSsl,
                    @SmtpUserName,
                    @SmtpPassword,
                    @FromAddress,
                    @FromDisplayName
                )
                ON CONFLICT (template_key) DO UPDATE
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
                    TemplateKey = DefaultTemplateKey,
                    normalized.To,
                    normalized.Subject,
                    normalized.Content,
                    normalized.SmtpHost,
                    normalized.SmtpPort,
                    normalized.SmtpEnableSsl,
                    normalized.SmtpUserName,
                    SmtpPassword = encryptedPassword,
                    normalized.FromAddress,
                    normalized.FromDisplayName
                },
                cancellationToken: cancellationToken));
    }

    public Task<int> SendAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(settings);
        return _emailSender.SendAsync(normalized, cancellationToken);
    }

    private EmailTemplateSettings NormalizeAndValidate(EmailTemplateSettings? settings)
    {
        if (settings == null)
        {
            throw new EmailTemplateValidationException(["Параметры письма не переданы."]);
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
            errors.Add("Поле \"Кому\" должно содержать хотя бы один email.");
        }
        else
        {
            var invalidRecipients = recipients.Where(static email => !EmailAddressParser.IsValid(email)).ToArray();
            if (invalidRecipients.Length > 0)
            {
                errors.Add($"Поле \"Кому\" содержит некорректные email: {string.Join(", ", invalidRecipients)}.");
            }
        }

        if (string.IsNullOrWhiteSpace(normalized.Subject))
        {
            errors.Add("Поле \"Тема\" обязательно.");
        }

        if (string.IsNullOrWhiteSpace(normalized.Content))
        {
            errors.Add("Поле \"Содержание\" обязательно.");
        }

        if (string.IsNullOrWhiteSpace(normalized.SmtpHost))
        {
            errors.Add("Поле \"SMTP сервер\" обязательно.");
        }

        if (normalized.SmtpPort < 1 || normalized.SmtpPort > 65535)
        {
            errors.Add("Поле \"Порт SMTP\" должно быть числом от 1 до 65535.");
        }

        if (!EmailAddressParser.IsValid(normalized.FromAddress))
        {
            errors.Add("Поле \"Email отправителя\" заполнено некорректно.");
        }

        var hasUserName = !string.IsNullOrWhiteSpace(normalized.SmtpUserName);
        var hasPassword = !string.IsNullOrWhiteSpace(normalized.SmtpPassword);
        if (hasUserName != hasPassword)
        {
            errors.Add("Логин SMTP и пароль SMTP должны быть заполнены вместе.");
        }

        if (errors.Count > 0)
        {
            throw new EmailTemplateValidationException(errors);
        }

        return normalized;
    }

    private string EncryptPassword(string password)
    {
        return string.IsNullOrWhiteSpace(password)
            ? string.Empty
            : _passwordProtector.Protect(password);
    }

    private string DecryptPassword(string? encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(encryptedPassword))
        {
            return string.Empty;
        }

        try
        {
            return _passwordProtector.Unprotect(encryptedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось расшифровать сохранённый SMTP пароль.");
            return string.Empty;
        }
    }

    private sealed class EmailTemplateSettingsRow
    {
        public string To { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string SmtpHost { get; init; } = string.Empty;
        public int SmtpPort { get; init; }
        public bool SmtpEnableSsl { get; init; }
        public string SmtpUserName { get; init; } = string.Empty;
        public string SmtpPasswordEncrypted { get; init; } = string.Empty;
        public string FromAddress { get; init; } = string.Empty;
        public string FromDisplayName { get; init; } = string.Empty;
    }
}
