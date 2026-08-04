using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class CheckAnswersPageViewModel
{
    public Survey Survey { get; init; } = new();
    public IReadOnlyList<AnswerPayloadItem> Answers { get; init; } = Array.Empty<AnswerPayloadItem>();
    public int IdOrganization { get; init; }
}
