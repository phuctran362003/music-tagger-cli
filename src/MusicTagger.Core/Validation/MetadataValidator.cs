using System.Globalization;
using MusicTagger.Core.Models;

namespace MusicTagger.Core.Validation;

public sealed record FieldValidationResult(bool IsValid, string? Value, string? Error)
{
    public static FieldValidationResult Ok(string value) => new(true, value, null);

    public static FieldValidationResult Invalid(string error) => new(false, null, error);
}

/// <summary>Validates the values the user enters for a metadata field.</summary>
public static class MetadataValidator
{
    public const int MaxTextLength = 512;
    public const int MaxCommentLength = 2048;
    public const int MinYear = 1000;
    public const int MaxYear = 2999;
    public const int MaxPositionNumber = 9999;

    /// <summary>
    /// Validates a value for a <see cref="FieldEditKind.Set"/> edit. Empty or whitespace
    /// only input is rejected: the UI must use Clear to remove a field (SRS 3.2 / 4.1).
    /// </summary>
    public static FieldValidationResult ValidateSetValue(MetadataField field, string? rawValue)
    {
        var descriptor = MetadataFieldCatalog.Get(field);
        var normalized = MetadataText.Normalize(rawValue);

        if (normalized is null)
        {
            return FieldValidationResult.Invalid(
                "The value is empty. Choose Keep to leave the field unchanged, or Clear to remove it.");
        }

        var allowNewLines = field == MetadataField.Comment;
        if (ContainsControlCharacters(normalized, allowNewLines))
        {
            return FieldValidationResult.Invalid("The value contains control characters that cannot be stored.");
        }

        var maxLength = field == MetadataField.Comment ? MaxCommentLength : MaxTextLength;
        if (normalized.Length > maxLength)
        {
            return FieldValidationResult.Invalid($"The value is too long ({normalized.Length} characters, maximum {maxLength}).");
        }

        switch (descriptor.Kind)
        {
            case FieldValueKind.Year:
                return ValidateYear(normalized);
            case FieldValueKind.Position:
                return ValidatePosition(normalized);
            case FieldValueKind.FreeText:
                return FieldValidationResult.Ok(normalized);
            default:
                return FieldValidationResult.Invalid("Unsupported field.");
        }
    }

    public static bool TryParsePosition(string? value, out uint number, out uint? total)
    {
        number = 0;
        total = null;

        var normalized = MetadataText.Normalize(value);
        if (normalized is null)
        {
            return false;
        }

        var parts = normalized.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 2)
        {
            return false;
        }

        if (!TryParseNumber(parts[0], out number) || number == 0)
        {
            return false;
        }

        if (parts.Length == 2)
        {
            if (!TryParseNumber(parts[1], out var parsedTotal) || parsedTotal == 0 || parsedTotal < number)
            {
                return false;
            }

            total = parsedTotal;
        }

        return true;
    }

    public static string FormatPosition(uint number, uint? total) =>
        total is null || total == 0
            ? number.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{number}/{total.Value}");

    private static FieldValidationResult ValidateYear(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return FieldValidationResult.Invalid("Year must be a number, for example 1998.");
        }

        if (year is < MinYear or > MaxYear)
        {
            return FieldValidationResult.Invalid($"Year must be between {MinYear} and {MaxYear}.");
        }

        return FieldValidationResult.Ok(year.ToString(CultureInfo.InvariantCulture));
    }

    private static FieldValidationResult ValidatePosition(string value)
    {
        if (!TryParsePosition(value, out var number, out var total))
        {
            return FieldValidationResult.Invalid(
                "Use a number such as 3, or number/total such as 3/12 (both between 1 and 9999).");
        }

        return FieldValidationResult.Ok(FormatPosition(number, total));
    }

    private static bool TryParseNumber(string text, out uint value) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= MaxPositionNumber;

    private static bool ContainsControlCharacters(string value, bool allowNewLines)
    {
        foreach (var c in value)
        {
            if (c == '\n' && allowNewLines)
            {
                continue;
            }

            if (c == '\r')
            {
                continue;
            }

            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
