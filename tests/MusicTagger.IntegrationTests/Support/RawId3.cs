using System.Buffers.Binary;
using System.Text;

namespace MusicTagger.IntegrationTests.Support;

public sealed record RawId3Frame(string Id, byte Encoding, byte[] Payload);

/// <summary>
/// A deliberately independent, minimal ID3v2 reader. It does not use the tag library that the
/// application uses, so the integration tests can prove what was really written to the file
/// (version byte, text encoding byte and text value).
/// </summary>
public static class RawId3
{
    public static bool HasId3v2Tag(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return bytes.Length >= 10 && bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3';
    }

    public static int ReadTagVersion(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return HasHeader(bytes) ? bytes[3] : 0;
    }

    public static IReadOnlyList<RawId3Frame> ReadFrames(string filePath) => ReadFrames(File.ReadAllBytes(filePath));

    public static IReadOnlyList<RawId3Frame> ReadFrames(byte[] bytes)
    {
        var frames = new List<RawId3Frame>();
        if (!HasHeader(bytes))
        {
            return frames;
        }

        var majorVersion = bytes[3];
        var flags = bytes[5];
        var tagSize = ReadSynchSafe(bytes, 6);
        var offset = 10;

        if ((flags & 0x40) != 0)
        {
            // Extended header: 4 bytes of size (v2.3) or synch-safe size (v2.4) followed by data.
            var extendedSize = majorVersion >= 4
                ? ReadSynchSafe(bytes, offset)
                : BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4)) + 4;
            offset += extendedSize;
        }

        var end = 10 + tagSize;
        while (offset + 10 <= end && offset + 10 <= bytes.Length)
        {
            if (bytes[offset] == 0)
            {
                break; // padding
            }

            var id = Encoding.ASCII.GetString(bytes, offset, 4);
            var frameSize = majorVersion >= 4
                ? ReadSynchSafe(bytes, offset + 4)
                : BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 4, 4));

            if (frameSize <= 0 || offset + 10 + frameSize > bytes.Length)
            {
                break;
            }

            var payload = bytes.AsSpan(offset + 10, frameSize).ToArray();
            frames.Add(new RawId3Frame(id, payload.Length > 0 ? payload[0] : (byte)0, payload));
            offset += 10 + frameSize;
        }

        return frames;
    }

    public static RawId3Frame? FindFrame(string filePath, string id) =>
        ReadFrames(filePath).FirstOrDefault(frame => frame.Id == id);

    public static string? GetText(string filePath, string id)
    {
        var frame = FindFrame(filePath, id);
        return frame is null ? null : DecodeText(frame);
    }

    /// <summary>Decodes a text information frame payload using the encoding byte in the payload.</summary>
    public static string DecodeText(RawId3Frame frame)
    {
        var payload = frame.Payload;
        if (payload.Length <= 1)
        {
            return string.Empty;
        }

        var data = payload.AsSpan(1);
        string text = frame.Encoding switch
        {
            0 => Encoding.Latin1.GetString(data),
            1 => DecodeUtf16WithBom(data),
            2 => Encoding.BigEndianUnicode.GetString(data),
            3 => Encoding.UTF8.GetString(data),
            _ => Encoding.Latin1.GetString(data),
        };

        return text.TrimEnd('\0');
    }

    private static string DecodeUtf16WithBom(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 2)
        {
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(data[2..]);
            }

            if (data[0] == 0xFE && data[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(data[2..]);
            }
        }

        return Encoding.Unicode.GetString(data);
    }

    private static bool HasHeader(byte[] bytes) =>
        bytes.Length >= 10 && bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3';

    private static int ReadSynchSafe(byte[] bytes, int offset) =>
        (bytes[offset] << 21) | (bytes[offset + 1] << 14) | (bytes[offset + 2] << 7) | bytes[offset + 3];
}
