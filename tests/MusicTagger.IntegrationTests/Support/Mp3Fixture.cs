using System.Text;

namespace MusicTagger.IntegrationTests.Support;

/// <summary>
/// Creates real, structurally valid MP3 files (MPEG-1 Layer III frames with silent payload)
/// so the integration tests exercise the actual tag library on actual files instead of mocks.
/// </summary>
public static class Mp3Fixture
{
    public const int FrameSize = 417;

    public static string Create(string filePath, int frameCount = 48)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var frame = BuildSilentFrame();
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        for (var i = 0; i < frameCount; i++)
        {
            stream.Write(frame);
        }

        return filePath;
    }

    /// <summary>MPEG-1 Layer III, 128 kbps, 44100 Hz, no CRC, no padding.</summary>
    private static byte[] BuildSilentFrame()
    {
        var frame = new byte[FrameSize];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0x90;
        frame[3] = 0x00;
        return frame;
    }
}

/// <summary>Disposable temporary folder used by each test.</summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace(string? name = null)
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "musictagger-tests",
            (name ?? "ws") + "-" + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Combine(string relative) => System.IO.Path.Combine(Root, relative);

    public string CreateFile(string relative, int frameCount = 48) => Mp3Fixture.Create(Combine(relative), frameCount);

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary folder cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal static class Utf8
{
    public static readonly Encoding Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
