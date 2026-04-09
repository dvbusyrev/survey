using MainProject.Models;
using System.Text.Json.Serialization;

namespace MainProject.Services.Surveys;

public sealed class SurveyAnswerPageViewModel
{
    public Survey Survey { get; init; } = new();
    public IReadOnlyList<SurveyAnswerEntryViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerEntryViewModel>();
    public string Role { get; init; } = string.Empty;
}

public sealed class SurveyAnswerEntryViewModel
{
    public int IdAnswer { get; init; }
    public int IdOrganization { get; init; }
    public int IdSurvey { get; init; }
    public string NameOrganization { get; init; } = string.Empty;
    public string? Csp { get; init; }
    public DateTime? CompletionDate { get; init; }
    public IReadOnlyList<SurveyAnswerDetailViewModel> Details { get; init; } = Array.Empty<SurveyAnswerDetailViewModel>();
}

public sealed class SurveyAnswerDetailViewModel
{
    [JsonPropertyName("question_text")]
    public string? QuestionText { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    public string DisplayQuestion => QuestionText ?? Text ?? string.Empty;
}
