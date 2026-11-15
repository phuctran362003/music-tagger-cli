using MusicTagger.Core.Models;

namespace MusicTagger.Core.Validation;

/// <summary>
/// Validates a backup manifest before it is used for a restore. An invalid manifest is
/// rejected as a whole: nothing is restored partially (SRS 7.11).
/// </summary>
public static class ManifestValidator
{
    public static IReadOnlyList<string> Validate(BackupManifest? manifest)
    {
        var errors = new List<string>();

        if (manifest is null)
        {
            errors.Add("The manifest could not be read.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(manifest.ManifestId))
        {
            errors.Add("Manifest id is missing.");
        }

        if (string.IsNullOrWhiteSpace(manifest.RootFolder))
        {
            errors.Add("Root folder is missing.");
        }
        else if (!Path.IsPathFullyQualified(manifest.RootFolder))
        {
            errors.Add("Root folder must be an absolute path.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Operation))
        {
            errors.Add("Operation is missing.");
        }

        if (manifest.CreatedUtc == default)
        {
            errors.Add("Creation timestamp is missing.");
        }

        if (manifest.Entries.Count == 0)
        {
            errors.Add("The manifest does not contain any file entries.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                errors.Add("An entry has an empty relative path.");
                continue;
            }

            if (!seen.Add(entry.RelativePath))
            {
                errors.Add($"Duplicate entry for '{entry.RelativePath}'.");
            }

            if (entry.Metadata is null)
            {
                errors.Add($"Entry '{entry.RelativePath}' has no metadata.");
            }

            var root = string.IsNullOrWhiteSpace(manifest.RootFolder) ? null : manifest.RootFolder;
            if (root is not null &&
                !PathSafety.TryResolveContained(root, entry.RelativePath, out _, out var pathError))
            {
                errors.Add($"Entry '{entry.RelativePath}' is not safe: {pathError}");
            }
        }

        return errors;
    }
}
