namespace MainProject.Application.DTO;

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Error { get; init; }
    public string? Code { get; init; }
    public int? EntityId { get; init; }
    public bool ShouldReload { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
