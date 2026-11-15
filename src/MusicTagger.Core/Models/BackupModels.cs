namespace MusicTagger.Core.Models;

public enum BackupEntryStatus
{
    /// <summary>Old metadata was captured before any write happened.</summary>
    Captured = 0,

    Updated = 1,
    Unchanged = 2,
    Skipped = 3,
    Failed = 4,
}

public sealed record BackupManifestEntry
{
    public required string RelativePath { get; init; }

    public required AudioMetadata Metadata { get; init; }

    public Id3Version Id3Version { get; init; } = Id3Version.Unknown;

    public BackupEntryStatus Status { get; init; } = BackupEntryStatus.Captured;

    public DateTimeOffset? ProcessedUtc { get; init; }

    public string? Message { get; init; }
}

/// <summary>
/// JSON manifest describing the metadata of every file that was part of an edit
/// or restore operation. Audio bytes are never stored here.
/// </summary>
public sealed record BackupManifest
{
    public required string ManifestId { get; init; }

    public required string RootFolder { get; init; }

    public required string Operation { get; init; }

    public DateTimeOffset CreatedUtc { get; init; }

    public string AppVersion { get; init; } = "1.0";

    /// <summary>Absolute path of the manifest file itself (informational).</summary>
    public string? ManifestPath { get; init; }

    public IReadOnlyList<BackupManifestEntry> Entries { get; init; } = [];
}

/// <summary>Metadata captured for one file before writing (input of the backup service).</summary>
public sealed record BackupCaptureItem(
    string FilePath,
    string RelativePath,
    AudioMetadata Metadata,
    Id3Version Id3Version,
    BackupEntryStatus Status,
    string? Message);

/// <summary>Light weight manifest description used to list backups in the UI.</summary>
public sealed record BackupManifestInfo(
    string ManifestPath,
    string ManifestId,
    string Operation,
    string RootFolder,
    DateTimeOffset CreatedUtc,
    int EntryCount);

public sealed record BackupManifestLoad(BackupManifest? Manifest, IReadOnlyList<string> Errors)
{
    public bool IsValid => Manifest is not null && Errors.Count == 0;

    public static BackupManifestLoad Invalid(params string[] errors) => new(null, errors);
}
