namespace MusicTagger.Cli.Tests.Support;

/// <summary>
/// Creates small but structurally valid FLAC files (STREAMINFO plus an audio payload), so the
/// CLI flows can be exercised on a non MP3 container as well.
/// </summary>
public static class FlacFile
{
    public static string Create(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(filePath, Bytes());
        return filePath;
    }

    private static byte[] Bytes()
    {
        const int sampleRate = 44100;
        const int totalSamples = 4410;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("fLaC"u8.ToArray());

        // STREAMINFO is 34 bytes and is the only mandatory metadata block.
        writer.Write((byte)0x80);
        writer.Write((byte)0x00);
        writer.Write((byte)0x00);
        writer.Write((byte)0x22);
        writer.Write((ushort)4096);
        writer.Write((ushort)4096);
        writer.Write(new byte[6]);

        var packed = ((ulong)sampleRate << 44) | (15UL << 36) | totalSamples;
        for (var i = 7; i >= 0; i--)
        {
            writer.Write((byte)((packed >> (i * 8)) & 0xFF));
        }

        writer.Write(new byte[16]);
        writer.Write(Enumerable.Range(0, 256).Select(i => (byte)(0x30 + (i % 100))).ToArray());
        writer.Flush();

        return stream.ToArray();
    }
}
