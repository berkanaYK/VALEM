namespace VALE.Api.Services;

public static class TextNormalizer
{
    public static string Plate(string value) => string.Concat(
            value.Where(char.IsLetterOrDigit))
        .ToUpperInvariant();

    public static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Concat(value.Where(char.IsDigit));
        return normalized.Length == 0 ? null : normalized;
    }
}

