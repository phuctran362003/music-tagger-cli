using MusicTagger.Core.Models;
using MusicTagger.Core.Services;
using MusicTagger.IntegrationTests.Support;
using Xunit;

namespace MusicTagger.IntegrationTests;

/// <summary>Batch (multi/all) behaviour on real files: only selected fields change, failures do not stop the batch.</summary>
public sealed class BatchEditingTests
{
    [Fact]
    public async Task Batch_applies_shared_values_and_keeps_the_other_fields_per_file()
    {
        using var harness = new TestHarness("batch");
        var files = new List<SelectedFile>
        {
            harness.Select("album/01.mp3"),
            harness.Select("album/02.mp3"),
            harness.Select("album/03.mp3"),
        };

        harness.Workspace.CreateFile("album/01.mp3");
        harness.Workspace.CreateFile("album/02.mp3");
        harness.Workspace.CreateFile("album/03.mp3");

        // Different existing metadata per file; only Artist/Album will be shared.
        var titles = new[] { "One", "Two", "Three" };
        for (var i = 0; i < files.Count; i++)
        {
            var setup = await harness.Writer.WriteAsync(
                files[i].FilePath,
                MetadataUpdate.Empty
                    .WithSet(MetadataField.Title, titles[i])
                    .WithSet(MetadataField.Genre, "Bolero"),
                Id3WriteVersion.V23);
            Assert.True(setup.Success, setup.ErrorMessage);
        }

        var update = MetadataUpdate.Empty
            .WithSet(MetadataField.Artist, "Trịnh Công Sơn")
            .WithSet(MetadataField.Album, "Những Bản Tình Ca");

        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, files, update);
        Assert.Equal(3, preview.FilesToUpdateCount);
        Assert.True(preview.HasChanges);

        var result = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(3, result.UpdatedCount);
        Assert.Equal(0, result.FailedCount);

        for (var i = 0; i < files.Count; i++)
        {
            var read = await harness.Reader.ReadAsync(files[i].FilePath);
            Assert.True(read.Success, read.ErrorMessage);
            Assert.Equal("Trịnh Công Sơn", read.Metadata.Artist);
            Assert.Equal("Những Bản Tình Ca", read.Metadata.Album);
            Assert.Equal(titles[i], read.Metadata.Title);   // untouched field kept per file
            Assert.Equal("Bolero", read.Metadata.Genre);    // untouched field kept per file
        }
    }

    [Fact]
    public async Task Batch_does_not_rewrite_files_that_already_have_the_requested_values()
    {
        using var harness = new TestHarness("unchanged");
        var selected = harness.Select("same.mp3");
        harness.Workspace.CreateFile("same.mp3");

        var setup = await harness.Writer.WriteAsync(
            selected.FilePath,
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, "Trịnh Công Sơn"),
            Id3WriteVersion.V23);
        Assert.True(setup.Success, setup.ErrorMessage);

        var bytesBefore = await File.ReadAllBytesAsync(selected.FilePath);

        var preview = await harness.Planner.BuildPreviewAsync(
            harness.Root,
            [selected],
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, "Trịnh Công Sơn"));

        Assert.Equal(1, preview.UnchangedCount);
        Assert.False(preview.HasChanges);

        var result = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(1, result.UnchangedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Null(result.BackupFilePath); // no changes, so no backup and no write
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(selected.FilePath));
    }

    [Fact]
    public async Task Batch_continues_after_one_file_fails()
    {
        using var harness = new TestHarness("mixed");
        var good1 = harness.Select("01.mp3");
        var bad = harness.Select("02.mp3");
        var good2 = harness.Select("03.mp3");

        harness.Workspace.CreateFile("01.mp3");
        harness.Workspace.CreateFile("02.mp3");
        harness.Workspace.CreateFile("03.mp3");
        MakeReadOnly(bad.FilePath);

        var update = MetadataUpdate.Empty.WithSet(MetadataField.Album, "Sơn Ca 7");
        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [good1, bad, good2], update);

        Assert.Equal(2, preview.FilesToUpdateCount);
        Assert.Equal(1, preview.FailedCount);

        var result = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.Failures, outcome => outcome.RelativePath.EndsWith("02.mp3", StringComparison.Ordinal));
        Assert.Contains("read-only", result.Failures.First().Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("Sơn Ca 7", (await harness.Reader.ReadAsync(good1.FilePath)).Metadata.Album);
        Assert.Equal("Sơn Ca 7", (await harness.Reader.ReadAsync(good2.FilePath)).Metadata.Album);
    }

    [Fact]
    public async Task Batch_marks_a_file_deleted_after_the_scan_as_skipped()
    {
        using var harness = new TestHarness("deleted");
        var kept = harness.Select("kept.mp3");
        var removed = harness.Select("removed.mp3");

        harness.Workspace.CreateFile("kept.mp3");
        harness.Workspace.CreateFile("removed.mp3");

        var update = MetadataUpdate.Empty.WithSet(MetadataField.Album, "Album X");
        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [kept, removed], update);
        Assert.Equal(2, preview.FilesToUpdateCount);

        File.Delete(removed.FilePath);

        var result = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Outcomes, outcome => outcome.Status == FileOutcomeStatus.Skipped);
    }

    [Fact]
    public async Task Batch_marks_an_unreadable_file_as_failed_but_still_writes_the_others()
    {
        using var harness = new TestHarness("corrupt-batch");
        var good = harness.Select("good.mp3");
        var corrupt = harness.Select("broken.mp3");

        harness.Workspace.CreateFile("good.mp3");
        await File.WriteAllBytesAsync(corrupt.FilePath, "not an mp3"u8.ToArray());

        var update = MetadataUpdate.Empty.WithSet(MetadataField.Album, "Album Y");
        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [good, corrupt], update);

        Assert.Equal(1, preview.FilesToUpdateCount);
        Assert.Equal(1, preview.FailedCount);

        var result = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(RawId3.HasId3v2Tag(corrupt.FilePath));
    }

    [Fact]
    public async Task Batch_never_writes_anything_when_the_backup_fails()
    {
        using var harness = new TestHarness("backup-fail");
        var first = harness.Select("a.mp3");
        var second = harness.Select("b.mp3");
        harness.Workspace.CreateFile("a.mp3");
        harness.Workspace.CreateFile("b.mp3");

        var bytesFirst = await File.ReadAllBytesAsync(first.FilePath);
        var bytesSecond = await File.ReadAllBytesAsync(second.FilePath);

        var update = MetadataUpdate.Empty.WithSet(MetadataField.Album, "Should stay unwritten");
        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [first, second], update);

        var executor = new BatchUpdateExecutor(
            harness.Writer,
            new FailingBackupService(),
            harness.Probe,
            harness.Log);

        var result = await executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.False(result.BackupSucceeded);
        Assert.NotNull(result.BackupFailureMessage);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(2, result.FailedCount);
        Assert.Equal(bytesFirst, await File.ReadAllBytesAsync(first.FilePath));
        Assert.Equal(bytesSecond, await File.ReadAllBytesAsync(second.FilePath));
        Assert.False(RawId3.HasId3v2Tag(first.FilePath));
    }

    [Fact]
    public async Task Cancelling_before_the_first_write_leaves_every_file_unchanged()
    {
        using var harness = new TestHarness("cancel");
        var selected = harness.Select("c.mp3");
        harness.Workspace.CreateFile("c.mp3");
        var bytesBefore = await File.ReadAllBytesAsync(selected.FilePath);

        var preview = await harness.Planner.BuildPreviewAsync(
            harness.Root,
            [selected],
            MetadataUpdate.Empty.WithSet(MetadataField.Title, "Never written"));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Executor.ExecuteAsync(
                new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23),
                progress: null,
                cancellationToken: cancellation.Token));

        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(selected.FilePath));
    }

    [Fact]
    public async Task Preview_alone_never_modifies_a_file()
    {
        using var harness = new TestHarness("preview-only");
        var selected = harness.Select("p.mp3");
        harness.Workspace.CreateFile("p.mp3");
        var bytesBefore = await File.ReadAllBytesAsync(selected.FilePath);
        var stampBefore = File.GetLastWriteTimeUtc(selected.FilePath);

        var preview = await harness.Planner.BuildPreviewAsync(
            harness.Root,
            [selected],
            MetadataUpdate.Empty
                .WithSet(MetadataField.Title, "Preview only")
                .WithSet(MetadataField.Artist, "Preview only"));

        Assert.Equal(1, preview.FilesToUpdateCount);
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(selected.FilePath));
        Assert.Equal(stampBefore, File.GetLastWriteTimeUtc(selected.FilePath));
    }

    private static void MakeReadOnly(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
            return;
        }

        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }
}
