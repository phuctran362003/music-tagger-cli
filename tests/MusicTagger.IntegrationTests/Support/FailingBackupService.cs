using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;

namespace MusicTagger.IntegrationTests.Support;

/// <summary>Backup service stub that always fails, used to prove that no file is written then.</summary>
public sealed class FailingBackupService : IBackupService
{
    public string BackupDirectory => Path.Combine(Path.GetTempPath(), "musictagger-tests", "never");

    public Task<BackupManifest> CreateBackupAsync(
        string rootFolder,
        string operation,
        IReadOnlyList<BackupCaptureItem> items,
        CancellationToken cancellationToken = default) =>
        throw new IOException("Simulated backup failure: the backup folder is not writable.");

    public Task<BackupManifest> SaveManifestAsync(BackupManifest manifest, CancellationToken cancellationToken = default) =>
        throw new IOException("Simulated backup failure.");

    public Task<IReadOnlyList<BackupManifestInfo>> ListManifestsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupManifestInfo>>([]);

    public Task<BackupManifestLoad> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(BackupManifestLoad.Invalid("Simulated backup failure."));
}
