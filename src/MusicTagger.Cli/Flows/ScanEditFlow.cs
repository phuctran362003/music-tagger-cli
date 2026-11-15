using MusicTagger.Cli.Presentation;
using MusicTagger.Cli.Session;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;

namespace MusicTagger.Cli.Flows;

/// <summary>
/// Scan a folder, show what was found, let the user select files and edit their metadata.
/// The flow always ends with a preview and an explicit confirmation; nothing is written
/// before that (FR-005 ... FR-012).
/// </summary>
public sealed class ScanEditFlow
{
    private readonly CliContext _context;
    private readonly FolderPrompt _folderPrompt;
    private readonly MetadataEditPrompts _prompts;

    public ScanEditFlow(CliContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _folderPrompt = new FolderPrompt(context);
        _prompts = new MetadataEditPrompts(context.Console, context.Input);
    }

    public async Task RunAsync()
    {
        var folder = _folderPrompt.Ask();
        if (folder is null)
        {
            return;
        }

        var setting = _context.Settings.RecursiveScan ? "On" : "Off";
        var recursive = _context.Input.AskYesNo(
            $"Scan sub folders too? (setting: {setting})",
            defaultYes: _context.Settings.RecursiveScan,
            allowBack: true);

        if (recursive is null)
        {
            return;
        }

        var session = await BuildSessionAsync(folder, recursive.Value).ConfigureAwait(false);
        if (session is null)
        {
            return;
        }

        _context.Presenter.ShowSuccess($"Found {session.Count} music file(s) in '{session.RootFolder}'.");

        foreach (var issue in session.Issues)
        {
            _context.Presenter.ShowWarning(issue);
        }

        if (session.ReadableEntries.Count < session.Count)
        {
            _context.Presenter.ShowWarning(
                $"{session.Count - session.ReadableEntries.Count} file(s) could not be read and cannot be edited.");
        }

        await RunFileMenuAsync(session).ConfigureAwait(false);
    }

    private async Task<EditSession?> BuildSessionAsync(string folder, bool recursive)
    {
        _context.Presenter.ShowInfo($"Scanning '{folder}' ...");

        ScanResult scan;
        try
        {
            scan = await _context.Scanner
                .ScanAsync(folder, recursive, _context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (DirectoryNotFoundException)
        {
            _context.Presenter.ShowError($"Folder not found: {folder}");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _context.Log.Error($"The folder '{folder}' could not be scanned.", ex);
            _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
            return null;
        }

        if (scan.Count == 0)
        {
            _context.Presenter.ShowWarning("No supported music files were found.");
            _context.Presenter.ShowHint($"Supported formats: {AudioFormatCatalog.DisplayList}.");
            _context.Presenter.ShowHint(recursive
                ? "Choose another folder."
                : "Choose another folder, or answer yes to sub folder scanning.");
            return null;
        }

        var entries = await _context.Progress
            .RunAsync(
                $"Reading metadata of {scan.Count} file(s)",
                scan.Count,
                handle => ReadEntriesAsync(scan, handle))
            .ConfigureAwait(false);

        return new EditSession(scan.RootFolder, scan.Recursive, entries, scan.Issues);
    }

    private async Task<IReadOnlyList<FolderEntry>> ReadEntriesAsync(ScanResult scan, ProgressHandle handle)
    {
        var entries = new List<FolderEntry>(scan.Count);

        for (var i = 0; i < scan.Files.Count; i++)
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
            var file = scan.Files[i];

            MetadataReadResult read;
            try
            {
                read = await _context.Reader
                    .ReadAsync(file.FilePath, _context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _context.Log.Error($"Could not read metadata from '{file.FilePath}'.", ex);
                read = MetadataReadResult.Failure(UserFriendlyErrors.Describe(ex));
            }

            entries.Add(read.Success
                ? new FolderEntry(file, read.Metadata, read.TagFormat, null)
                : new FolderEntry(
                    file,
                    AudioMetadata.Empty,
                    TagFormat.Unknown,
                    read.ErrorMessage ?? "The metadata could not be read."));

            if (read.WarningMessage is not null && read.Success)
            {
                _context.Log.Warn($"{file.RelativePath}: {read.WarningMessage}");
            }

            handle.Report(i + 1);
        }

        return entries;
    }

    private async Task RunFileMenuAsync(EditSession session)
    {
        while (true)
        {
            _context.Presenter.ShowFileTable(session);

            var choice = _context.Input.AskChoice(
                "Choose an operation",
                ["Edit one file", "Select multiple files", "Edit all files", "Back"],
                allowBack: true,
                showBackLine: false);

            if (choice is null)
            {
                return;
            }

            switch (choice.Value)
            {
                case 0:
                    await EditOneAsync(session).ConfigureAwait(false);
                    break;
                case 1:
                    await EditManyAsync(session).ConfigureAwait(false);
                    break;
                case 2:
                    await EditSelectionAsync(session, session.Entries, "edit-all", singleFile: false).ConfigureAwait(false);
                    break;
                default:
                    return;
            }
        }
    }

    private async Task EditOneAsync(EditSession session)
    {
        var entry = AskFileByNumber(session);
        if (entry is null)
        {
            return;
        }

        _context.Presenter.ShowFileDetails(entry);

        if (!entry.IsReadable)
        {
            _context.Presenter.ShowError("The metadata of this file could not be read, so it cannot be edited safely.");
            return;
        }

        await EditSelectionAsync(session, [entry], "edit-one", singleFile: true).ConfigureAwait(false);
    }

    private async Task EditManyAsync(EditSession session)
    {
        var options = session.Entries.Select(entry => entry.RelativePath).ToList();

        var selected = _context.Input.AskMultipleChoice("Select files", options);
        if (selected is null)
        {
            return;
        }

        var entries = selected.Select(index => session.Entries[index]).ToList();
        await EditSelectionAsync(session, entries, "edit-multiple", singleFile: false).ConfigureAwait(false);
    }

    private async Task EditSelectionAsync(
        EditSession session,
        IReadOnlyList<FolderEntry> entries,
        string operation,
        bool singleFile)
    {
        if (entries.Count == 0)
        {
            _context.Presenter.ShowInfo("No file was selected.");
            return;
        }

        var editable = entries.Where(entry => entry.IsReadable).ToList();
        if (editable.Count == 0)
        {
            _context.Presenter.ShowError("None of the selected files has readable metadata.");
            return;
        }

        if (editable.Count < entries.Count)
        {
            _context.Presenter.ShowWarning($"{entries.Count - editable.Count} unreadable file(s) are left out of this edit.");
        }

        _context.Presenter.ShowInfo($"Selected: {editable.Count} file(s).");

        MetadataUpdate? update;
        if (singleFile)
        {
            _context.Presenter.ShowHint("Press Enter to keep the current value of a field. Type b to cancel.");
            update = _prompts.AskSingleFileUpdate(editable[0].Metadata);
        }
        else
        {
            update = _prompts.AskBatchUpdate();
        }

        if (update is null)
        {
            _context.Presenter.ShowInfo("Cancelled - no file was changed.");
            return;
        }

        if (update.IsEmpty)
        {
            _context.Presenter.ShowInfo("Every field was kept, so there is nothing to write.");
            return;
        }

        var selectedFiles = editable.Select(session.ToSelectedFile).ToList();

        UpdatePreview preview;
        try
        {
            preview = await _context.Progress
                .RunAsync(
                    $"Preparing preview for {selectedFiles.Count} file(s)",
                    selectedFiles.Count,
                    handle => _context.Planner.BuildPreviewAsync(
                        session.RootFolder,
                        selectedFiles,
                        update,
                        new InlineProgress<int>(handle.Report),
                        _context.CancellationToken))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context.Log.Error("The preview could not be prepared.", ex);
            _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
            return;
        }

        _context.Presenter.ShowPreview(preview);

        if (!preview.HasChanges)
        {
            _context.Presenter.ShowInfo("Nothing to write: the requested values already match every selected file.");
            return;
        }

        var confirmed = _context.Input.AskYesNo(
            $"Apply these changes to {preview.FilesToUpdateCount} of {preview.SelectedCount} file(s)?",
            defaultYes: false);

        if (confirmed != true)
        {
            _context.Presenter.ShowInfo("Cancelled - no file was changed.");
            return;
        }

        var request = new BatchUpdateRequest(
            session.RootFolder,
            operation,
            preview.Files,
            _context.Settings.Id3WriteVersion);

        BatchExecutionResult result;
        try
        {
            result = await _context.Progress
                .RunAsync(
                    $"Writing metadata to {preview.FilesToUpdateCount} file(s)",
                    preview.Files.Count,
                    handle => _context.Executor.ExecuteAsync(
                        request,
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
            _context.Log.Error("The batch operation could not be completed.", ex);
            _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
            return;
        }

        _context.Presenter.ShowResult(result);

        await RefreshEntriesAsync(session, result).ConfigureAwait(false);
    }

    private async Task RefreshEntriesAsync(EditSession session, BatchExecutionResult result)
    {
        var updated = result.Outcomes
            .Where(outcome => outcome.Status == FileOutcomeStatus.Updated)
            .ToList();

        if (updated.Count == 0)
        {
            return;
        }

        foreach (var outcome in updated)
        {
            var entry = session.Find(outcome.RelativePath);
            if (entry is null)
            {
                continue;
            }

            try
            {
                var read = await _context.Reader
                    .ReadAsync(entry.FilePath, _context.CancellationToken)
                    .ConfigureAwait(false);

                if (read.Success)
                {
                    session.Replace(entry with
                    {
                        Metadata = read.Metadata,
                        TagFormat = read.TagFormat,
                        Error = null,
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _context.Log.Warn($"The metadata of '{entry.FilePath}' could not be reloaded after the write: {ex.Message}");
            }
        }

        _context.Presenter.ShowHint("The file list now shows the values that were read back from disk.");
    }

    private FolderEntry? AskFileByNumber(EditSession session)
    {
        while (true)
        {
            var answer = _context.Input.ReadLine($"File number (1-{session.Count}):");
            if (answer is null || _context.Input.IsBackToken(answer))
            {
                return null;
            }

            if (int.TryParse(answer, out var number) && number >= 1 && number <= session.Count)
            {
                return session.Entries[number - 1];
            }

            _context.Presenter.ShowWarning($"Enter a number between 1 and {session.Count}.");
        }
    }
}

