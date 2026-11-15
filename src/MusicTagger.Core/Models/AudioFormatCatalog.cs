namespace MusicTagger.Core.Models;

/// <summary>Tag system an audio container uses when the application writes metadata.</summary>
public enum AudioTagSystem
{
    /// <summary>ID3v1/ID3v2 tags (MPEG audio, WAV, AIFF, ADTS AAC). Honors the ID3 write version setting.</summary>
    Id3 = 0,

    /// <summary>Vorbis comment blocks (FLAC, Ogg Vorbis, Ogg Opus). Always UTF-8.</summary>
    VorbisComment = 1,

    /// <summary>MP4/iTunes metadata atoms (M4A, M4B). Always UTF-8.</summary>
    Mp4Atom = 2,
}

/// <summary>One supported audio container.</summary>
/// <param name="Extension">File extension, including the leading dot.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
/// <param name="TagSystem">Tag system the application writes for this container.</param>
/// <param name="Id3VersionApplies">
/// False for containers whose tag writer always emits one fixed ID3v2 generation, so the
/// ID3 write version setting has no effect (measured, not assumed).
/// </param>
public sealed record AudioFormat(
    string Extension,
    string DisplayName,
    AudioTagSystem TagSystem,
    bool Id3VersionApplies = true)
{
    /// <summary>True when the ID3 write version setting (v2.3/v2.4) changes what gets written.</summary>
    public bool HonorsId3WriteVersion => TagSystem == AudioTagSystem.Id3 && Id3VersionApplies;
}

/// <summary>
/// The audio formats this application can read and write, keyed by file extension.
/// Everything listed here is covered by the integration test suite; unsupported files are
/// simply not offered for editing instead of being written blindly.
/// </summary>
public static class AudioFormatCatalog
{
    /// <summary>Formats proven to work end to end (read, write, verify).</summary>
    public static IReadOnlyList<AudioFormat> All { get; } =
    [
        new(".mp3", "MP3", AudioTagSystem.Id3),
        new(".flac", "FLAC", AudioTagSystem.VorbisComment),
        new(".m4a", "M4A", AudioTagSystem.Mp4Atom),
        new(".m4b", "M4B", AudioTagSystem.Mp4Atom),
        new(".aac", "AAC", AudioTagSystem.Id3),
        new(".ogg", "OGG Vorbis", AudioTagSystem.VorbisComment),
        new(".oga", "OGG Vorbis", AudioTagSystem.VorbisComment),
        new(".opus", "Opus", AudioTagSystem.VorbisComment),

        // The tag library always renders an ID3v2.4 tag inside a WAV "id3 " chunk, whatever
        // version is requested (measured with TagLib# 2.3.0), so the setting cannot apply here.
        new(".wav", "WAV", AudioTagSystem.Id3, Id3VersionApplies: false),
        new(".aiff", "AIFF", AudioTagSystem.Id3),
        new(".aif", "AIFF", AudioTagSystem.Id3),
    ];

    private static readonly Dictionary<string, AudioFormat> ByExtension =
        All.ToDictionary(format => format.Extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>Distinct extensions in catalog order, e.g. <c>.mp3, .flac, ...</c>.</summary>
    public static IReadOnlyList<string> Extensions { get; } =
        All.Select(format => format.Extension).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Human readable list for UI messages, e.g. <c>MP3, FLAC, M4A</c>.</summary>
    public static string DisplayList { get; } =
        string.Join(", ", All.Select(format => format.DisplayName).Distinct(StringComparer.Ordinal));

    /// <summary>Formats whose tags follow the ID3 write version setting.</summary>
    public static string Id3FormatDisplayList { get; } =
        string.Join(
            ", ",
            All.Where(format => format.HonorsId3WriteVersion)
                .Select(format => format.DisplayName)
                .Distinct(StringComparer.Ordinal));

    /// <summary>
    /// Formats that store a fixed ID3v2.4 tag, so the ID3 write version setting does not apply
    /// to them. Empty when every ID3 container honors the setting.
    /// </summary>
    public static string FixedId3v24DisplayList { get; } =
        string.Join(
            ", ",
            All.Where(format => format.TagSystem == AudioTagSystem.Id3 && !format.HonorsId3WriteVersion)
                .Select(format => format.DisplayName)
                .Distinct(StringComparer.Ordinal));

    /// <summary>True when the container keeps its metadata in RIFF style chunks (WAV, AIFF).</summary>
    public static bool UsesRiffChunks(AudioFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        return format.Extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || format.Extension.Equals(".aiff", StringComparison.OrdinalIgnoreCase)
            || format.Extension.Equals(".aif", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSupportedExtension(string extension) =>
        !string.IsNullOrWhiteSpace(extension) && ByExtension.ContainsKey(extension);

    public static bool IsSupportedPath(string path) =>
        !string.IsNullOrWhiteSpace(path) && IsSupportedExtension(Path.GetExtension(path));

    public static bool TryGet(string extension, out AudioFormat format)
    {
        if (!string.IsNullOrWhiteSpace(extension) && ByExtension.TryGetValue(extension, out var found))
        {
            format = found;
            return true;
        }

        format = null!;
        return false;
    }

    public static bool TryGetForPath(string path, out AudioFormat format) =>
        TryGet(string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetExtension(path), out format);
}
