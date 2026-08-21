namespace MainProject.Application.DTO;

public sealed class SelectionOption
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<int> Ids { get; init; } = Array.Empty<int>();
}
