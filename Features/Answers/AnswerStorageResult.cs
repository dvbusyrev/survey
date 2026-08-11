namespace MainProject.Application.DTO;

public sealed class AnswerStorageResult
{
    public bool Found { get; init; }
    public bool SubmissionClosed { get; init; }
    public bool AlreadySigned { get; init; }
    public bool AlreadySubmitted { get; init; }
    public int AnswerId { get; init; }
}

public sealed class AnswerDeleteStorageResult
{
    public bool Found { get; init; }
    public bool SurveyIsActive { get; init; }
    public bool Deleted { get; init; }
}
