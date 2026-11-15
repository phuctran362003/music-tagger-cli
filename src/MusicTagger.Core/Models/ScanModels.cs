namespace MusicTagger.Core.Models;

/// <summary>An audio file discovered by a folder scan (any format listed in <see cref="AudioFormatCatalog"/>).</summary>
public sealed record ScannedFile(string FilePath, string RelativePath, long SizeBytes, DateTimeOffset LastWriteTimeUtc);

public sealed record ScanResult(
    string RootFolder,
    bool Recursive,
    IReadOnlyList<ScannedFile> Files,
    IReadOnlyList<string> Issues)
{
    public int Count => Files.Count;

    public static ScanResult Empty(string rootFolder, bool recursive) =>
        new(rootFolder, recursive, [], []);
}

/// <summary>A file the user selected for editing (identity is the relative path, SRS 7.6).</summary>
public sealed record SelectedFile(string FilePath, string RelativePath);
