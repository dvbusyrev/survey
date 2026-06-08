using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class AnswerSignatureInfoViewModel
{
    [JsonPropertyName("is_signed")]
    public bool IsSigned { get; init; }

    [JsonPropertyName("is_valid")]
    public bool? IsValid { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("signed_by")]
    public string SignedBy { get; init; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; init; } = string.Empty;

    [JsonPropertyName("issuer")]
    public string Issuer { get; init; } = string.Empty;

    [JsonPropertyName("serial_number")]
    public string SerialNumber { get; init; } = string.Empty;

    [JsonPropertyName("thumbprint")]
    public string Thumbprint { get; init; } = string.Empty;

    [JsonPropertyName("valid_from")]
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("valid_to")]
    public string ValidTo { get; init; } = string.Empty;

    [JsonPropertyName("validation_message")]
    public string ValidationMessage { get; init; } = string.Empty;
}
