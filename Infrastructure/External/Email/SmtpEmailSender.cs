using System.Net;
using System.Net.Mail;
using MainProject.Application.DTO.Email;

namespace MainProject.Infrastructure.External.Email;

public sealed class SmtpEmailSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(100);

    public async Task<int> SendAsync(EmailTemplateSettings message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.SmtpHost)
            || string.IsNullOrWhiteSpace(message.FromAddress))
        {
            throw new InvalidOperationException("SMTP настройки заполнены не полностью.");
        }

        var recipients = EmailAddressParser.Split(message.To);
        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Не указан ни один получатель письма.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var mail = new MailMessage
        {
            From = CreateFromAddress(message),
            Subject = string.IsNullOrWhiteSpace(message.Subject) ? "Без темы" : message.Subject,
            Body = string.IsNullOrWhiteSpace(message.Content) ? "Пустое письмо" : message.Content,
            IsBodyHtml = LooksLikeHtml(message.Content)
        };

        foreach (var recipient in recipients)
        {
            mail.To.Add(new MailAddress(recipient));
        }

        using var smtp = new SmtpClient(message.SmtpHost, message.SmtpPort)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = (int)SendTimeout.TotalMilliseconds,
            EnableSsl = message.SmtpEnableSsl
        };

        if (!string.IsNullOrWhiteSpace(message.SmtpUserName))
        {
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(message.SmtpUserName, message.SmtpPassword);
        }
        else
        {
            smtp.UseDefaultCredentials = true;
        }

        try
        {
            await smtp.SendMailAsync(mail, cancellationToken);
            return recipients.Count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SmtpFailedRecipientsException ex)
        {
            var failedRecipients = ex.InnerExceptions
                .Select(static item => item.FailedRecipient)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var details = failedRecipients.Length > 0
                ? $" Не удалось доставить: {string.Join(", ", failedRecipients)}."
                : string.Empty;

            throw new InvalidOperationException($"SMTP сервер отклонил одного или нескольких получателей.{details}", ex);
        }
        catch (SmtpException ex) when (ex.StatusCode == SmtpStatusCode.MustIssueStartTlsFirst)
        {
            throw new InvalidOperationException(
                "SMTP сервер требует защищённое соединение. Проверьте режим SSL/TLS и порт SMTP.",
                ex);
        }
        catch (SmtpException ex) when (ex.StatusCode == SmtpStatusCode.ClientNotPermitted
            || ex.StatusCode == SmtpStatusCode.GeneralFailure)
        {
            throw new InvalidOperationException(
                "Не удалось подключиться к SMTP серверу. Проверьте адрес сервера, порт и доступность сети.",
                ex);
        }
        catch (SmtpException ex) when (ex.StatusCode == SmtpStatusCode.MailboxUnavailable
            || ex.StatusCode == SmtpStatusCode.UserNotLocalTryAlternatePath
            || ex.StatusCode == SmtpStatusCode.MailboxBusy)
        {
            throw new InvalidOperationException(
                "SMTP сервер отклонил получателя письма. Проверьте адреса в поле «Кому».",
                ex);
        }
        catch (SmtpException ex) when (ex.StatusCode == SmtpStatusCode.TransactionFailed)
        {
            throw new InvalidOperationException(
                "SMTP сервер не принял письмо. Проверьте логин, пароль и ограничения почтового сервера.",
                ex);
        }
        catch (SmtpException ex)
        {
            throw new InvalidOperationException(
                $"Ошибка SMTP: {ex.Message}",
                ex);
        }
        catch (TimeoutException ex)
        {
            throw new InvalidOperationException(
                "SMTP сервер не ответил вовремя. Проверьте адрес сервера, порт и режим SSL/TLS.",
                ex);
        }
    }

    private static MailAddress CreateFromAddress(EmailTemplateSettings message)
    {
        return string.IsNullOrWhiteSpace(message.FromDisplayName)
            ? new MailAddress(message.FromAddress)
            : new MailAddress(message.FromAddress, message.FromDisplayName);
    }

    private static bool LooksLikeHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return content.Contains('<') && content.Contains('>');
    }
}
