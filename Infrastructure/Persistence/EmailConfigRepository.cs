using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Configuration;
using Microsoft.AspNetCore.DataProtection;

namespace MainProject.Infrastructure.Persistence;

public sealed class EmailConfigRepository : IEmailConfigRepository
{
    private const string SmtpPasswordProtectionPurpose = "MainProject.EmailTemplate.SmtpPassword";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDataProtector _passwordProtector;

    public EmailConfigRepository(
        IDbConnectionFactory connectionFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _connectionFactory = connectionFactory;
        _passwordProtector = dataProtectionProvider.CreateProtector(SmtpPasswordProtectionPurpose);
    }

    public async Task<EmailConfigRecord?> GetAsync(int configId, CancellationToken cancellationToken = default)
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
            : new EmailConfigRecord
            {
                To = storedRecord.To,
                Subject = storedRecord.Subject,
                Content = storedRecord.Content,
                SmtpHost = storedRecord.SmtpHost,
                SmtpPort = storedRecord.SmtpPort,
                SmtpEnableSsl = storedRecord.SmtpEnableSsl,
                SmtpUserName = storedRecord.SmtpUserName,
                SmtpPassword = UnprotectPassword(storedRecord.ProtectedSmtpPassword),
                FromAddress = storedRecord.FromAddress,
                FromDisplayName = storedRecord.FromDisplayName
            };
    }

    public async Task SaveAsync(int configId, EmailConfigRecord record, CancellationToken cancellationToken = default)
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
                record.To,
                record.Subject,
                record.Content,
                record.SmtpHost,
                record.SmtpPort,
                record.SmtpEnableSsl,
                record.SmtpUserName,
                ProtectedSmtpPassword = ProtectPassword(record.SmtpPassword),
                record.FromAddress,
                record.FromDisplayName
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

    private sealed class StoredEmailConfigRecord
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
}
