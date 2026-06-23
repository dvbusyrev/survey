using MainProject.Application.DTO.Audit;

namespace MainProject.Application.Contracts;

public interface IAuditLogRepository
{
    int GetEventCount();

    AuditLogReadResult GetPage(
        int currentPage,
        int pageSize,
        string sortBy,
        string sortDirection,
        bool includeDetails);

    AuditLogReadResult GetDetails(long idAudit, string? sourceTable);

    AuditLogReadResult GetAll();

    AuditAnswerContext? GetAnswerContext(int? organizationSurveyId, int? answerId);
}
