using MainProject.Application.DTO.Audit;

namespace MainProject.Application.Contracts;

public interface IAuditLogRepository
{
    Task<int> GetEventCountAsync(CancellationToken cancellationToken = default);

    Task<AuditLogReadResult> GetPageAsync(
        int currentPage,
        int pageSize,
        string sortBy,
        string sortDirection,
        bool includeDetails,
        CancellationToken cancellationToken = default);

    Task<AuditLogReadResult> GetDetailsAsync(long idAudit, string? sourceTable, CancellationToken cancellationToken = default);

    Task<AuditLogReadResult> GetAllAsync(CancellationToken cancellationToken = default);

    Task<AuditAnswerContext?> GetAnswerContextAsync(
        int? organizationSurveyId,
        int? answerId,
        CancellationToken cancellationToken = default);
}
