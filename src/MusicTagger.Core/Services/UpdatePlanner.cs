using MusicTagger.Core.Contracts;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;

namespace MusicTagger.Core.Services;

/// <summary>
/// Builds the dry run (preview) for a set of selected files. Nothing here writes to disk,
/// and every file is evaluated independently so one broken file cannot hide the others
/// (SRS FR-008, 7.2).
/// </summary>
public sealed class UpdatePlanner
{
    private readonly IAudioMetadataReader _reader;
    private readonly IFileProbe _probe;
    private readonly IAppLog _log;

    public UpdatePlanner(IAudioMetadataReader reader, IFileProbe probe, IAppLog log)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<UpdatePreview> BuildPreviewAsync(
        string rootFolder,
        IReadOnlyList<SelectedFile> files,
        MetadataUpdate update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(update);

        var plans = new List<PlannedFileUpdate>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            plans.Add(await PlanFileAsync(file, update, cancellationToken).ConfigureAwait(false));
            progress?.Report(plans.Count);
        }

        return new UpdatePreview(rootFolder, files.Count, plans);
    }

    public async Task<PlannedFileUpdate> PlanFileAsync(
        SelectedFile file,
        MetadataUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(update);

        if (!_probe.FileExists(file.FilePath))
        {
            return new PlannedFileUpdate(
                file.FilePath,
                file.RelativePath,
                MetadataUpdate.Empty,
                AudioMetadata.Empty,
                [],
                PreviewStatus.Skipped,
                "The file no longer exists.");
        }

        MetadataReadResult read;
        try
        {
            read = await _reader.ReadAsync(file.FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"Could not read metadata from '{file.FilePath}'.", ex);
            read = MetadataReadResult.Failure(UserFriendlyErrors.Describe(ex));
        }

        if (!read.Success)
        {
            return new PlannedFileUpdate(
                file.FilePath,
                file.RelativePath,
                MetadataUpdate.Empty,
                AudioMetadata.Empty,
                [],
                PreviewStatus.Failed,
                read.ErrorMessage ?? "The metadata could not be read.");
        }

        if (!update.TryApply(read.Metadata, out _, out var changes))
        {
            return new PlannedFileUpdate(
                file.FilePath,
                file.RelativePath,
                update,
                read.Metadata,
                [],
                PreviewStatus.Unchanged,
                "The requested values already match this file.",
                read.TagFormat);
        }

        if (_probe.IsFileReadOnly(file.FilePath))
        {
            return new PlannedFileUpdate(
                file.FilePath,
                file.RelativePath,
                update,
                read.Metadata,
                changes,
                PreviewStatus.Failed,
                "The file is read-only.",
                read.TagFormat);
        }

        return new PlannedFileUpdate(
            file.FilePath,
            file.RelativePath,
            update,
            read.Metadata,
            changes,
            PreviewStatus.WillUpdate,
            read.WarningMessage,
            read.TagFormat);
    }
}

