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
        var body = new Markup(
            $"[bold]{Markup.Escape(AppInfo.ProductName)}[/]  [grey]v{Markup.Escape(AppInfo.Version)}[/]\n" +
            $"[grey]{Markup.Escape(AppInfo.Description)}[/]");

        _console.Write(new Panel(body)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("blue"),
            Padding = new Padding(2, 0),
            Expand = true,
        });
    }

    public void ShowInfo(string message) => Status("blue", "Info:", message);

    public void ShowSuccess(string message) => Status("green", "Success:", message);

    public void ShowWarning(string message) => Status("yellow", "Warning:", message);

    public void ShowError(string message) => Status("red", "Error:", message);

    public void ShowHint(string message) => _console.MarkupLine($"[grey]{Markup.Escape(message)}[/]");

    private void Status(string color, string label, string message) =>
        _console.MarkupLine($"[{color}]{label}[/] {Markup.Escape(message)}");

    public void ShowBlankLine() => _console.WriteLine();

    /// <summary>Compact list of the scanned files: index (used for selection), file and metadata.</summary>
    public void ShowFileTable(EditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        ShowHeading($"{session.Count} file(s)");

        var table = CreateTable(
            ("#", R),
            ("File", L),
            ("Title", L),
            ("Artist", L),
            ("Album", L),
            ("Type", L),
            ("Tag", L),
            ("Size", R));
        table.Caption = new TableTitle($"[grey]{Markup.Escape(session.RootFolder)}[/]");

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

        ShowHeading(entry.RelativePath);
        _console.MarkupLine(
            $"[grey]Format[/]  {Markup.Escape(FormatDisplay(entry.File.RelativePath))}    " +
            $"[grey]Tag[/]  {Markup.Escape(entry.TagFormat.ToDisplayString())}");

        var table = CreateTable(("Field", L), ("Current value", L));
        foreach (var descriptor in MetadataFieldCatalog.All)
        {
            table.AddRow(
                Cell(descriptor.DisplayName),
                Cell(FormatValue(entry.Metadata.GetValue(descriptor.Field))));
        }

        _console.Write(table);

        if (entry.Error is not null)
        {
            ShowError(entry.Error);
        }
    }

    /// <summary>Dry run representation before anything is written (FR-008).</summary>
    public void ShowPreview(UpdatePreview preview, string heading = "PREVIEW - no files have been changed yet")
    {
        ArgumentNullException.ThrowIfNull(preview);

        ShowHeading(heading);

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
            ShowHeading("Field changes");

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
            ShowHeading("Files that will be updated");

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
        ShowHeading("Backup to restore");

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

        ShowHeading("Completed");

        var summary = CreateTable(("Status", L), ("Files", R));
        summary.AddRow(SuccessCell("Updated"), Cell(FormatCount(result.UpdatedCount)));
        summary.AddRow(ErrorCell("Failed"), Cell(FormatCount(result.FailedCount)));
        summary.AddRow(Cell("Skipped"), Cell(FormatCount(result.SkippedCount)));
        summary.AddRow(Cell("Unchanged"), Cell(FormatCount(result.UnchangedCount)));
        summary.AddRow(Cell("Total"), Cell(FormatCount(result.ProcessedCount)));
        _console.Write(summary);

        _console.MarkupLine(
            $"[grey]Tag format used: {Markup.Escape(result.WriteVersionLabel)}. " +
            $"ID3 write version applies to {Markup.Escape(AudioFormatCatalog.Id3FormatDisplayList)}.[/]");

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
            ShowHeading("Failed files");

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
            ShowHeading("Notes");

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
        table.AddRow(Cell("Settings file"), Cell(settingsFilePath));
        table.AddRow(Cell("Log directory"), Cell(logDirectory));
        _console.Write(table);
    }

    private void ShowProblemFiles(IReadOnlyList<PlannedFileUpdate> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        ShowHeading("Files that will not be written");

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
        var table = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = Style.Parse("grey"),
            UseSafeBorder = true,
        };

        foreach (var (header, alignment) in columns)
        {
            table.AddColumn(new TableColumn($"[bold]{Markup.Escape(header)}[/]")
            {
                Padding = new Padding(1, 0),
                Alignment = alignment,
                NoWrap = header is "#" or "Type" or "Tag" or "Size" or "Files" or "Count" or "Status",
            });
        }

        return table;
    }

    private void ShowHeading(string title) =>
        _console.Write(new Rule($"[bold]{Markup.Escape(title)}[/]").LeftJustified().RuleStyle("grey"));

    private static Markup Cell(string? text)
    {
        if (string.IsNullOrEmpty(text) || text == MissingValue)
        {
            return new Markup("[grey]-[/]");
        }

        return new Markup(Markup.Escape(text));
    }

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


