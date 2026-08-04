using MainProject.Application.DTO.Email;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace MainProject.Tests.Services;

public sealed class EmailTemplateServiceTests
{
    [Theory]
    [InlineData("smtp-user", "Поле \"Логин SMTP\" обязательно")]
    [InlineData("smtp-password", "Поле \"Пароль SMTP\" обязательно")]
    [InlineData("from-address", "Поле \"Эл. почта отправителя\" обязательно")]
    [InlineData("display-name", "Поле \"Имя отправителя\" обязательно")]
    public async Task SaveSender_RejectsMissingRequiredField(string field, string expectedError)
    {
        var settings = CreateValidSettings();
        switch (field)
        {
            case "smtp-user":
                settings.SmtpUserName = string.Empty;
                break;
            case "smtp-password":
                settings.SmtpPassword = string.Empty;
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
            new EphemeralDataProtectionProvider());

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
