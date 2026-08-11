using MainProject.Application.DTO.Email;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace MainProject.Tests.Services;

public sealed class EmailTemplateServiceTests
{
    [Theory]
    [InlineData("smtp-host", "Введите SMTP сервер.")]
    [InlineData("smtp-user", "Введите логин SMTP.")]
    [InlineData("from-address", "Введите эл. почту отправителя.")]
    [InlineData("display-name", "Введите имя отправителя.")]
    public async Task SaveSender_RejectsMissingRequiredField(string field, string expectedError)
    {
        var settings = CreateValidSettings();
        switch (field)
        {
            case "smtp-host":
                settings.SmtpHost = string.Empty;
                break;
            case "smtp-user":
                settings.SmtpUserName = string.Empty;
                break;
            case "from-address":
                settings.FromAddress = string.Empty;
                break;
            case "display-name":
                settings.FromDisplayName = string.Empty;
                break;
        }

        var service = new EmailTemplateService(
            new UnexpectedConnectionFactory(),
            new SmtpEmailSender(),
            new SmtpPasswordProtector(new EphemeralDataProtectionProvider()));

        var exception = await Assert.ThrowsAsync<EmailTemplateValidationException>(
            () => service.SaveSenderAsync(settings));

        Assert.Contains(expectedError, exception.Errors);
    }

    private static EmailSenderSettings CreateValidSettings()
        => new()
        {
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpEnableSsl = true,
            SmtpUserName = "smtp-user",
            SmtpPassword = "smtp-password",
            FromAddress = "sender@example.test",
            FromDisplayName = "Отправитель"
        };

    private sealed class UnexpectedConnectionFactory : IDbConnectionFactory
    {
        public Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Проверка обязательных полей не должна обращаться к БД.");
    }
}
