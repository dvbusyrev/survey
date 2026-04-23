using System.Net.Mail;

namespace MainProject.Application.DTO.Email;

public static class EmailAddressParser
{
    private static readonly char[] Separators = [';', ',', '\r', '\n'];

    public static IReadOnlyList<string> Split(string? rawValue)
    {
        return (rawValue ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
