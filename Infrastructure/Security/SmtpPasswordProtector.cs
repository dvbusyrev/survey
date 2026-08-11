using Microsoft.AspNetCore.DataProtection;

namespace MainProject.Infrastructure.Security;

public sealed class SmtpPasswordProtector
{
    public const string ProtectedValuePrefix = "smtp-dp:v1:";
    private const string ProtectionPurpose = "AIS.Anketirovanie.Email.SmtpPassword.v1";

    private readonly IDataProtector _protector;

    public SmtpPasswordProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(ProtectionPurpose);
    }

    public bool IsCurrentFormat(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal);

    public string Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return ProtectedValuePrefix + _protector.Protect(value);
    }

    public string Unprotect(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return string.Empty;
        }

        if (IsCurrentFormat(storedValue))
        {
            return _protector.Unprotect(storedValue[ProtectedValuePrefix.Length..]);
        }

        // Older builds could store a Data Protection payload without an application prefix.
        if (storedValue.StartsWith("CfDJ8", StringComparison.Ordinal))
        {
            return _protector.Unprotect(storedValue);
        }

        return storedValue;
    }
}
