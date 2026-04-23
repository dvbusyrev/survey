using MainProject.Application.DTO.Email;

namespace MainProject.Application.Contracts;

public interface IEmailTemplateService
{
    Task<EmailTemplateSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default);
    Task<int> SendAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default);
}
