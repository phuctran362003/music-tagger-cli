using MusicTagger.Core.Contracts;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;

namespace MusicTagger.Core.Services;

/// <summary>
/// Applies planned metadata updates file by file.
/// <list type="number">
/// <item>A backup manifest with the old metadata is written <b>before</b> the first write.</item>
/// <item>Each file is written and read back for verification; a failure never stops the batch.</item>
/// <item>The manifest is updated with the per-file result at the end.</item>
/// </list>
/// </summary>
public sealed class BatchUpdateExecutor
{
    private readonly IAudioMetadataWriter _writer;
    private readonly IBackupService _backupService;
    private readonly IFileProbe _probe;
    private readonly IAppLog _log;

    public BatchUpdateExecutor(
        IAudioMetadataWriter writer,
        IBackupService backupService,
        IFileProbe probe,
        IAppLog log)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<BatchExecutionResult> ExecuteAsync(
        BatchUpdateRequest request,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var versionLabel = request.WriteVersion.ToDisplayString();

        BackupManifest manifest;
        try
        {
            manifest = await CreateBackupAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"Backup failed for '{request.RootFolder}'. No file was modified.", ex);

            var blocked = request.Files
                .Select(plan => FileOutcome.FromPlan(
                    plan,
                    FileOutcomeStatus.Failed,
                    "Not modified because the metadata backup could not be created."))
                .ToList();

            return new BatchExecutionResult
            {
                Outcomes = blocked,
                WriteVersionLabel = versionLabel,
                BackupFailureMessage = $"The metadata backup could not be created: {UserFriendlyErrors.Describe(ex)}",
            };
        }

        var outcomes = new List<FileOutcome>(request.Files.Count);
        try
        {
            foreach (var plan in request.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outcome = await ProcessFileAsync(plan, request.WriteVersion, cancellationToken).ConfigureAwait(false);
                outcomes.Add(outcome);
                progress?.Report(new BatchProgress(outcomes.Count, request.Files.Count, outcome));
            }
        }
        finally
        {
            await UpdateManifestStatusesAsync(manifest, outcomes).ConfigureAwait(false);
        }

        return new BatchExecutionResult
        {
            Manifest = manifest,
            BackupFilePath = manifest.ManifestPath,
            Outcomes = outcomes,
            WriteVersionLabel = versionLabel,
        };
    }

    private async Task<BackupManifest> CreateBackupAsync(BatchUpdateRequest request, CancellationToken cancellationToken)
    {
        var items = request.Files
            .Select(plan => new BackupCaptureItem(
                plan.FilePath,
                plan.RelativePath,
                plan.CurrentMetadata,
                plan.Id3Version,
                MapStatus(plan.Status),
                plan.Message))
            .ToList();

        return await _backupService
            .CreateBackupAsync(request.RootFolder, request.Operation, items, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<FileOutcome> ProcessFileAsync(
        PlannedFileUpdate plan,
        Id3WriteVersion writeVersion,
        CancellationToken cancellationToken)
    {
        switch (plan.Status)
        {
            case PreviewStatus.Unchanged:
                return FileOutcome.FromPlan(plan, FileOutcomeStatus.Unchanged, plan.Message);
            case PreviewStatus.Skipped:
                return FileOutcome.FromPlan(plan, FileOutcomeStatus.Skipped, plan.Message);
            case PreviewStatus.Failed:
                return FileOutcome.FromPlan(plan, FileOutcomeStatus.Failed, plan.Message);
            case PreviewStatus.WillUpdate:
                break;
            default:
                return FileOutcome.FromPlan(plan, FileOutcomeStatus.Failed, "Unknown plan status.");
        }

        if (!_probe.FileExists(plan.FilePath))
        {
            return FileOutcome.FromPlan(plan, FileOutcomeStatus.Skipped, "The file no longer exists.");
        }

        if (_probe.IsFileReadOnly(plan.FilePath))
        {
            return FileOutcome.FromPlan(plan, FileOutcomeStatus.Failed, "The file is read-only.");
        }

        try
        {
            var writeResult = await _writer
                .WriteAsync(plan.FilePath, plan.Update, writeVersion, cancellationToken)
                .ConfigureAwait(false);

            if (!writeResult.Success)
            {
                return FileOutcome.FromPlan(
                    plan,
                    FileOutcomeStatus.Failed,
                    writeResult.ErrorMessage ?? "The file could not be written.");
            }

            var verification = Verify(plan, writeResult.VerifiedMetadata);
            if (verification.Error is not null)
            {
                _log.Warn($"Verification failed for '{plan.FilePath}': {verification.Error}");
                return FileOutcome.FromPlan(plan, FileOutcomeStatus.Failed, verification.Error);
            }

            if (verification.Warning is not null)
            {
                _log.Warn($"Integrity note for '{plan.FilePath}': {verification.Warning}");
            }

            return FileOutcome.FromPlan(plan, FileOutcomeStatus.Updated, verification.Warning);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to write metadata to '{plan.FilePath}'.", ex);
            return FileOutcome.FromPlan(plan, FileOutcomeStatus.Failed, UserFriendlyErrors.Describe(ex));
        }
    }

    private record VerificationOutcome(string? Error, string? Warning);

    /// <summary>
    /// Compares the values that were just stored with the expected values, and makes sure
    /// fields that were not part of the update did not disappear (SRS FR-011, NFR-002).
    /// </summary>
    private static VerificationOutcome Verify(PlannedFileUpdate plan, AudioMetadata verified)
    {
        var expected = plan.Update.ApplyTo(plan.CurrentMetadata);

        foreach (var (field, edit) in plan.Update.Edits)
        {
            if (edit.Kind == FieldEditKind.Keep)
            {
                continue;
            }

            var expectedValue = expected.GetValue(field);
            var actualValue = verified.GetValue(field);
            if (!MetadataText.AreEquivalent(expectedValue, actualValue))
            {
                return new VerificationOutcome(
                    $"Verification failed: '{MetadataFieldCatalog.DisplayName(field)}' was stored as '{FieldChange.Display(actualValue)}' " +
                    $"but '{FieldChange.Display(expectedValue)}' was requested. Restore from the backup if needed.",
                    null);
            }
        }

        var changedFields = new List<string>();
        foreach (var (field, previousValue) in plan.CurrentMetadata.EnumerateValues())
        {
            if (plan.Update.Contains(field))
            {
                continue;
            }

            var actualValue = verified.GetValue(field);
            if (!MetadataText.AreEquivalent(previousValue, actualValue))
            {
                if (previousValue is not null && actualValue is null)
                {
                    return new VerificationOutcome(
                        $"Verification failed: the field '{MetadataFieldCatalog.DisplayName(field)}' was lost during the write. " +
                        "Restore from the backup if needed.",
                        null);
                }

                changedFields.Add(MetadataFieldCatalog.DisplayName(field));
            }
        }

        return new VerificationOutcome(
            null,
            changedFields.Count == 0
                ? null
                : $"Unrelated fields were rewritten by the tag library: {string.Join(", ", changedFields)}.");
    }

    private async Task UpdateManifestStatusesAsync(BackupManifest manifest, IReadOnlyList<FileOutcome> outcomes)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var statusByPath = outcomes.ToDictionary(o => o.RelativePath, StringComparer.OrdinalIgnoreCase);

            var entries = manifest.Entries
                .Select(entry =>
                {
                    if (!statusByPath.TryGetValue(entry.RelativePath, out var outcome))
                    {
                        return entry;
                    }

                    return entry with
                    {
                        Status = MapStatus(outcome.Status),
                        Message = outcome.Message,
                        ProcessedUtc = now,
                    };
                })
                .ToList();

            await _backupService
                .SaveManifestAsync(manifest with { Entries = entries }, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"The backup manifest could not be updated with the per-file results: {UserFriendlyErrors.Describe(ex)}");
        }
    }

    private static BackupEntryStatus MapStatus(PreviewStatus status) => status switch
    {
        PreviewStatus.Unchanged => BackupEntryStatus.Unchanged,
        PreviewStatus.Skipped => BackupEntryStatus.Skipped,
        PreviewStatus.Failed => BackupEntryStatus.Failed,
        _ => BackupEntryStatus.Captured,
    };

    private static BackupEntryStatus MapStatus(FileOutcomeStatus status) => status switch
    {
        FileOutcomeStatus.Updated => BackupEntryStatus.Updated,
        FileOutcomeStatus.Unchanged => BackupEntryStatus.Unchanged,
        FileOutcomeStatus.Skipped => BackupEntryStatus.Skipped,
        _ => BackupEntryStatus.Failed,
    };
}
