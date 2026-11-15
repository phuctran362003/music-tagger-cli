using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;

namespace MusicTagger.Core.Tests.Support;

public sealed class FakeFileProbe : IFileProbe
{
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ReadOnlyFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ExistingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> DirectoryAccessProblems { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => ExistingFiles.Contains(path);

    public bool DirectoryExists(string path) => ExistingDirectories.Contains(path);

    public bool IsFileReadOnly(string path) => ReadOnlyFiles.Contains(path);

    public string? CheckDirectoryAccess(string path) =>
        DirectoryAccessProblems.TryGetValue(path, out var problem) ? problem : null;
}

public sealed class FakeAudioMetadataReader : IAudioMetadataReader
{
    public Dictionary<string, AudioMetadata> MetadataByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> FailingPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int ReadCount { get; private set; }

    public Task<MetadataReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        if (FailingPaths.Contains(filePath))
        {
            return Task.FromResult(MetadataReadResult.Failure("corrupt tag"));
        }

        var metadata = MetadataByPath.TryGetValue(filePath, out var stored) ? stored : AudioMetadata.Empty;
        return Task.FromResult(MetadataReadResult.Ok(metadata, TagFormat.Id3v23));
    }
}

public sealed class FakeAudioMetadataWriter : IAudioMetadataWriter
{
    public List<(string FilePath, MetadataUpdate Update, Id3WriteVersion Version)> Writes { get; } = [];

    public Dictionary<string, AudioMetadata> MetadataByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> FailingPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> VerificationFailurePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<MetadataWriteResult> WriteAsync(
        string filePath,
        MetadataUpdate update,
        Id3WriteVersion targetVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Writes.Add((filePath, update, targetVersion));

        if (FailingPaths.Contains(filePath))
        {
            return Task.FromResult(MetadataWriteResult.Failure("disk is on fire"));
        }

        var current = MetadataByPath.TryGetValue(filePath, out var stored) ? stored : AudioMetadata.Empty;
        var updated = VerificationFailurePaths.Contains(filePath)
            ? current
            : update.ApplyTo(current);

        MetadataByPath[filePath] = updated;
        return Task.FromResult(MetadataWriteResult.Ok(updated));
    }
}

public sealed class FakeBackupService : IBackupService
{
    public List<BackupManifest> Manifests { get; } = [];

    public Exception? CreateFailure { get; set; }

    public string BackupDirectory => "/fake/backups";

    public Task<BackupManifest> CreateBackupAsync(
        string rootFolder,
        string operation,
        IReadOnlyList<BackupCaptureItem> items,
        CancellationToken cancellationToken = default)
    {
        if (CreateFailure is not null)
        {
            return Task.FromException<BackupManifest>(CreateFailure);
        }

        var manifest = new BackupManifest
        {
            ManifestId = Guid.NewGuid().ToString("N"),
            RootFolder = rootFolder,
            Operation = operation,
            CreatedUtc = DateTimeOffset.UtcNow,
            ManifestPath = $"/fake/backups/{Guid.NewGuid():N}.json",
            Entries = items
                .Select(item => new BackupManifestEntry
                {
                    RelativePath = item.RelativePath,
                    Metadata = item.Metadata,
                    TagFormat = item.TagFormat,
                    Status = item.Status,
                })
                .ToList(),
        };

        Manifests.Add(manifest);
        return Task.FromResult(manifest);
    }

    public Task<BackupManifest> SaveManifestAsync(BackupManifest manifest, CancellationToken cancellationToken = default)
    {
        var index = Manifests.FindIndex(existing => existing.ManifestId == manifest.ManifestId);
        if (index >= 0)
        {
            Manifests[index] = manifest;
        }
        else
        {
            Manifests.Add(manifest);
        }

        return Task.FromResult(manifest);
    }

    public Task<IReadOnlyList<BackupManifestInfo>> ListManifestsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupManifestInfo>>(
            Manifests
                .Select(manifest => new BackupManifestInfo(
                    manifest.ManifestPath ?? string.Empty,
                    manifest.ManifestId,
                    manifest.Operation,
                    manifest.RootFolder,
                    manifest.CreatedUtc,
                    manifest.Entries.Count))
                .ToList());

    public Task<BackupManifestLoad> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        var manifest = Manifests.FirstOrDefault(candidate => candidate.ManifestPath == manifestPath);
        return Task.FromResult(manifest is null
            ? BackupManifestLoad.Invalid("not found")
            : new BackupManifestLoad(manifest, []));
    }
}

public sealed class RecordingLog : IAppLog
{
    public List<string> Messages { get; } = [];

    public void Info(string message) => Messages.Add("INFO: " + message);

    public void Warn(string message) => Messages.Add("WARN: " + message);

    public void Error(string message, Exception? exception = null) => Messages.Add("ERROR: " + message);
}

/// <summary>IProgress implementation that reports inline (Progress<T> posts asynchronously).</summary>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _onReport;

    public SynchronousProgress(Action<T> onReport) => _onReport = onReport;

    public void Report(T value) => _onReport(value);
}

