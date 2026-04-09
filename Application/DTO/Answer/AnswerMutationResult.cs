using MainProject.Web.ViewModels;

namespace MainProject.Application.DTO;

public sealed class AnswerMutationResult
{
    public bool Success { get; init; }
    public bool NotFound { get; init; }
    public string? Error { get; init; }
    public CheckAnswersPageViewModel? Model { get; init; }
}
