namespace MusicTagger.Core.Validation;

/// <summary>
/// Guards against path traversal: a path that comes from user input or from a backup
/// manifest must resolve to a location inside the confirmed root folder.
/// </summary>
public static class PathSafety
{
    private static readonly char[] InvalidRelativePathChars = ['\0'];

    /// <summary>
    /// Combines <paramref name="rootFolder"/> with <paramref name="relativePath"/> and verifies
    /// that the result stays inside the root folder.
    /// </summary>
    public static bool TryResolveContained(
        string rootFolder,
        string relativePath,
        out string fullPath,
        out string? error)
    {
        fullPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            error = "Root folder is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "Relative path is empty.";
            return false;
        }

        if (relativePath.IndexOfAny(InvalidRelativePathChars) >= 0)
        {
            error = "Relative path contains invalid characters.";
            return false;
        }

        if (Path.IsPathRooted(relativePath))
        {
            error = "Absolute paths are not allowed inside a manifest.";
            return false;
        }

        // "C:file.mp3" on Windows is rooted but IsPathRooted can be false for drive-relative paths.
        if (relativePath.Length > 1 && relativePath[1] == ':')
        {
            error = "Drive qualified paths are not allowed inside a manifest.";
            return false;
        }

        string rootFullPath;
        string candidate;
        try
        {
            rootFullPath = TrimEndSeparator(Path.GetFullPath(rootFolder));
            candidate = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "The path could not be normalized.";
            return false;
        }

        if (!IsInside(rootFullPath, candidate))
        {
            error = "The path escapes the root folder.";
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>True when <paramref name="candidate"/> is the root itself or a child of it.</summary>
    public static bool IsInside(string rootFullPath, string candidate)
    {
        var root = TrimEndSeparator(rootFullPath);
        var comparison = IsCaseInsensitiveFileSystem ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (string.Equals(root, TrimEndSeparator(candidate), comparison))
        {
            return true;
        }

        var prefix = root + Path.DirectorySeparatorChar;
        if (root.EndsWith(Path.DirectorySeparatorChar))
        {
            prefix = root;
        }

        return candidate.StartsWith(prefix, comparison);
    }

    /// <summary>Relative path of a file inside the root, using the platform separator.</summary>
    public static string ToRelativePath(string rootFullPath, string filePath)
    {
        var relative = Path.GetRelativePath(TrimEndSeparator(rootFullPath), filePath);
        return relative;
    }

    // Path.TrimEndingDirectorySeparator keeps the separator of a root such as "/" or "C:\".
    private static string TrimEndSeparator(string path) => Path.TrimEndingDirectorySeparator(path);

    public static bool IsCaseInsensitiveFileSystem => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
}
