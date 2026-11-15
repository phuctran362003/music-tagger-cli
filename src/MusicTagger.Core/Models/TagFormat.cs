namespace MusicTagger.Core.Models;

/// <summary>
/// Tag system that was found inside a file (read side). The value describes what is really on
/// disk, not what the application would write.
/// </summary>
public enum TagFormat
{
    /// <summary>The tag system is not known (unsupported or unreadable tag).</summary>
    Unknown = 0,

    /// <summary>The file has no tag at all.</summary>
    NoTag = 1,

    Id3v1 = 2,
    Id3v22 = 3,
    Id3v23 = 4,
    Id3v24 = 5,

    /// <summary>Vorbis comment (FLAC, Ogg Vorbis, Ogg Opus, Ogg FLAC).</summary>
    VorbisComment = 6,

    /// <summary>MP4/iTunes metadata atoms (M4A, M4B).</summary>
    Mp4Atom = 7,

    /// <summary>RIFF INFO chunk (WAV files written by other tools).</summary>
    RiffInfo = 8,
}

public static class TagFormatExtensions
{
    public static string ToDisplayString(this TagFormat format) => format switch
    {
        TagFormat.NoTag => "none",
        TagFormat.Id3v1 => "ID3v1",
        TagFormat.Id3v22 => "ID3v2.2",
        TagFormat.Id3v23 => "ID3v2.3",
        TagFormat.Id3v24 => "ID3v2.4",
        TagFormat.VorbisComment => "Vorbis comment",
        TagFormat.Mp4Atom => "MP4 atoms",
        TagFormat.RiffInfo => "RIFF INFO",
        _ => "unknown",
    };
}
