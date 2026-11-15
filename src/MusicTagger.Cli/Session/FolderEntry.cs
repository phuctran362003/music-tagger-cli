using MusicTagger.Core.Models;

namespace MusicTagger.Cli.Session;

/// <summary>
/// One scanned audio file together with the metadata that was read from it. An entry whose
/// <see cref="Error"/> is set could not be read; it stays visible in the list (so the user
/// knows about it) but is never accepted as a write target.
/// </summary>
public sealed record FolderEntry(ScannedFile File, AudioMetadata Metadata, TagFormat TagFormat, string? Error)
{
    public string RelativePath => File.RelativePath;

    public string FilePath => File.FilePath;

    public bool IsReadable => Error is null;
}

