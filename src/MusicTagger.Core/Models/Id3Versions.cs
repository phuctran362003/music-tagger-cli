namespace MusicTagger.Core.Models;

/// <summary>ID3v2 generation that the application writes into ID3 based containers (SRS 3.3).</summary>
public enum Id3WriteVersion
{
    V23 = 0,
    V24 = 1,
}

public static class Id3WriteVersionExtensions
{
    public static string ToDisplayString(this Id3WriteVersion version) =>
        version == Id3WriteVersion.V24 ? "ID3v2.4" : "ID3v2.3";

    /// <summary>Encoding label used in the UI and in the README so users understand the choice.</summary>
    public static string EncodingLabel(this Id3WriteVersion version) =>
        version == Id3WriteVersion.V24 ? "UTF-8 (ID3v2.4)" : "UTF-16 (ID3v2.3)";

    /// <summary>
    /// Which files the setting actually changes. FLAC/OGG always use UTF-8 Vorbis comments and
    /// M4A always uses UTF-8 MP4 atoms, so the ID3 version only matters for these extensions.
    /// </summary>
    public static string ApplicabilityLabel(this Id3WriteVersion version) =>
        AudioFormatCatalog.Id3FormatDisplayList;

    public static byte ToTagLibVersion(this Id3WriteVersion version) =>
        version == Id3WriteVersion.V24 ? (byte)4 : (byte)3;
}
