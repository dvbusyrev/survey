using MainProject.Application.DTO;
using MainProject.Application.DTO.Organization;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IOrganizationRepository
{
    Task<int> CountAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetPageAsync(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationDataResponse>> GetActiveOptionsAsync(CancellationToken cancellationToken = default);
    Task<Organization?> GetByIdAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(OrganizationWriteModel organization, CancellationToken cancellationToken = default);
    Task<int> UpdateAsync(int organizationId, OrganizationWriteModel organization, CancellationToken cancellationToken = default);
    Task<OrganizationArchiveResult> ArchiveIfUnusedAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationSurveyAssignmentRecord>> GetLatestUnansweredAssignmentsAsync(IReadOnlyCollection<int>? organizationIds = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAssignmentEndDatesAsync(IReadOnlyCollection<(int OrganizationId, int SurveyId)> assignments, DateTime dateEnd, CancellationToken cancellationToken = default);
}
