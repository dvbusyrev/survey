namespace MainProject.Application.DTO.User;

public sealed class UserDeleteCandidate
{
    public int IdUser { get; init; }
    public string? FullName { get; init; }
    public string? UserName { get; init; }
}

public sealed record UserWriteModel(
    int OrganizationId,
    string Login,
    string FullName,
    string Role,
    string? PasswordHash,
    string? Email,
    DateTime? DateBegin,
    DateTime? DateEnd);

public sealed record UserDeletionResult(
    bool Found,
    bool Deleted,
    UserDeleteCandidate? User,
    IReadOnlyList<string> AnsweredSurveyNames,
    IReadOnlyList<string> SignedSurveyNames);
