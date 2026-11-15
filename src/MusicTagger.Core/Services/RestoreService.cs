using MusicTagger.Core.Contracts;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;

namespace MusicTagger.Core.Services;

/// <summary>
/// Lists backups, builds a restore preview and executes the restore through the
/// regular batch pipeline (so the current state is backed up before anything is written).
/// </summary>
public sealed class RestoreService
{
    private readonly IAudioMetadataReader _reader;
    private readonly IBackupService _backupService;
    private readonly BatchUpdateExecutor _executor;
    private readonly IFileProbe _probe;
    private readonly IAppLog _log;

    public RestoreService(
        IAudioMetadataReader reader,
        IBackupService backupService,
        BatchUpdateExecutor executor,
        IFileProbe probe,
        IAppLog log)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<IReadOnlyList<BackupManifestInfo>> ListBackupsAsync(CancellationToken cancellationToken = default) =>
        _backupService.ListManifestsAsync(cancellationToken);

    public Task<BackupManifestLoad> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken = default) =>
        _backupService.LoadManifestAsync(manifestPath, cancellationToken);

    /// <summary>
    /// Builds the restore preview. The manifest is validated first; an invalid manifest is
    /// rejected as a whole so nothing is restored partially (SRS 7.11).
    /// </summary>
    public async Task<RestorePreview> BuildPreviewAsync(
        BackupManifest manifest,
        string? rootFolderOverride = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var manifestErrors = ManifestValidator.Validate(manifest);
        if (manifestErrors.Count > 0)
        {
            throw new InvalidManifestException(manifestErrors);
        }

        var rootFolder = string.IsNullOrWhiteSpace(rootFolderOverride) ? manifest.RootFolder : rootFolderOverride;
        var warnings = new List<string>();

        if (!PathSafety.IsInside(manifest.RootFolder, Path.GetFullPath(rootFolder)))
        {
            warnings.Add(
                $"Restoring into '{rootFolder}', which is outside the original root '{manifest.RootFolder}'.");
        }

        if (!_probe.DirectoryExists(rootFolder))
        {
            throw new InvalidManifestException([$"Root folder not found: {rootFolder}"]);
        }

        var plans = new List<PlannedFileUpdate>(manifest.Entries.Count);
        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            plans.Add(await PlanEntryAsync(rootFolder, entry, cancellationToken).ConfigureAwait(false));
            progress?.Report(plans.Count);
        }

        var preview = new UpdatePreview(rootFolder, manifest.Entries.Count, plans);
        return new RestorePreview(manifest, preview, warnings);
    }

    public Task<BatchExecutionResult> ExecuteAsync(
        RestorePreview preview,
        Id3WriteVersion writeVersion,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var request = new BatchUpdateRequest(
            preview.RootFolder,
            "restore",
            preview.Preview.Files,
            writeVersion);

        return _executor.ExecuteAsync(request, progress, cancellationToken);
    }

    /// <summary>
    /// Builds the update that turns <paramref name="current"/> into <paramref name="target"/>.
    /// Returns null when the values already match.
    /// </summary>
    public static MetadataUpdate? BuildRestoreUpdate(AudioMetadata target, AudioMetadata current)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(current);

        var update = MetadataUpdate.Empty;
        foreach (var descriptor in MetadataFieldCatalog.All)
        {
            var targetValue = MetadataText.Normalize(target.GetValue(descriptor.Field));
            var currentValue = MetadataText.Normalize(current.GetValue(descriptor.Field));

            if (string.Equals(targetValue, currentValue, StringComparison.Ordinal))
            {
                continue;
            }

            update = targetValue is null
                ? update.WithClear(descriptor.Field)
                : update.WithSet(descriptor.Field, targetValue);
        }

        return update.IsEmpty ? null : update;
    }

    private async Task<PlannedFileUpdate> PlanEntryAsync(
        string rootFolder,
        BackupManifestEntry entry,
        CancellationToken cancellationToken)
    {
        if (!PathSafety.TryResolveContained(rootFolder, entry.RelativePath, out var filePath, out var pathError))
        {
            return new PlannedFileUpdate(
                entry.RelativePath,
                entry.RelativePath,
                MetadataUpdate.Empty,
                AudioMetadata.Empty,
                [],
                PreviewStatus.Failed,
                $"Unsafe path in manifest: {pathError}");
        }

        if (!_probe.FileExists(filePath))
        {
            return new PlannedFileUpdate(
                filePath,
                entry.RelativePath,
                MetadataUpdate.Empty,
                AudioMetadata.Empty,
                [],
                PreviewStatus.Skipped,
                "The file no longer exists.");
        }

        MetadataReadResult read;
        try
        {
            read = await _reader.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"Could not read metadata from '{filePath}' during restore.", ex);
            read = MetadataReadResult.Failure(UserFriendlyErrors.Describe(ex));
        }

        if (!read.Success)
        {
            return new PlannedFileUpdate(
                filePath,
                entry.RelativePath,
                MetadataUpdate.Empty,
                AudioMetadata.Empty,
                [],
                PreviewStatus.Failed,
                read.ErrorMessage ?? "The metadata could not be read.");
        }

        var update = BuildRestoreUpdate(entry.Metadata, read.Metadata);
        if (update is null)
        {
            return new PlannedFileUpdate(
                filePath,
                entry.RelativePath,
                MetadataUpdate.Empty,
                read.Metadata,
                [],
                PreviewStatus.Unchanged,
                "The stored metadata already matches the backup.",
                read.TagFormat);
        }

        update.TryApply(read.Metadata, out _, out var changes);

        if (_probe.IsFileReadOnly(filePath))
        {
            return new PlannedFileUpdate(
                filePath,
                entry.RelativePath,
                update,
                read.Metadata,
                changes,
                PreviewStatus.Failed,
                "The file is read-only.",
                read.TagFormat);
        }

        return new PlannedFileUpdate(
            filePath,
            entry.RelativePath,
            update,
            read.Metadata,
            changes,
            PreviewStatus.WillUpdate,
            null,
            read.TagFormat);
    }
}

