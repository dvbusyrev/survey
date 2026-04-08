using System.Net;
using System.Net.Mail;
using MainProject.Models;
using Microsoft.Extensions.Options;

namespace MainProject.Services.Email;

public sealed class SmtpEmailSender
{
    private readonly SmtpEmailOptions _options;

    public SmtpEmailSender(IOptions<SmtpEmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailTemplateSettings message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("SMTP settings are not configured.");
        }

        if (string.IsNullOrWhiteSpace(message.To))
        {
            throw new InvalidOperationException("Recipient email is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var mail = new MailMessage
        {
            From = CreateFromAddress(),
            Subject = string.IsNullOrWhiteSpace(message.Subject) ? "Без темы" : message.Subject,
            Body = string.IsNullOrWhiteSpace(message.Content) ? "Пустое письмо" : message.Content,
            IsBodyHtml = true
        };

        mail.To.Add(new MailAddress(message.To));

        using var smtp = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            smtp.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await smtp.SendMailAsync(mail, cancellationToken);
    }

    private MailAddress CreateFromAddress()
    {
        return string.IsNullOrWhiteSpace(_options.FromDisplayName)
            ? new MailAddress(_options.FromAddress)
            : new MailAddress(_options.FromAddress, _options.FromDisplayName);
    }
}
