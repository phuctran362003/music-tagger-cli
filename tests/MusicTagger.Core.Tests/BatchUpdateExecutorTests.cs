using MusicTagger.Core.Models;
using MusicTagger.Core.Services;
using MusicTagger.Core.Tests.Support;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class BatchUpdateExecutorTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "batch"));

    private static SelectedFile File(string name) => new(Path.Combine(Root, name), name);

    private sealed record Setup(
        FakeFileProbe Probe,
        FakeAudioMetadataReader Reader,
        FakeAudioMetadataWriter Writer,
        FakeBackupService Backup,
        UpdatePlanner Planner,
        BatchUpdateExecutor Executor,
        RecordingLog Log);

    private static Setup CreateSetup()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var writer = new FakeAudioMetadataWriter();
        var backup = new FakeBackupService();
        var log = new RecordingLog();
        var planner = new UpdatePlanner(reader, probe, log);
        var executor = new BatchUpdateExecutor(writer, backup, probe, log);
        return new Setup(probe, reader, writer, backup, planner, executor, log);
    }

    [Fact]
    public async Task Backup_is_created_before_the_first_write_with_the_old_values()
    {
        var setup = CreateSetup();
        var first = File("1.mp3");
        var second = File("2.mp3");
        setup.Probe.ExistingFiles.Add(first.FilePath);
        setup.Probe.ExistingFiles.Add(second.FilePath);
        setup.Reader.MetadataByPath[first.FilePath] = new AudioMetadata { Artist = "Old 1" };
        setup.Reader.MetadataByPath[second.FilePath] = new AudioMetadata { Artist = "Old 2" };

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [first, second],
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, "New"));

        var result = await setup.Executor.ExecuteAsync(
            new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(2, result.UpdatedCount);
        var manifest = Assert.Single(setup.Backup.Manifests);
        Assert.Equal("edit", manifest.Operation);
        Assert.Equal(2, manifest.Entries.Count);
        Assert.Equal("Old 1", manifest.Entries[0].Metadata.Artist);
        Assert.All(setup.Writer.Writes, write => Assert.Equal(Id3WriteVersion.V23, write.Version));
    }

    [Fact]
    public async Task Nothing_is_written_when_the_backup_fails()
    {
        var setup = CreateSetup();
        var selected = File("1.mp3");
        setup.Probe.ExistingFiles.Add(selected.FilePath);
        setup.Backup.CreateFailure = new IOException("backup disk full");

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [selected],
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, "New"));

        var result = await setup.Executor.ExecuteAsync(
            new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.False(result.BackupSucceeded);
        Assert.Empty(setup.Writer.Writes);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains("backup", result.Outcomes[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unchanged_and_skipped_files_are_never_written()
    {
        var setup = CreateSetup();
        var unchanged = File("same.mp3");
        var skipped = File("missing.mp3");
        setup.Probe.ExistingFiles.Add(unchanged.FilePath);
        setup.Reader.MetadataByPath[unchanged.FilePath] = new AudioMetadata { Title = "Same" };

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [unchanged, skipped],
            MetadataUpdate.Empty.WithSet(MetadataField.Title, "Same"));

        var result = await setup.Executor.ExecuteAsync(
            new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Empty(setup.Writer.Writes);
        Assert.Equal(1, result.UnchangedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Null(result.BackupFilePath);
    }

    [Fact]
    public async Task A_failing_write_does_not_stop_the_remaining_files()
    {
        var setup = CreateSetup();
        var first = File("1.mp3");
        var failing = File("2.mp3");
        var third = File("3.mp3");
        foreach (var file in new[] { first, failing, third })
        {
            setup.Probe.ExistingFiles.Add(file.FilePath);
        }

        setup.Writer.FailingPaths.Add(failing.FilePath);

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [first, failing, third],
            MetadataUpdate.Empty.WithSet(MetadataField.Album, "Album"));

        var progress = new List<BatchProgress>();
        var result = await setup.Executor.ExecuteAsync(
            new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23),
            new SynchronousProgress<BatchProgress>(progress.Add));

        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(3, progress.Count);
        Assert.Equal(3, progress[^1].ProcessedCount);
    }

    [Fact]
    public async Task A_verification_mismatch_is_reported_as_a_failure()
    {
        var setup = CreateSetup();
        var selected = File("verify.mp3");
        setup.Probe.ExistingFiles.Add(selected.FilePath);
        setup.Writer.VerificationFailurePaths.Add(selected.FilePath);

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [selected],
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, "Expected"));

        var result = await setup.Executor.ExecuteAsync(
            new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(1, result.FailedCount);
        Assert.Contains("Verification", result.Outcomes[0].Message);
    }

    [Fact]
    public async Task Manifest_statuses_are_updated_after_the_batch()
    {
        var setup = CreateSetup();
        var first = File("1.mp3");
        var failing = File("2.mp3");
        setup.Probe.ExistingFiles.Add(first.FilePath);
        setup.Probe.ExistingFiles.Add(failing.FilePath);
        setup.Writer.FailingPaths.Add(failing.FilePath);

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [first, failing],
            MetadataUpdate.Empty.WithSet(MetadataField.Album, "Album"));

        await setup.Executor.ExecuteAsync(
            new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23));

        var manifest = Assert.Single(setup.Backup.Manifests);
        Assert.Equal(BackupEntryStatus.Updated, manifest.Entries[0].Status);
        Assert.Equal(BackupEntryStatus.Failed, manifest.Entries[1].Status);
        Assert.NotNull(manifest.Entries[0].ProcessedUtc);
    }

    [Fact]
    public async Task Cancellation_is_honoured_between_files()
    {
        var setup = CreateSetup();
        var first = File("1.mp3");
        var second = File("2.mp3");
        setup.Probe.ExistingFiles.Add(first.FilePath);
        setup.Probe.ExistingFiles.Add(second.FilePath);

        var preview = await setup.Planner.BuildPreviewAsync(
            Root,
            [first, second],
            MetadataUpdate.Empty.WithSet(MetadataField.Album, "Album"));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            setup.Executor.ExecuteAsync(
                new BatchUpdateRequest(Root, "edit", preview.Files, Id3WriteVersion.V23),
                null,
                cancellation.Token));

        Assert.Empty(setup.Writer.Writes);
    }
}
