namespace MusicTagger.Core.Models;

/// <summary>Request handed to the batch executor. Contains only files that were planned.</summary>
public sealed record BatchUpdateRequest(
    string RootFolder,
    string Operation,
    IReadOnlyList<PlannedFileUpdate> Files,
    Id3WriteVersion WriteVersion);

public enum FileOutcomeStatus
{
    Updated = 0,
    Unchanged = 1,
    Skipped = 2,
    Failed = 3,
}

public sealed record FileOutcome(
    string FilePath,
    string RelativePath,
    FileOutcomeStatus Status,
    string? Message,
    IReadOnlyList<FieldChange> Changes)
{
    public static FileOutcome FromPlan(PlannedFileUpdate plan, FileOutcomeStatus status, string? message) =>
        new(plan.FilePath, plan.RelativePath, status, message, plan.Changes);
}

/// <summary>Progress notification for one file (used by the progress bar).</summary>
public sealed record BatchProgress(int ProcessedCount, int TotalCount, FileOutcome Outcome)
{
    public double Percentage => TotalCount <= 0 ? 100d : ProcessedCount * 100d / TotalCount;
}

public sealed record BatchExecutionResult
{
    public BackupManifest? Manifest { get; init; }

    public string? BackupFilePath { get; init; }

    public string? BackupFailureMessage { get; init; }

    public string WriteVersionLabel { get; init; } = "ID3v2.3";

    public IReadOnlyList<FileOutcome> Outcomes { get; init; } = [];

    public int UpdatedCount => Outcomes.Count(o => o.Status == FileOutcomeStatus.Updated);

    public int UnchangedCount => Outcomes.Count(o => o.Status == FileOutcomeStatus.Unchanged);

    public int SkippedCount => Outcomes.Count(o => o.Status == FileOutcomeStatus.Skipped);

    public int FailedCount => Outcomes.Count(o => o.Status == FileOutcomeStatus.Failed);

    public int ProcessedCount => Outcomes.Count;

    public bool BackupSucceeded => BackupFailureMessage is null;

    public IEnumerable<FileOutcome> Failures => Outcomes.Where(o => o.Status == FileOutcomeStatus.Failed);
}
