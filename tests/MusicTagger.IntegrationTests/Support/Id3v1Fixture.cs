using System.Text;

namespace MusicTagger.IntegrationTests.Support;

/// <summary>Writes a plain ID3v1 tag (128 bytes at the end of the file) without any library help.</summary>
public static class Id3v1Fixture
{
    public static void WriteId3v1Tag(string filePath, string title, string artist, string album, string year = "1998", byte genre = 12)
    {
        var tag = new byte[128];
        tag[0] = (byte)'T';
        tag[1] = (byte)'A';
        tag[2] = (byte)'G';

        WriteField(tag, 3, 30, title);
        WriteField(tag, 33, 30, artist);
        WriteField(tag, 63, 30, album);
        WriteField(tag, 93, 4, year);
        tag[127] = genre;

        var bytes = File.ReadAllBytes(filePath);
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Write(tag);
    }

    private static void WriteField(byte[] tag, int offset, int length, string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        var count = Math.Min(bytes.Length, length);
        Array.Copy(bytes, 0, tag, offset, count);
    }
}
