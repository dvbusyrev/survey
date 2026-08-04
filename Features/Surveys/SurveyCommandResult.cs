namespace MainProject.Application.DTO;

public sealed class SurveyCommandResult
{
    public bool Success { get; init; }
    public bool NotFound { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? SurveyId { get; init; }
}
