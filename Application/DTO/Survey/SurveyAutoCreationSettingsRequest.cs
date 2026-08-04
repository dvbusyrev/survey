namespace MainProject.Application.DTO;

public sealed class SurveyAutoCreationSettingsRequest
{
    public string ReportingPeriod { get; set; } = "month";
    public int ReportingOffsetBusinessDays { get; set; } = 1;
    public int ActivePeriodBusinessDays { get; set; } = 8;
    public List<int> SurveyIds { get; set; } = new();
}

public sealed class SurveyAutoCreationPreviewRequest
{
    public string ReportingPeriod { get; set; } = "month";
    public int ReportingOffsetBusinessDays { get; set; } = 1;
    public int ActivePeriodBusinessDays { get; set; } = 8;
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }
}

public sealed class SurveyAutoCreationPreviewResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int TargetYear { get; init; }
    public int TargetMonth { get; init; }
    public string StartDate { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
    public IReadOnlyList<SurveyAutoCreationPreviewPeriod> Periods { get; init; }
        = Array.Empty<SurveyAutoCreationPreviewPeriod>();
}

public sealed class SurveyAutoCreationPreviewPeriod
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string StartDate { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
}
