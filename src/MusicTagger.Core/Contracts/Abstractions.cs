using MusicTagger.Core.Models;

namespace MusicTagger.Core.Contracts;

/// <summary>Reads metadata from a single audio file.</summary>
public interface IAudioMetadataReader
{
    Task<MetadataReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>Writes metadata for a single audio file (never changes the audio stream).</summary>
public interface IAudioMetadataWriter
{
    Task<MetadataWriteResult> WriteAsync(
        string filePath,
        MetadataUpdate update,
        Id3WriteVersion targetVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>Enumerates supported audio files inside a root folder.</summary>
public interface IAudioFileScanner
{
    Task<ScanResult> ScanAsync(string rootFolder, bool recursive, CancellationToken cancellationToken = default);
}

/// <summary>Small filesystem surface the core needs; keeps the core testable without touching disk.</summary>
public interface IFileProbe
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    bool IsFileReadOnly(string path);

    /// <summary>Returns null when the directory can be listed, otherwise a user friendly reason.</summary>
    string? CheckDirectoryAccess(string path);
}

/// <summary>Creates, saves and loads metadata backup manifests.</summary>
public interface IBackupService
{
    string BackupDirectory { get; }

    Task<BackupManifest> CreateBackupAsync(
        string rootFolder,
        string operation,
        IReadOnlyList<BackupCaptureItem> items,
        CancellationToken cancellationToken = default);

    Task<BackupManifest> SaveManifestAsync(BackupManifest manifest, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupManifestInfo>> ListManifestsAsync(CancellationToken cancellationToken = default);

    Task<BackupManifestLoad> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken = default);
}

/// <summary>Persists application settings.</summary>
public interface ISettingsStore
{
    string SettingsFilePath { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Diagnostic log. UI messages stay friendly; details go here (SRS FR-015).</summary>
public interface IAppLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
