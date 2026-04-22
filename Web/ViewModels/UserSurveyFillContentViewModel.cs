using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserSurveyFillContentViewModel
{
    public Survey Survey { get; init; } = new();
    public int OrganizationId { get; init; }
    public IReadOnlyList<SurveyQuestionItem> Questions { get; init; } = Array.Empty<SurveyQuestionItem>();
}
