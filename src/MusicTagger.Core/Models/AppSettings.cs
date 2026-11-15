namespace MusicTagger.Core.Models;

public enum ColorMode
{
    Auto = 0,
    On = 1,
    Off = 2,
}

/// <summary>User settings persisted as JSON in the application data directory.</summary>
public sealed record AppSettings
{
    public Id3WriteVersion Id3WriteVersion { get; init; } = Id3WriteVersion.V23;

    public bool RecursiveScan { get; init; }

    /// <summary>Override of the backup directory. <c>null</c> = default application data location.</summary>
    public string? BackupDirectory { get; init; }

    public ColorMode ConsoleColor { get; init; } = ColorMode.Auto;

    public static AppSettings Default { get; } = new();

    /// <summary>Returns human readable validation problems; empty when the settings are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (BackupDirectory is not null && BackupDirectory.Trim().Length == 0)
        {
            errors.Add("Backup directory must not be an empty string. Leave it empty (not set) to use the default.");
        }

        return errors;
    }
}
