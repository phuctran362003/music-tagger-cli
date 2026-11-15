using MusicTagger.Core.Contracts;

namespace MusicTagger.Core.Validation;

public sealed record PathValidationResult(bool IsValid, string? FullPath, string? Error)
{
    public static PathValidationResult Ok(string fullPath) => new(true, fullPath, null);

    public static PathValidationResult Invalid(string error) => new(false, null, error);
}

/// <summary>
/// Validates the folder path entered (or pasted) by the user. Quotes around a pasted
/// path are removed; nothing else about the path is rewritten (SRS FR-002).
/// </summary>
public static class FolderPathValidator
{
    public static string Normalize(string? rawInput)
    {
        if (rawInput is null)
        {
            return string.Empty;
        }

        var value = rawInput.Trim();

        // Strip one matching pair of surrounding quotes ("..." or '...').
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                value = value[1..^1].Trim();
            }
        }

        return value;
    }

    public static PathValidationResult Validate(string? rawInput, IFileProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var normalized = Normalize(rawInput);
        if (normalized.Length == 0)
        {
            return PathValidationResult.Invalid("Please enter a folder path.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(normalized);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathValidationResult.Invalid("That path is not a valid file system path.");
        }

        if (!probe.DirectoryExists(fullPath))
        {
            return PathValidationResult.Invalid($"Folder not found: {fullPath}");
        }

        var accessProblem = probe.CheckDirectoryAccess(fullPath);
        if (accessProblem is not null)
        {
            return PathValidationResult.Invalid(accessProblem);
        }

        return PathValidationResult.Ok(fullPath);
    }
}
