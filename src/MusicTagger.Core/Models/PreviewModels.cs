namespace MusicTagger.Core.Models;

public enum PreviewStatus
{
    /// <summary>The file will be written because at least one field really changes.</summary>
    WillUpdate = 0,

    /// <summary>The requested values already match the file; it will not be touched.</summary>
    Unchanged = 1,

    /// <summary>The file is not usable (missing, unreadable) and is skipped.</summary>
    Skipped = 2,

    /// <summary>The file cannot be written safely (read-only, corrupt, access denied).</summary>
    Failed = 3,
}

/// <summary>Per file plan produced by the preview/dry run step.</summary>
public sealed record PlannedFileUpdate(
    string FilePath,
    string RelativePath,
    MetadataUpdate Update,
    AudioMetadata CurrentMetadata,
    IReadOnlyList<FieldChange> Changes,
    PreviewStatus Status,
    string? Message,
    Id3Version Id3Version = Id3Version.Unknown)
{
    public bool WillWrite => Status == PreviewStatus.WillUpdate;
}

public sealed record FieldValueTransition(string? OldValue, string? NewValue, int FileCount);

public sealed record FieldChangeSummary(MetadataField Field, IReadOnlyList<FieldValueTransition> Transitions)
{
    public string FieldName => MetadataFieldCatalog.DisplayName(Field);
}

/// <summary>Dry run result: nothing has been written when this object exists.</summary>
public sealed record UpdatePreview(string RootFolder, int SelectedCount, IReadOnlyList<PlannedFileUpdate> Files)
{
    public IEnumerable<PlannedFileUpdate> FilesToUpdate => Files.Where(f => f.Status == PreviewStatus.WillUpdate);

    public int FilesToUpdateCount => Files.Count(f => f.Status == PreviewStatus.WillUpdate);

    public int UnchangedCount => Files.Count(f => f.Status == PreviewStatus.Unchanged);

    public int SkippedCount => Files.Count(f => f.Status == PreviewStatus.Skipped);

    public int FailedCount => Files.Count(f => f.Status == PreviewStatus.Failed);

    public bool HasChanges => FilesToUpdateCount > 0;

    public IReadOnlyList<FieldChangeSummary> BuildFieldSummaries()
    {
        var summaries = new List<FieldChangeSummary>();
        foreach (var descriptor in MetadataFieldCatalog.All)
        {
            var transitions = new Dictionary<(string? OldValue, string? NewValue), int>();
            foreach (var file in Files)
            {
                if (file.Status != PreviewStatus.WillUpdate)
                {
                    continue;
                }

                foreach (var change in file.Changes)
                {
                    if (change.Field != descriptor.Field)
                    {
                        continue;
                    }

                    var key = (change.OldValue, change.NewValue);
                    transitions[key] = transitions.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }

            if (transitions.Count == 0)
            {
                continue;
            }

            var list = transitions
                .Select(pair => new FieldValueTransition(pair.Key.OldValue, pair.Key.NewValue, pair.Value))
                .OrderByDescending(t => t.FileCount)
                .ThenBy(t => t.NewValue, StringComparer.Ordinal)
                .ToList();

            summaries.Add(new FieldChangeSummary(descriptor.Field, list));
        }

        return summaries;
    }
}
