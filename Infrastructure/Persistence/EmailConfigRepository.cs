using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Configuration;

namespace MainProject.Infrastructure.Persistence;

public sealed class EmailConfigRepository : IEmailConfigRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EmailConfigRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<EmailConfigRecord?> GetAsync(int configId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<EmailConfigRecord>(new CommandDefinition(
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
            FROM public.email_config
            WHERE id_config = @ConfigId
            LIMIT 1;
            """,
            new { ConfigId = configId },
            cancellationToken: cancellationToken));
    }

    public async Task SaveAsync(int configId, EmailConfigRecord record, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
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
                @SmtpEnableSsl, @SmtpUserName, @SmtpPasswordEncrypted, @FromAddress, @FromDisplayName
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
                record.SmtpPasswordEncrypted,
                record.FromAddress,
                record.FromDisplayName
            },
            cancellationToken: cancellationToken));
    }
}
