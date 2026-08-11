using System.Security.Cryptography;
using Dapper;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;
using Npgsql;

namespace MainProject.Infrastructure.External.Email;

public sealed class SmtpPasswordMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmtpPasswordMigrationHostedService> _logger;

    public SmtpPasswordMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SmtpPasswordMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var passwordProtector = scope.ServiceProvider.GetRequiredService<SmtpPasswordProtector>();
            await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var storedPasswords = (await connection.QueryAsync<StoredSmtpPassword>(new CommandDefinition(
                """
                SELECT id_config AS IdConfig, smtp_password AS Password
                FROM public.email_config
                WHERE smtp_password <> '';
                """,
                cancellationToken: cancellationToken))).ToArray();

            foreach (var storedPassword in storedPasswords)
            {
                if (passwordProtector.IsCurrentFormat(storedPassword.Password))
                {
                    continue;
                }

                string plainTextPassword;
                try
                {
                    plainTextPassword = passwordProtector.Unprotect(storedPassword.Password);
                }
                catch (CryptographicException exception)
                {
                    _logger.LogError(
                        exception,
                        "Не удалось расшифровать SMTP-пароль конфигурации {ConfigId}. Требуется ввести новый пароль.",
                        storedPassword.IdConfig);
                    continue;
                }

                var protectedPassword = passwordProtector.Protect(plainTextPassword);
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE public.email_config
                    SET smtp_password = @ProtectedPassword
                    WHERE id_config = @ConfigId
                      AND smtp_password = @OriginalPassword;
                    """,
                    new
                    {
                        ConfigId = storedPassword.IdConfig,
                        OriginalPassword = storedPassword.Password,
                        ProtectedPassword = protectedPassword
                    },
                    cancellationToken: cancellationToken));
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning(exception, "Таблица email_config ещё не создана, миграция SMTP-пароля пропущена.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось выполнить миграцию SMTP-паролей в защищённый формат.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record StoredSmtpPassword(int IdConfig, string Password);
}
