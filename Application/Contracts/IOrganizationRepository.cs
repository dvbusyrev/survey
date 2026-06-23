using MainProject.Application.DTO;
using MainProject.Application.DTO.Organization;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IOrganizationRepository
{
    int Count(bool includeArchived);
    IReadOnlyList<Organization> GetPage(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset);
    IReadOnlyList<Organization> GetAll(bool includeArchived);
    IReadOnlyList<OrganizationDataResponse> GetActiveOptions();
    Organization? GetById(int organizationId);
    int Create(OrganizationWriteModel organization);
    int Update(int organizationId, OrganizationWriteModel organization);
    OrganizationArchiveResult ArchiveIfUnused(int organizationId);
    IReadOnlyList<OrganizationSurveyAssignmentRecord> GetLatestUnansweredAssignments(IReadOnlyCollection<int>? organizationIds = null);
    bool UpdateAssignmentEndDates(IReadOnlyCollection<(int OrganizationId, int SurveyId)> assignments, DateTime dateEnd);
}
