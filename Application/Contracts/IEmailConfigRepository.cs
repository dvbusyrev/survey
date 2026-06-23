using MainProject.Application.DTO.Configuration;

namespace MainProject.Application.Contracts;

public interface IEmailConfigRepository
{
    Task<EmailConfigRecord?> GetAsync(int configId, CancellationToken cancellationToken = default);
    Task SaveAsync(int configId, EmailConfigRecord record, CancellationToken cancellationToken = default);
}
