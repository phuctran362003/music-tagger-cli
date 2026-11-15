using System.Globalization;
using MusicTagger.Cli.Presentation;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;

namespace MusicTagger.Cli.Flows;

/// <summary>
/// Lists the metadata backups, previews a restore and executes it through the regular batch
/// pipeline, so the current metadata is backed up again before anything is written (FR-013).
/// </summary>
public sealed class RestoreFlow
{
    private readonly CliContext _context;
    private readonly FolderPrompt _folderPrompt;

    public RestoreFlow(CliContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _folderPrompt = new FolderPrompt(context);
    }

    public async Task RunAsync()
    {
        IReadOnlyList<BackupManifestInfo> backups;
        try
        {
            backups = await _context.RestoreService
                .ListBackupsAsync(_context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context.Log.Error("The backups could not be listed.", ex);
            _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
            return;
        }

        if (backups.Count == 0)
        {
            _context.Presenter.ShowInfo($"No metadata backups were found in '{_context.BackupDirectory}'.");
            _context.Presenter.ShowHint("A backup is created automatically before metadata is written.");
            return;
        }

        _context.Presenter.ShowInfo($"Backup location: {_context.BackupDirectory}");
        _context.Presenter.ShowBackups(backups);

        var options = backups.Select(FormatOption).ToList();
        var choice = _context.Input.AskChoice("Choose a backup to restore", options);
        if (choice is null)
        {
            return;
        }

        var selected = backups[choice.Value];

        var load = await _context.RestoreService
            .LoadManifestAsync(selected.ManifestPath, _context.CancellationToken)
            .ConfigureAwait(false);

        if (load.Manifest is null)
        {
            _context.Presenter.ShowError("This backup cannot be used:");
            foreach (var error in load.Errors)
            {
                _context.Presenter.ShowHint($"  - {error}");
            }

            return;
        }

        var manifest = load.Manifest;
        var root = ResolveRoot(manifest);
        if (root is null)
        {
            return;
        }

        RestorePreview preview;
        try
        {
            preview = await _context.Progress
                .RunAsync(
                    $"Preparing restore preview for {manifest.Entries.Count} file(s)",
                    manifest.Entries.Count,
                    handle => _context.RestoreService.BuildPreviewAsync(
                        manifest,
                        root,
                        new InlineProgress<int>(handle.Report),
                        _context.CancellationToken))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidManifestException ex)
        {
            _context.Log.Warn($"Restore manifest rejected: {string.Join(" ", ex.Errors)}");
            _context.Presenter.ShowError("The backup cannot be restored:");
            foreach (var error in ex.Errors)
            {
                _context.Presenter.ShowHint($"  - {error}");
            }

            return;
        }
        catch (Exception ex)
        {
            _context.Log.Error("The restore preview could not be prepared.", ex);
            _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
            return;
        }

        _context.Presenter.ShowRestorePreview(preview);

        if (!preview.HasChanges)
        {
            _context.Presenter.ShowInfo("Nothing to restore: the files already match this backup.");
            return;
        }

        var confirmed = _context.Input.AskYesNo(
            $"Restore the metadata of {preview.FilesToRestore} file(s)?",
            defaultYes: false);

        if (confirmed != true)
        {
            _context.Presenter.ShowInfo("Cancelled - no file was changed.");
            return;
        }

        BatchExecutionResult result;
        try
        {
            result = await _context.Progress
                .RunAsync(
                    $"Restoring metadata of {preview.FilesToRestore} file(s)",
                    preview.Preview.Files.Count,
                    handle => _context.RestoreService.ExecuteAsync(
                        preview,
                        _context.Settings.Id3WriteVersion,
                        new InlineProgress<BatchProgress>(progress => handle.Report(progress.ProcessedCount)),
                        _context.CancellationToken))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context.Log.Error("The restore could not be completed.", ex);
            _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
            return;
        }

        _context.Presenter.ShowResult(result);
    }

    /// <summary>
    /// The manifest may point at a folder that has been moved or renamed. Restoring into a
    /// different folder is allowed, but only after an explicit confirmation (FR-013).
    /// </summary>
    private string? ResolveRoot(BackupManifest manifest)
    {
        if (_context.Probe.DirectoryExists(manifest.RootFolder))
        {
            return manifest.RootFolder;
        }

        _context.Presenter.ShowWarning($"The original folder no longer exists: {manifest.RootFolder}");
        _context.Presenter.ShowHint("Enter the folder that holds these files now. Files outside that folder are never touched.");

        while (true)
        {
            var folder = _folderPrompt.Ask("Folder to restore into:");
            if (folder is null)
            {
                return null;
            }

            if (!PathSafety.IsInside(manifest.RootFolder, folder))
            {
                _context.Presenter.ShowWarning($"'{folder}' is outside the original root folder.");
                var accepted = _context.Input.AskYesNo(
                    "Restore into that folder anyway?",
                    defaultYes: false,
                    allowBack: true);

                if (accepted is null)
                {
                    return null;
                }

                if (accepted != true)
                {
                    continue;
                }
            }

            return folder;
        }
    }

    private static string FormatOption(BackupManifestInfo info)
    {
        var created = info.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        var files = info.EntryCount.ToString(CultureInfo.CurrentCulture);
        return $"{created} | {info.Operation} | {files} file(s)";
    }
}
