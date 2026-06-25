using MainProject.Application.Contracts;
using MainProject.Application.DTO.Configuration;
using MainProject.Application.DTO.Email;
using MainProject.Infrastructure.External.Email;

namespace MainProject.Application.UseCases.Admin;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private const int DefaultConfigId = 1;

    private readonly IEmailConfigRepository _emailConfigRepository;
    private readonly SmtpEmailSender _emailSender;

    public EmailTemplateService(
        IEmailConfigRepository emailConfigRepository,
        SmtpEmailSender emailSender)
    {
        _emailConfigRepository = emailConfigRepository;
        _emailSender = emailSender;
    }

    public async Task<EmailTemplateSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _emailConfigRepository.GetAsync(DefaultConfigId, cancellationToken);

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
        await _emailConfigRepository.SaveAsync(
            DefaultConfigId,
            new EmailConfigRecord
            {
                To = normalized.To,
                Subject = normalized.Subject,
                Content = normalized.Content,
                SmtpHost = normalized.SmtpHost,
                SmtpPort = normalized.SmtpPort,
                SmtpEnableSsl = normalized.SmtpEnableSsl,
                SmtpUserName = normalized.SmtpUserName,
                SmtpPassword = normalized.SmtpPassword,
                FromAddress = normalized.FromAddress,
                FromDisplayName = normalized.FromDisplayName
            },
            cancellationToken);
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
