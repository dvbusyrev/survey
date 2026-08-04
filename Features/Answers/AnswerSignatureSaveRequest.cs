namespace MainProject.Application.DTO;

public sealed class AnswerSignatureSaveRequest
{
    public string Signature { get; init; } = string.Empty;
    public string? SignedContent { get; init; }
    public string? ContentEncoding { get; init; }
    public bool Detached { get; init; }
}
