namespace MusicTagger.Core.Models;

/// <summary>
/// Normalization rules shared by every layer: a metadata value is trimmed and an
/// empty/whitespace value is represented as <c>null</c> (= field absent).
/// Empty strings are never used as a hidden "clear" signal.
/// </summary>
public static class MetadataText
{
    public static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static bool AreEquivalent(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    public static string[] ToList(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return [];
        }

        var parts = normalized.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? [] : parts;
    }

    public static string? FromList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var items = values
            .Select(Normalize)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToArray();

        return items.Length == 0 ? null : string.Join("; ", items);
    }
}
