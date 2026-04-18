namespace Advertified.App.Support;

public static class ProspectLeadContactNormalizer
{
    public static string NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        if (digits.StartsWith("27", StringComparison.Ordinal) && digits.Length == 11)
        {
            return $"+{digits}";
        }

        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 10)
        {
            return $"+27{digits[1..]}";
        }

        if (digits.Length == 9)
        {
            return $"+27{digits}";
        }

        return value.Trim().StartsWith('+') ? $"+{digits}" : digits;
    }
}
