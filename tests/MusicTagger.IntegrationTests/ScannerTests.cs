using MusicTagger.IntegrationTests.Support;
using Xunit;

namespace MusicTagger.IntegrationTests;

/// <summary>Folder scanning behaviour on a real file system.</summary>
public sealed class ScannerTests
{
    [Fact]
    public async Task Scan_finds_mp3_files_case_insensitively_and_ignores_other_files()
    {
        using var harness = new TestHarness("scan");
        harness.Workspace.CreateFile("a.mp3");
        harness.Workspace.CreateFile("b.MP3");
        harness.Workspace.CreateFile("c.Mp3");
        await File.WriteAllTextAsync(harness.Workspace.Combine("notes.txt"), "not music");
        await File.WriteAllTextAsync(harness.Workspace.Combine("song.mp4"), "not music");

        var result = await harness.Scanner.ScanAsync(harness.Root, recursive: false);

        Assert.Equal(3, result.Count);
        Assert.All(result.Files, file => Assert.EndsWith(".mp3", file.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Scan_is_not_recursive_by_default_and_recursive_when_asked()
    {
        using var harness = new TestHarness("scan-recursive");
        harness.Workspace.CreateFile("top.mp3");
        harness.Workspace.CreateFile("sub/deep.mp3");

        var flat = await harness.Scanner.ScanAsync(harness.Root, recursive: false);
        Assert.Single(flat.Files);

        var deep = await harness.Scanner.ScanAsync(harness.Root, recursive: true);
        Assert.Equal(2, deep.Count);
        Assert.Contains(deep.Files, file => file.RelativePath.Replace('\\', '/') == "sub/deep.mp3");
    }

    [Fact]
    public async Task Scan_returns_relative_paths_and_ignores_writer_temp_files()
    {
        using var harness = new TestHarness("scan-temp");
        harness.Workspace.CreateFile("real.mp3");
        harness.Workspace.CreateFile(".real.mttmp-0123456789abcdef.mp3");

        var result = await harness.Scanner.ScanAsync(harness.Root, recursive: false);

        Assert.Single(result.Files);
        Assert.Equal("real.mp3", result.Files[0].RelativePath);
        Assert.True(Path.IsPathRooted(result.Files[0].FilePath));
    }

    [Fact]
    public async Task Scan_does_not_follow_directory_symlinks()
    {
        using var harness = new TestHarness("scan-symlink");
        harness.Workspace.CreateFile("inside.mp3");

        var outside = new TempWorkspace("outside");
        try
        {
            outside.CreateFile("outside.mp3");
            var linkPath = harness.Workspace.Combine("link");

            try
            {
                Directory.CreateSymbolicLink(linkPath, outside.Root);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return; // Symlinks are not available for this user/platform: nothing to verify here.
            }

            var result = await harness.Scanner.ScanAsync(harness.Root, recursive: true);

            Assert.Single(result.Files);
            Assert.Equal("inside.mp3", result.Files[0].RelativePath);
        }
        finally
        {
            outside.Dispose();
        }
    }

    [Fact]
    public async Task Scan_finds_every_supported_audio_format_in_a_mixed_folder()
    {
        using var harness = new TestHarness("scan-mixed");
        harness.Workspace.CreateFile("a.mp3");
        harness.Workspace.CreateFile("b.flac", AudioFixtures.Flac());
        harness.Workspace.CreateFile("c.m4a", AudioFixtures.M4a());
        harness.Workspace.CreateFile("d.wav", AudioFixtures.Wav());
        harness.Workspace.CreateFile("e.aiff", AudioFixtures.Aiff());
        harness.Workspace.CreateFile("f.ogg", AudioFixtures.Ogg());
        harness.Workspace.CreateFile("g.opus", AudioFixtures.Opus());

        // Adjacent formats TagLib# cannot write are deliberately not offered for editing.
        await File.WriteAllTextAsync(harness.Workspace.Combine("h.wma"), "not supported");
        await File.WriteAllTextAsync(harness.Workspace.Combine("i.ape"), "not supported");
        await File.WriteAllTextAsync(harness.Workspace.Combine("clip.m4v"), "video, not music");

        var result = await harness.Scanner.ScanAsync(harness.Root, recursive: false);

        Assert.Equal(7, result.Count);
        Assert.Contains(result.Files, file => file.RelativePath == "b.flac");
        Assert.Contains(result.Files, file => file.RelativePath == "c.m4a");
        Assert.Contains(result.Files, file => file.RelativePath == "d.wav");
        Assert.Contains(result.Files, file => file.RelativePath == "e.aiff");
        Assert.Contains(result.Files, file => file.RelativePath == "f.ogg");
        Assert.Contains(result.Files, file => file.RelativePath == "g.opus");
        Assert.DoesNotContain(result.Files, file => file.RelativePath is "h.wma" or "i.ape" or "clip.m4v");
    }

    [Fact]
    public async Task Scan_throws_a_clear_error_for_a_missing_folder()
    {
        using var harness = new TestHarness("scan-missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            harness.Scanner.ScanAsync(harness.Workspace.Combine("does-not-exist"), recursive: false));
    }
}
