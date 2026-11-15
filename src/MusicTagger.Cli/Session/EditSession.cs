using MusicTagger.Core.Models;

namespace MusicTagger.Cli.Session;

/// <summary>
/// State of one "scan and edit" session: the folder that was scanned, the files that were
/// found and the metadata that was read from them. Nothing is persisted to disk: closing
/// the app has no side effects, and no file is written without an explicit confirmation.
/// </summary>
public sealed class EditSession
{
    private readonly List<FolderEntry> _entries;

    public EditSession(string rootFolder, bool recursive, IReadOnlyList<FolderEntry> entries, IReadOnlyList<string> issues)
    {
        RootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));
        Recursive = recursive;
        _entries = [.. entries ?? throw new ArgumentNullException(nameof(entries))];
        Issues = issues ?? [];
    }

    public string RootFolder { get; }

    public bool Recursive { get; }

    public IReadOnlyList<FolderEntry> Entries => _entries;

    public IReadOnlyList<string> Issues { get; }

    public int Count => _entries.Count;

    public IReadOnlyList<FolderEntry> ReadableEntries => _entries.Where(e => e.IsReadable).ToList();

    public FolderEntry? Find(string relativePath) =>
        _entries.FirstOrDefault(e => string.Equals(e.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Replaces one entry, e.g. after the metadata of that file was reloaded.</summary>
    public void Replace(FolderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var index = _entries.FindIndex(e =>
            string.Equals(e.RelativePath, entry.RelativePath, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            _entries[index] = entry;
            return;
        }

        _entries.Add(entry);
        _entries.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
    }

    public SelectedFile ToSelectedFile(FolderEntry entry) => new(entry.FilePath, entry.RelativePath);
}
