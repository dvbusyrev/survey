namespace MainProject.Application.DTO;

public sealed class SurveySignatureStatusViewModel
{
    public string OrganizationName { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public bool IsSigned { get; init; }
    public string CompletionStatus => IsCompleted ? "Пройдена" : "Не пройдена";
    public string SignatureStatus => IsSigned ? "Подписана" : "Не подписана";
}
