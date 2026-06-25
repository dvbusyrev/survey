namespace MainProject.Application.DTO.Read;

public sealed record AnswerListReadRequest(
    IReadOnlyCollection<int> OrganizationIds,
    IReadOnlyCollection<int> SurveyIds,
    DateTime? DateStart,
    DateTime? DateEnd,
    string SortBy,
    string SortDirection,
    int CurrentPage,
    int PageSize,
    int Offset = 0);

public sealed class AnswerListReadRow
{
    public int IdAnswer { get; init; }
    public int IdOrganization { get; init; }
    public int IdSurvey { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public string SurveyName { get; init; } = string.Empty;
    public DateTime? CompletionDate { get; init; }
    public bool IsSigned { get; init; }
}

public sealed record AnswerListReadData(
    int TotalCount,
    int CurrentPage,
    int TotalPages,
    int PageSize,
    IReadOnlyList<AnswerListReadRow> Rows,
    IReadOnlyList<SelectionOption> OrganizationOptions,
    IReadOnlyList<SelectionOption> SurveyOptions);

public sealed class SurveySignatureReadRow
{
    public string OrganizationName { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public bool IsSigned { get; init; }
    public DateTime? CompletionDate { get; init; }
}

public sealed record SurveySignatureReadData(
    string SurveyName,
    IReadOnlyList<SurveySignatureReadRow> Rows);

public sealed class AverageByYearReadRow
{
    public int Year { get; init; }
    public double AverageRating { get; init; }
}

public sealed class AverageByQuarterReadRow
{
    public int Quarter { get; init; }
    public double AverageRating { get; init; }
}

public sealed class OrganizationAverageReadRow
{
    public string OrganizationName { get; init; } = string.Empty;
    public double AverageRating { get; init; }
}

public sealed record AnswerStatisticsReadData(
    IReadOnlyList<AverageByYearReadRow> ByYear,
    IReadOnlyList<AverageByQuarterReadRow> ByQuarter,
    IReadOnlyList<OrganizationAverageReadRow> ByOrganization);
