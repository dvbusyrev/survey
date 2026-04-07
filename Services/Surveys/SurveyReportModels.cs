namespace main_project.Services.Surveys;

public sealed class GeneratedFileResult
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = string.Empty;
}

public sealed class SurveyQuestions
{
    public SurveyQuestion[]? questions { get; set; }
    public int survey_id { get; set; }
}

public sealed class SurveyQuestion
{
    public string? text { get; set; }
    public int question_id { get; set; }
}

public sealed class AnswerData
{
    public int? rating { get; set; }
    public string? comment { get; set; }
    public string? question_id { get; set; }
}
