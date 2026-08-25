namespace MainProject.Application.DTO;

public sealed class SurveyAutoCreationCommandResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int SelectedTemplateCount { get; init; }
}
