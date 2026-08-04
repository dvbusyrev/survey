namespace MainProject.Application.DTO;

public sealed class AnswerStorageResult
{
    public bool Found { get; init; }
    public bool AlreadySigned { get; init; }
    public int AnswerId { get; init; }
}
