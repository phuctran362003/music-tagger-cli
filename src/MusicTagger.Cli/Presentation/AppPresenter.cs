using System.Globalization;
using MusicTagger.Cli.Session;
using MusicTagger.Core.Models;
using Spectre.Console;

namespace MusicTagger.Cli.Presentation;

/// <summary>
/// Every visual element of the CLI lives here so the flows only contain navigation logic.
/// Text that comes from the user (file names, metadata values) is always escaped: a title
/// containing square brackets must never be interpreted as console markup.
/// </summary>
public sealed class AppPresenter
{
    private const string MissingValue = "-";
    private const int MaxValueWidth = 38;
    private const int MaxTableRows = 300;
    private const int MaxPreviewRows = 25;

    private const Justify L = Justify.Left;
    private const Justify R = Justify.Right;

    private readonly IAnsiConsole _console;

    public AppPresenter(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public void ShowBanner()
    {
        _console.Write(new Rule().RuleStyle(Style.Parse("grey")));
        _console.Write(new Markup(
            $"[bold]{Markup.Escape(AppInfo.ProductName)}[/]\n[grey]{Markup.Escape(AppInfo.Description)}[/]\n"));
    }

    public void ShowInfo(string message) => _console.MarkupLine($"[blue]{Markup.Escape(message)}[/]");

    public void ShowSuccess(string message) => _console.MarkupLine($"[green]Success: {Markup.Escape(message)}[/]");

    public void ShowWarning(string message) => _console.MarkupLine($"[yellow]Warning: {Markup.Escape(message)}[/]");

    public void ShowError(string message) => _console.MarkupLine($"[red]Error: {Markup.Escape(message)}[/]");

    public void ShowHint(string message) => _console.MarkupLine($"[grey]{Markup.Escape(message)}[/]");

    public void ShowBlankLine() => _console.WriteLine();

    /// <summary>Compact list of the scanned files: index (used for selection), file and metadata.</summary>
    public void ShowFileTable(EditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var table = CreateTable(
            ("#", R),
            ("File", L),
            ("Title", L),
            ("Artist", L),
            ("Album", L),
            ("Type", L),
            ("Tag", L),
            ("Size", R));

        var shown = 0;
        for (var i = 0; i < session.Count && shown < MaxTableRows; i++)
        {
            var entry = session.Entries[i];
            table.AddRow(
                Cell((i + 1).ToString(CultureInfo.InvariantCulture)),
                Cell(entry.RelativePath),
                entry.IsReadable ? Cell(FormatValue(entry.Metadata.Title)) : ErrorCell("read error"),
                Cell(FormatValue(entry.Metadata.Artist)),
                Cell(FormatValue(entry.Metadata.Album)),
                Cell(FormatDisplay(entry.File.RelativePath)),
                Cell(entry.TagFormat.ToDisplayString()),
                Cell(FormatSize(entry.File.SizeBytes)));

            shown++;
        }

        _console.Write(table);

        if (session.Count > shown)
        {
            ShowHint($"Showing the first {shown} of {session.Count} files. Selection by number still works for every file.");
        }
    }

    /// <summary>Full metadata of one file (FR-004, FR-006).</summary>
    public void ShowFileDetails(FolderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var table = CreateTable(("Field", L), ("Current value", L));
        foreach (var descriptor in MetadataFieldCatalog.All)
        {
            table.AddRow(
                Cell(descriptor.DisplayName),
                Cell(FormatValue(entry.Metadata.GetValue(descriptor.Field))));
        }

        _console.Write(table);

        _console.MarkupLine($"[grey]File:[/] {Markup.Escape(entry.RelativePath)}");
        _console.MarkupLine($"[grey]Format:[/] {Markup.Escape(FormatDisplay(entry.File.RelativePath))}");
        _console.MarkupLine($"[grey]Tag:[/] {Markup.Escape(entry.TagFormat.ToDisplayString())}");

        if (entry.Error is not null)
        {
            ShowError(entry.Error);
        }
    }

    /// <summary>Dry run representation before anything is written (FR-008).</summary>
    public void ShowPreview(UpdatePreview preview, string heading = "PREVIEW - no files have been changed yet")
    {
        ArgumentNullException.ThrowIfNull(preview);

        _console.MarkupLine($"[bold]{Markup.Escape(heading)}[/]");
        ShowBlankLine();

        var summary = CreateTable(("Item", L), ("Count", R));
        summary.AddRow(Cell("Files selected"), Cell(FormatCount(preview.SelectedCount)));
        summary.AddRow(Cell("Files with changes"), Cell(FormatCount(preview.FilesToUpdateCount)));
        summary.AddRow(Cell("Unchanged (nothing to do)"), Cell(FormatCount(preview.UnchangedCount)));
        summary.AddRow(Cell("Skipped (missing)"), Cell(FormatCount(preview.SkippedCount)));
        summary.AddRow(Cell("Failed (cannot write)"), Cell(FormatCount(preview.FailedCount)));
        _console.Write(summary);

        var summaries = preview.BuildFieldSummaries();
        if (summaries.Count > 0)
        {
            ShowBlankLine();
            _console.MarkupLine("[bold]Field changes[/]");

            var fields = CreateTable(("Field", L), ("Old value", L), ("New value", L), ("Files", R));
            foreach (var summaryItem in summaries)
            {
                foreach (var transition in summaryItem.Transitions)
                {
                    fields.AddRow(
                        Cell(summaryItem.FieldName),
                        Cell(FormatValue(transition.OldValue)),
                        transition.NewValue is null ? ErrorCell("(cleared)") : Cell(FormatValue(transition.NewValue)),
                        Cell(FormatCount(transition.FileCount)));
                }
            }

            _console.Write(fields);
        }

        var updateFiles = preview.FilesToUpdate.ToList();
        if (updateFiles.Count > 0)
        {
            ShowBlankLine();
            _console.MarkupLine("[bold]Files that will be updated[/]");

            var files = CreateTable(("File", L), ("Changes", L), ("Note", L));
            foreach (var file in updateFiles.Take(MaxPreviewRows))
            {
                files.AddRow(
                    Cell(file.RelativePath),
                    Cell(FormatChanges(file.Changes)),
                    Cell(file.Message));
            }

            _console.Write(files);

            if (updateFiles.Count > MaxPreviewRows)
            {
                ShowHint($"... and {updateFiles.Count - MaxPreviewRows} more file(s).");
            }
        }

        ShowProblemFiles(preview.Files.Where(f => f.Status is PreviewStatus.Skipped or PreviewStatus.Failed).ToList());
    }

    /// <summary>Dry run of a restore (FR-013).</summary>
    public void ShowRestorePreview(RestorePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var manifest = preview.Manifest;
        _console.MarkupLine("[bold]Backup to restore[/]");

        var info = CreateTable(("Item", L), ("Value", L));
        info.AddRow(Cell("Backup created"), Cell(manifest.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)));
        info.AddRow(Cell("Operation"), Cell(manifest.Operation));
        info.AddRow(Cell("Original root folder"), Cell(manifest.RootFolder));
        info.AddRow(Cell("Restore target"), Cell(preview.RootFolder));
        info.AddRow(Cell("Entries in backup"), Cell(FormatCount(manifest.Entries.Count)));
        info.AddRow(Cell("Backup file"), Cell(manifest.ManifestPath));
        _console.Write(info);

        foreach (var warning in preview.Warnings)
        {
            ShowWarning(warning);
        }

        ShowBlankLine();
        ShowPreview(preview.Preview, "RESTORE PREVIEW - no files have been changed yet");

        if (preview.HasChanges)
        {
            ShowHint("The current metadata of these files is backed up again before the restore is written.");
        }
    }

    /// <summary>Final per-file result of a batch (FR-012).</summary>
    public void ShowResult(BatchExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _console.MarkupLine("[bold]Completed[/]");

        var summary = CreateTable(("Status", L), ("Files", R));
        summary.AddRow(SuccessCell("Updated"), Cell(FormatCount(result.UpdatedCount)));
        summary.AddRow(ErrorCell("Failed"), Cell(FormatCount(result.FailedCount)));
        summary.AddRow(Cell("Skipped"), Cell(FormatCount(result.SkippedCount)));
        summary.AddRow(Cell("Unchanged"), Cell(FormatCount(result.UnchangedCount)));
        summary.AddRow(Cell("Total"), Cell(FormatCount(result.ProcessedCount)));
        _console.Write(summary);

        _console.MarkupLine($"[grey]Tag format used:[/] {Markup.Escape(result.WriteVersionLabel)}");
        _console.MarkupLine($"[grey]ID3 write version applies to:[/] {Markup.Escape(AudioFormatCatalog.Id3FormatDisplayList)}");

        if (result.BackupFilePath is not null)
        {
            _console.MarkupLine("[grey]Backup manifest:[/]");
            _console.WriteLine($"  {result.BackupFilePath}");
        }
        else if (result.BackupFailureMessage is not null)
        {
            ShowError(result.BackupFailureMessage);
        }

        var failures = result.Failures.ToList();
        if (failures.Count > 0)
        {
            ShowBlankLine();
            _console.MarkupLine("[bold]Failed files[/]");

            var table = CreateTable(("File", L), ("Reason", L));
            foreach (var failure in failures)
            {
                table.AddRow(Cell(failure.RelativePath), Cell(failure.Message ?? "Unknown reason."));
            }

            _console.Write(table);
        }

        var notes = result.Outcomes
            .Where(outcome => outcome.Status == FileOutcomeStatus.Updated && !string.IsNullOrWhiteSpace(outcome.Message))
            .ToList();

        if (notes.Count > 0)
        {
            ShowBlankLine();
            _console.MarkupLine("[bold]Notes[/]");

            var table = CreateTable(("File", L), ("Note", L));
            foreach (var note in notes)
            {
                table.AddRow(Cell(note.RelativePath), Cell(note.Message));
            }

            _console.Write(table);
        }
    }

    public void ShowBackups(IReadOnlyList<BackupManifestInfo> backups)
    {
        ArgumentNullException.ThrowIfNull(backups);

        var table = CreateTable(("#", R), ("Created", L), ("Operation", L), ("Root folder", L), ("Files", R));

        for (var i = 0; i < backups.Count; i++)
        {
            var backup = backups[i];
            table.AddRow(
                Cell((i + 1).ToString(CultureInfo.InvariantCulture)),
                Cell(backup.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)),
                Cell(backup.Operation),
                Cell(Trim(backup.RootFolder, 60)),
                Cell(FormatCount(backup.EntryCount)));
        }

        _console.Write(table);
    }

    public void ShowSettings(
        AppSettings settings,
        int backupCount,
        string settingsFilePath,
        string backupDirectory,
        string logDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var table = CreateTable(("Setting", L), ("Value", L));
        table.AddRow(
            Cell("ID3 write version"),
            Cell($"{settings.Id3WriteVersion.ToDisplayString()} ({settings.Id3WriteVersion.EncodingLabel()}) - for {settings.Id3WriteVersion.ApplicabilityLabel()}"));
        table.AddRow(Cell("Recursive scan"), Cell(settings.RecursiveScan ? "On" : "Off"));
        table.AddRow(Cell("Backup directory"), Cell(backupDirectory));
        table.AddRow(Cell("Backups available"), Cell(FormatCount(backupCount)));
        table.AddRow(Cell("Console color"), Cell(FormatColorMode(settings.ConsoleColor)));
        _console.Write(table);

        _console.MarkupLine($"[grey]Settings file:[/] {Markup.Escape(settingsFilePath)}");
        _console.MarkupLine($"[grey]Log directory:[/] {Markup.Escape(logDirectory)}");
    }

    private void ShowProblemFiles(IReadOnlyList<PlannedFileUpdate> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        ShowBlankLine();
        _console.MarkupLine("[bold]Files that will not be written[/]");

        var table = CreateTable(("File", L), ("Status", L), ("Reason", L));
        foreach (var problem in problems)
        {
            table.AddRow(
                Cell(problem.RelativePath),
                problem.Status == PreviewStatus.Failed ? ErrorCell("Failed") : Cell("Skipped"),
                Cell(problem.Message));
        }

        _console.Write(table);
    }

    private static Table CreateTable(params (string Header, Justify Alignment)[] columns)
    {
        var table = new Table().Border(TableBorder.Rounded).BorderStyle(Style.Parse("grey"));

        foreach (var (header, alignment) in columns)
        {
            table.AddColumn(new TableColumn(new Markup($"[bold]{Markup.Escape(header)}[/]"))
            {
                Padding = new Padding(0, 1, 0, 1),
                Alignment = alignment,
            });
        }

        return table;
    }

    private static Markup Cell(string? text) => new(Markup.Escape(text ?? string.Empty));

    private static Markup SuccessCell(string text) => new($"[green]{Markup.Escape(text)}[/]");

    private static Markup ErrorCell(string text) => new($"[red]{Markup.Escape(text)}[/]");

    private static string FormatValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? MissingValue : Trim(value);

    private static string FormatDisplay(string? filePath) =>
        AudioFormatCatalog.TryGetForPath(filePath ?? string.Empty, out var format) ? format.DisplayName : "?";

    private static string FormatCount(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Create(CultureInfo.CurrentCulture, $"{bytes} B");
        }

        if (bytes < 1024 * 1024)
        {
            return string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024d:0.0} KB");
        }

        return string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024d * 1024d):0.0} MB");
    }

    private static string FormatChanges(IReadOnlyList<FieldChange> changes)
    {
        if (changes.Count == 0)
        {
            return MissingValue;
        }

        var parts = changes
            .Select(change => change.IsClear
                ? $"{change.FieldName}: {FormatValue(change.OldValue)} -> (cleared)"
                : $"{change.FieldName}: {FormatValue(change.OldValue)} -> {Trim(change.NewValue)}")
            .ToList();

        return string.Join("; ", parts);
    }

    private static string FormatColorMode(ColorMode mode) => mode switch
    {
        ColorMode.On => "On",
        ColorMode.Off => "Off",
        _ => "Auto",
    };

    private static string Trim(string? value, int maxWidth = MaxValueWidth)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var singleLine = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return singleLine.Length <= maxWidth ? singleLine : singleLine[..(maxWidth - 1)] + "...";
    }
}


