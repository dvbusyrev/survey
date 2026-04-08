using System.Text.Json.Serialization;
using MainProject.Models;

namespace MainProject.Services.Answers;

public sealed class OperationResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public string? Details { get; init; }
}

public sealed class CheckAnswersPageViewModel
{
    public Survey Survey { get; init; } = new();
    public IReadOnlyList<AnswerPayloadItem> Answers { get; init; } = Array.Empty<AnswerPayloadItem>();
    public int IdOrganization { get; init; }
}

public sealed class UpdateAnswerPageViewModel
{
    public int SurveyId { get; init; }
    public int OrganizationId { get; init; }
    public IReadOnlyList<AnswerPayloadItem> Answers { get; init; } = Array.Empty<AnswerPayloadItem>();
}

public sealed class SurveyAnswersResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Details { get; init; }
    public SurveyAnswersSurveyViewModel? Survey { get; init; }
    public IReadOnlyList<SurveyAnswerResultViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerResultViewModel>();
}

public sealed class SurveyAnswersSurveyViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    [JsonPropertyName("is_archive")]
    public bool IsArchive { get; init; }

    public string? Csp { get; init; }
}

public sealed class SurveyAnswerResultViewModel
{
    public int Id { get; init; }

    [JsonPropertyName("organization_id")]
    public int OrganizationId { get; init; }

    [JsonPropertyName("organization_name")]
    public string OrganizationName { get; init; } = string.Empty;

    public string Date { get; init; } = string.Empty;
    public IReadOnlyList<SurveyAnswerResultItemViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerResultItemViewModel>();

    [JsonPropertyName("is_signed")]
    public bool IsSigned { get; init; }

    public string? Signature { get; init; }
}

public sealed class SurveyAnswerResultItemViewModel
{
    [JsonPropertyName("question_text")]
    public string QuestionText { get; init; } = string.Empty;

    public string Rating { get; init; } = "0";

    public string Comment { get; init; } = string.Empty;
}
