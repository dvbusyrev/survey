using System.Globalization;
using System.Text;
using MainProject.Application.DTO.Audit;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public partial class AuditLogService
{
    private const string RedactedValue = "[REDACTED]";
    private static readonly StringComparer AuditLogStringComparer = StringComparer.Create(new CultureInfo("ru-RU"), true);

    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "hash_password",
        "password",
        "csp",
        "key_csp",
        "signature",
        "email",
        "recipient_emails",
        "smtp_user_name",
        "smtp_password",
        "from_address"
    };

    private static readonly HashSet<string> IgnoredChangedFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "date_update",
        "user_update"
    };

    private readonly AuditLogRepository _auditLogRepository;

    protected AuditLogService()
    {
        _auditLogRepository = null!;
    }

    public AuditLogService(AuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public virtual Task<AuditLogPageViewModel> GetLogsPageAsync(
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        return GetLogsPageInternalAsync(currentPage, pageSize, sortBy, sortDirection, stripDetails: true, cancellationToken);
    }

    public virtual async Task<Log?> GetLogDetailsAsync(
        long idLog,
        string? sourceTable,
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        if (idLog <= 0)
        {
            return null;
        }

        var result = await _auditLogRepository.GetDetailsAsync(idLog, sourceTable, cancellationToken);
        if (result.Rows.Count == 0)
        {
            return null;
        }

        var logs = MapAuditRows(result.Rows, result.SourceColumnOrders, reconstructPreviousSnapshots: false);
        var log = logs.FirstOrDefault(log => log.IdLog == idLog) ?? logs.FirstOrDefault();
        if (log != null)
        {
            await EnrichAnswerContextAsync(log, cancellationToken);
        }

        return log;
    }

    private async Task<AuditLogPageViewModel> GetLogsPageInternalAsync(
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        bool stripDetails,
        CancellationToken cancellationToken)
    {
        var normalizedPageSize = pageSize > 0 ? pageSize : AppListPaging.DefaultPageSize;
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSortDirection(null, normalizedSortBy);
        var totalCount = await _auditLogRepository.GetEventCountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / normalizedPageSize);
        var normalizedPage = Math.Clamp(currentPage, 1, totalPages);
        var pageRead = await _auditLogRepository.GetPageAsync(
            normalizedPage,
            normalizedPageSize,
            normalizedSortBy,
            normalizedSortDirection,
            includeDetails: true,
            cancellationToken);
        var pageLogs = SortLogsForPage(
                MapAuditRows(pageRead.Rows, pageRead.SourceColumnOrders, reconstructPreviousSnapshots: false),
                normalizedSortBy,
                normalizedSortDirection)
            .Take(normalizedPageSize)
            .ToList();
        if (stripDetails)
        {
            StripLogDetails(pageLogs);
        }

        return new AuditLogPageViewModel
        {
            Logs = pageLogs,
            CurrentPage = normalizedPage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = normalizedPageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty
        };
    }

    public virtual async Task<IReadOnlyList<Log>> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _auditLogRepository.GetAllAsync(cancellationToken);
        var groupedLogs = MapAuditRows(result.Rows, result.SourceColumnOrders, reconstructPreviousSnapshots: true);
        AssignDisplayLogIds(groupedLogs);

        return groupedLogs
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.IdLog)
            .ToList();
    }

    private static void StripLogDetails(IEnumerable<Log> logs)
    {
        foreach (var log in logs)
        {
            log.ExtraData = BuildLookupDetails(log.ExtraData as JObject);
        }
    }

    private static JObject? BuildLookupDetails(JObject? details)
    {
        if (details == null)
        {
            return null;
        }

        var lookup = new JObject
        {
            ["source_table"] = ExtractValue(details, "source_table"),
            ["target_id"] = ExtractValue(details, "target_id"),
            ["is_chain"] = details["is_chain"] ?? new JValue(false)
        };

        var sourceItem = details["items"] is JArray items
            ? items
                .OfType<JObject>()
                .OrderBy(GetItemSourceOrder)
                .ThenBy(GetItemAuditId)
                .FirstOrDefault()
            : details;

        lookup["audit_lookup_source_table"] = ExtractValue(sourceItem, "source_table");
        lookup["audit_lookup_id"] = ExtractValue(sourceItem, "audit_id") ?? ExtractValue(details, "audit_id");

        return lookup;
    }

    private static List<Log> SortLogsForPage(IReadOnlyList<Log> logs, string sortBy, string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.Ordinal);
        IOrderedEnumerable<Log> orderedLogs = sortBy switch
        {
            AuditLogSortFields.User => descending
                ? logs.OrderByDescending(log => GetUserValue(log), AuditLogStringComparer)
                : logs.OrderBy(log => GetUserValue(log), AuditLogStringComparer),
            AuditLogSortFields.Event => descending
                ? logs.OrderByDescending(log => GetEventValue(log), AuditLogStringComparer)
                : logs.OrderBy(log => GetEventValue(log), AuditLogStringComparer),
            AuditLogSortFields.Description => descending
                ? logs.OrderByDescending(log => GetDescriptionValue(log), AuditLogStringComparer)
                : logs.OrderBy(log => GetDescriptionValue(log), AuditLogStringComparer),
            _ => descending
                ? logs.OrderByDescending(log => log.Date)
                : logs.OrderBy(log => log.Date)
        };

        orderedLogs = descending
            ? orderedLogs.ThenByDescending(log => log.IdLog)
            : orderedLogs.ThenBy(log => log.IdLog);

        if (!string.Equals(sortBy, AuditLogSortFields.Date, StringComparison.Ordinal))
        {
            orderedLogs = orderedLogs.ThenByDescending(log => log.Date).ThenByDescending(log => log.IdLog);
        }

        return orderedLogs.ToList();
    }

    private static string NormalizeSortField(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            AuditLogSortFields.User => AuditLogSortFields.User,
            AuditLogSortFields.Event => AuditLogSortFields.Event,
            AuditLogSortFields.Description => AuditLogSortFields.Description,
            _ => AuditLogSortFields.Date
        };
    }

    private static string NormalizeSortDirection(string? sortDirection, string sortBy)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return string.Equals(sortBy, AuditLogSortFields.Date, StringComparison.Ordinal)
            ? "desc"
            : "asc";
    }

    private static string GetUserValue(Log log) => string.IsNullOrWhiteSpace(log.NameUser) ? "Система" : log.NameUser!;

    private static string GetEventValue(Log log) => string.IsNullOrWhiteSpace(log.EventType) ? "—" : log.EventType!;

    private static string GetDescriptionValue(Log log) => string.IsNullOrWhiteSpace(log.Description) ? "—" : log.Description!;

    private async Task EnrichAnswerContextAsync(Log log, CancellationToken cancellationToken)
    {
        if (log.ExtraData is not JObject details)
        {
            return;
        }

        if (!ContainsSourceTable(details, "answer") && !ContainsSourceTable(details, "answer_item"))
        {
            return;
        }

        var idOrganizationSurvey = FindDetailValue(details, "id_organization_survey");
        var idAnswer = FindDetailValue(details, "id_answer");
        var organizationSurveyId = int.TryParse(idOrganizationSurvey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOrganizationSurveyId)
            ? parsedOrganizationSurveyId
            : (int?)null;
        var answerId = int.TryParse(idAnswer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAnswerId)
            ? parsedAnswerId
            : (int?)null;
        var context = await _auditLogRepository.GetAnswerContextAsync(
            organizationSurveyId,
            answerId,
            cancellationToken);

        if (context == null)
        {
            return;
        }

        details["semantic_context"] = new JObject
        {
            ["id_survey"] = context.IdSurvey,
            ["name_survey"] = context.SurveyName,
            ["completed_by"] = string.IsNullOrWhiteSpace(log.NameUser) ? "Система" : log.NameUser,
            ["id_organization"] = context.IdOrganization,
            ["organization_name"] = context.OrganizationName
        };
    }

    private static string? FindDetailValue(JObject details, string propertyName)
    {
        var value = ExtractValue(details, propertyName)
            ?? ExtractValue(details["record_pk"] as JObject, propertyName)
            ?? ExtractValue(details["row_data"] as JObject, propertyName)
            ?? ExtractValue(details["new_row_data"] as JObject, propertyName)
            ?? ExtractValue(details["old_row_data"] as JObject, propertyName);

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (details["items"] is not JArray items)
        {
            return null;
        }

        foreach (var item in items.OfType<JObject>())
        {
            value = FindDetailValue(item, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool ContainsSourceTable(JObject details, string sourceTable)
    {
        if (string.Equals(ExtractValue(details, "source_table"), sourceTable, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return details["items"] is JArray items
               && items
                   .OfType<JObject>()
                   .Any(item => string.Equals(ExtractValue(item, "source_table"), sourceTable, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssignDisplayLogIds(IReadOnlyList<Log> logs)
    {
        var nextId = 1L;
        foreach (var log in SortLogs(logs))
        {
            log.IdLog = nextId++;
        }
    }

}
