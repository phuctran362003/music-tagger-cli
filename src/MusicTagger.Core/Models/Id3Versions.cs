namespace MusicTagger.Core.Models;

/// <summary>ID3 tag generation that was found in a file (read side).</summary>
public enum Id3Version
{
    Unknown = 0,
    Id3v1 = 1,
    Id3v22 = 2,
    Id3v23 = 3,
    Id3v24 = 4,
}

/// <summary>ID3v2 generation that the application writes. Default is v2.3 (SRS 3.3).</summary>
public enum Id3WriteVersion
{
    V23 = 0,
    V24 = 1,
}

public static class Id3VersionExtensions
{
    public static string ToDisplayString(this Id3Version version) => version switch
    {
        Id3Version.Id3v1 => "ID3v1",
        Id3Version.Id3v22 => "ID3v2.2",
        Id3Version.Id3v23 => "ID3v2.3",
        Id3Version.Id3v24 => "ID3v2.4",
        _ => "unknown",
    };

    public static string ToDisplayString(this Id3WriteVersion version) =>
        version == Id3WriteVersion.V24 ? "ID3v2.4" : "ID3v2.3";

    /// <summary>Encoding label used in the UI and in the README so users understand the choice.</summary>
    public static string EncodingLabel(this Id3WriteVersion version) =>
        version == Id3WriteVersion.V24 ? "UTF-8 (ID3v2.4)" : "UTF-16 (ID3v2.3)";

    public static byte ToTagLibVersion(this Id3WriteVersion version) =>
        version == Id3WriteVersion.V24 ? (byte)4 : (byte)3;
}
