namespace MainProject.Application.DTO;

public sealed class AnswerGeneratedFileResult
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = string.Empty;
}
