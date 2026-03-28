using System.Text.RegularExpressions;

namespace Advertified.App.Validation;

public static class RegistrationValidators
{
    private static readonly Regex StrongPasswordRegex =
        new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{12,}$", RegexOptions.Compiled);

    private static readonly Regex SaCompanyRegistrationRegex =
        new(@"^\d{4}/\d{6,7}/\d{2}$", RegexOptions.Compiled);

    private static readonly Regex SaIdRegex =
        new(@"^\d{13}$", RegexOptions.Compiled);

    private static readonly Regex Iso2Regex =
        new(@"^[A-Z]{2}$", RegexOptions.Compiled);

    public static bool HasNameAndSurname(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return false;
        }

        var parts = fullName.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2 && parts.All(p => p.Length >= 2);
    }

    public static bool IsStrongPassword(string password) =>
        !string.IsNullOrWhiteSpace(password) && StrongPasswordRegex.IsMatch(password);

    public static bool IsSaCompanyRegistrationNumber(string value) =>
        !string.IsNullOrWhiteSpace(value) && SaCompanyRegistrationRegex.IsMatch(value.Trim());

    public static bool IsSaIdNumber(string value) =>
        !string.IsNullOrWhiteSpace(value) && SaIdRegex.IsMatch(value.Trim());

    public static bool IsIso2CountryCode(string value) =>
        !string.IsNullOrWhiteSpace(value) && Iso2Regex.IsMatch(value.Trim().ToUpperInvariant());
}
