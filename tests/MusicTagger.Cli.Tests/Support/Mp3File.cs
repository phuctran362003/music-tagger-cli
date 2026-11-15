namespace MusicTagger.Cli.Tests.Support;

/// <summary>Creates real MPEG-1 Layer III frames so the CLI tests run on actual files.</summary>
public static class Mp3File
{
    public const int FrameSize = 417;

    public static string Create(string filePath, int frameCount = 40)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var frame = new byte[FrameSize];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0x90;
        frame[3] = 0x00;

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        for (var i = 0; i < frameCount; i++)
        {
            stream.Write(frame);
        }

        return filePath;
    }
}
