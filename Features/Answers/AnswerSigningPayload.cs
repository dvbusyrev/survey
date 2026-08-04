namespace MainProject.Application.DTO;

public sealed class AnswerSigningPayload
{
    public string Content { get; init; } = string.Empty;
    public string ContentEncoding { get; init; } = "utf8";
    public bool Detached { get; init; }
    public string FileName { get; init; } = string.Empty;
}
