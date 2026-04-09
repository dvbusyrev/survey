namespace MainProject.Application.DTO;

public sealed class OperationResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public string? Details { get; init; }
}
