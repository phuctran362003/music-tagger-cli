using System.Buffers.Binary;
using System.Security.Cryptography;

namespace MusicTagger.IntegrationTests.Support;

/// <summary>
/// Extracts the MPEG audio stream from an MP3 file (skipping the ID3v2 tag at the start,
/// the ID3v1 tag and the APEv2 tag at the end) so tests can prove the audio bytes never change
/// even though the tag bytes legitimately do.
/// </summary>
public static class AudioStream
{
    public static string HashAudio(string filePath) =>
        Convert.ToHexString(SHA256.HashData(ExtractAudio(File.ReadAllBytes(filePath))));

    public static byte[] ExtractAudio(byte[] file)
    {
        var start = 0;
        if (file.Length >= 10 && file[0] == 'I' && file[1] == 'D' && file[2] == '3')
        {
            var flags = file[5];

            // The tag size is synch-safe in ID3v2.2, v2.3 and v2.4.
            var size = (file[6] << 21) | (file[7] << 14) | (file[8] << 7) | file[9];

            start = 10 + size;
            if ((flags & 0x10) != 0)
            {
                start += 10; // footer present
            }
        }

        var end = file.Length;

        // ID3v1 tag: last 128 bytes start with "TAG".
        if (end - start >= 128 && file[end - 128] == 'T' && file[end - 127] == 'A' && file[end - 126] == 'G')
        {
            end -= 128;
        }

        // APEv2 footer: last 32 bytes start with "APETAGEX".
        if (end - start >= 32 && file[end - 32] == 'A' && file[end - 31] == 'P' && file[end - 30] == 'E')
        {
            var apeSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(end - 20, 4));
            var apeStart = end - apeSize;
            if (apeStart > start)
            {
                end = apeStart;
            }
        }

        if (end <= start)
        {
            return [];
        }

        return file[start..end];
    }
}
