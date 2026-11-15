using MusicTagger.Cli.Tests.Support;
using MusicTagger.Core.Models;
using MusicTagger.Infrastructure.AudioTags;
using MusicTagger.Infrastructure.Logging;
using Xunit;

namespace MusicTagger.Cli.Tests;

/// <summary>
/// End to end tests of the CLI flows: scripted answers in, real MP3 files out. They prove that
/// the scan, preview, confirmation, write, read back and backup steps are actually connected.
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class FlowTests
{
    private const string VietnameseArtist = "Trịnh Công Sơn";

    [Fact]
    public async Task BatchEdit_WritesTagAndCreatesBackupBeforeWriting()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        Mp3File.Create(Path.Combine(music, "01 - Nhớ mưa phố Huế.mp3"));
        Mp3File.Create(Path.Combine(music, "02 - Trở về.mp3"));
        var backups = workspace.Subdirectory("backups");

        using var console = new TestConsole(
        [
            "1",            // scan and edit
            music,
            "n",            // no sub folders
            "3",            // edit all files
            "2",            // Artist
            "s",            // set
            VietnameseArtist,
            "y",            // confirm
            "b",            // back to the main menu
            "4",            // exit
        ]);

        await CliRunner.RunAsync(console.Ansi, new AppSettings { BackupDirectory = backups }, workspace);

        Assert.Contains("PREVIEW - no files have been changed yet", console.Output, StringComparison.Ordinal);
        Assert.Contains("Completed", console.Output, StringComparison.Ordinal);
        Assert.Contains(VietnameseArtist, console.Output, StringComparison.Ordinal);

        var reader = new TagLibAudioMetadataReader(new NullAppLog());
        var first = await reader.ReadAsync(Path.Combine(music, "01 - Nhớ mưa phố Huế.mp3"));
        var second = await reader.ReadAsync(Path.Combine(music, "02 - Trở về.mp3"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(VietnameseArtist, first.Metadata.Artist);
        Assert.Equal(VietnameseArtist, second.Metadata.Artist);
        Assert.Equal(TagFormat.Id3v23, first.TagFormat);

        var manifests = Directory.GetFiles(backups, "*.json");
        Assert.Single(manifests);

        // The manifest describes the state before the write, so the artist is still empty there.
        var manifest = await File.ReadAllTextAsync(manifests[0]);
        Assert.Contains("\"operation\": \"edit-all\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"artist\": null", manifest, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"updated\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchEdit_WithUnchangedValues_ReportsNothingToWrite()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        Mp3File.Create(Path.Combine(music, "01.mp3"));
        var backups = workspace.Subdirectory("backups");

        var settings = new AppSettings { BackupDirectory = backups };

        using (var firstRun = new TestConsole(["1", music, "n", "3", "2", "s", VietnameseArtist, "y", "b", "4"]))
        {
            await CliRunner.RunAsync(firstRun.Ansi, settings, workspace);
        }

        using (var secondRun = new TestConsole(["1", music, "n", "3", "2", "s", VietnameseArtist, "y", "b", "4"]))
        {
            await CliRunner.RunAsync(secondRun.Ansi, settings, workspace);

            Assert.Contains("Nothing to write", secondRun.Output, StringComparison.Ordinal);
        }

        // The second run changed nothing, so it must not have created a second backup.
        Assert.Single(Directory.GetFiles(backups, "*.json"));
    }

    [Fact]
    public async Task DecliningTheConfirmation_WritesNothingAndCreatesNoBackup()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        var file = Mp3File.Create(Path.Combine(music, "01.mp3"));
        var backups = workspace.Subdirectory("backups");

        using var console = new TestConsole(["1", music, "n", "3", "2", "s", VietnameseArtist, "n", "b", "4"]);

        await CliRunner.RunAsync(console.Ansi, new AppSettings { BackupDirectory = backups }, workspace);

        Assert.Contains("Cancelled - no file was changed.", console.Output, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(backups, "*.json"));

        var reader = new TagLibAudioMetadataReader(new NullAppLog());
        var read = await reader.ReadAsync(file);
        Assert.True(read.Success);
        Assert.Null(read.Metadata.Artist);
        Assert.Equal(TagFormat.NoTag, read.TagFormat);
    }

    [Fact]
    public async Task Restore_BringsBackTheBackedUpMetadata()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        var file = Mp3File.Create(Path.Combine(music, "01.mp3"));
        var backups = workspace.Subdirectory("backups");
        var settings = new AppSettings { BackupDirectory = backups };

        using (var editRun = new TestConsole(["1", music, "n", "3", "2", "s", VietnameseArtist, "y", "b", "4"]))
        {
            await CliRunner.RunAsync(editRun.Ansi, settings, workspace);
        }

        var reader = new TagLibAudioMetadataReader(new NullAppLog());
        Assert.Equal(VietnameseArtist, (await reader.ReadAsync(file)).Metadata.Artist);

        using (var restoreRun = new TestConsole(["2", "1", "y", "4"]))
        {
            await CliRunner.RunAsync(restoreRun.Ansi, settings, workspace);

            Assert.Contains("RESTORE PREVIEW - no files have been changed yet", restoreRun.Output, StringComparison.Ordinal);
            Assert.Contains("Completed", restoreRun.Output, StringComparison.Ordinal);
        }

        var restored = await reader.ReadAsync(file);
        Assert.True(restored.Success);
        Assert.Null(restored.Metadata.Artist);

        // The restore itself is backed up again, so it can be undone.
        Assert.Equal(2, Directory.GetFiles(backups, "*.json").Length);
    }

    [Fact]
    public async Task FolderWithoutMp3Files_IsReportedAndReturnsToTheMenu()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        await File.WriteAllTextAsync(Path.Combine(music, "readme.txt"), "no music here");

        using var console = new TestConsole(["1", music, "n", "4"]);

        await CliRunner.RunAsync(console.Ansi, new AppSettings { BackupDirectory = workspace.Subdirectory("backups") }, workspace);

        Assert.Contains("No supported music files were found", console.Output, StringComparison.Ordinal);
        Assert.Contains("Goodbye", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchEdit_WorksOnNonMp3FilesInTheSameFolder()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        var mp3 = Mp3File.Create(Path.Combine(music, "01.mp3"));
        var flac = FlacFile.Create(Path.Combine(music, "02.flac"));
        var backups = workspace.Subdirectory("backups");

        using var console = new TestConsole(
        [
            "1",            // scan and edit
            music,
            "n",            // no sub folders
            "3",            // edit all files
            "2",            // Artist
            "s",            // set
            VietnameseArtist,
            "y",            // confirm
            "b",            // back to the main menu
            "4",            // exit
        ]);

        await CliRunner.RunAsync(console.Ansi, new AppSettings { BackupDirectory = backups }, workspace);

        // The list shows the container and the tag system that will be used for each file.
        Assert.Contains("FLAC", console.Output, StringComparison.Ordinal);
        Assert.Contains("Vorbis comment", console.Output, StringComparison.Ordinal);
        Assert.Contains("Found 2 music file(s)", console.Output, StringComparison.Ordinal);

        var reader = new TagLibAudioMetadataReader(new NullAppLog());
        var mp3Read = await reader.ReadAsync(mp3);
        var flacRead = await reader.ReadAsync(flac);

        Assert.True(mp3Read.Success, mp3Read.ErrorMessage);
        Assert.True(flacRead.Success, flacRead.ErrorMessage);
        Assert.Equal(VietnameseArtist, mp3Read.Metadata.Artist);
        Assert.Equal(VietnameseArtist, flacRead.Metadata.Artist);
        Assert.Equal(TagFormat.Id3v23, mp3Read.TagFormat);
        Assert.Equal(TagFormat.VorbisComment, flacRead.TagFormat);

        Assert.Single(Directory.GetFiles(backups, "*.json"));
    }

    [Fact]
    public async Task SubFoldersAreOnlyScannedWhenTheUserAsksForIt()
    {
        using var workspace = new TempWorkspace();
        var music = workspace.Subdirectory("music");
        Mp3File.Create(Path.Combine(music, "01.mp3"));
        Mp3File.Create(Path.Combine(music, "Sub", "02.mp3"));

        using var console = new TestConsole(["1", music, "y", "4", "4"]);

        await CliRunner.RunAsync(console.Ansi, new AppSettings { BackupDirectory = workspace.Subdirectory("backups") }, workspace);

        Assert.Contains("Found 2 music file(s)", console.Output, StringComparison.Ordinal);
    }
}

