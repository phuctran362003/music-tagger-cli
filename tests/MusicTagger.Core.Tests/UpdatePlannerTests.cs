using MusicTagger.Core.Models;
using MusicTagger.Core.Services;
using MusicTagger.Core.Tests.Support;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class UpdatePlannerTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "planner"));

    private static SelectedFile File(string name) => new(Path.Combine(Root, name), name);

    [Fact]
    public async Task Files_with_a_real_difference_are_planned_for_update()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var selected = File("a.mp3");
        probe.ExistingFiles.Add(selected.FilePath);
        reader.MetadataByPath[selected.FilePath] = new AudioMetadata { Artist = "Old" };

        var planner = new UpdatePlanner(reader, probe, new RecordingLog());
        var preview = await planner.BuildPreviewAsync(Root, [selected], MetadataUpdate.Empty.WithSet(MetadataField.Artist, "New"));

        Assert.Equal(1, preview.FilesToUpdateCount);
        Assert.True(preview.HasChanges);
        var change = Assert.Single(preview.Files[0].Changes);
        Assert.Equal("Old", change.OldValue);
        Assert.Equal("New", change.NewValue);
    }

    [Fact]
    public async Task Files_that_already_match_are_marked_unchanged()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var selected = File("b.mp3");
        probe.ExistingFiles.Add(selected.FilePath);
        reader.MetadataByPath[selected.FilePath] = new AudioMetadata { Artist = "Same" };

        var planner = new UpdatePlanner(reader, probe, new RecordingLog());
        var preview = await planner.BuildPreviewAsync(Root, [selected], MetadataUpdate.Empty.WithSet(MetadataField.Artist, "Same"));

        Assert.Equal(1, preview.UnchangedCount);
        Assert.False(preview.HasChanges);
    }

    [Fact]
    public async Task Missing_files_are_skipped_without_calling_the_reader()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var planner = new UpdatePlanner(reader, probe, new RecordingLog());

        var preview = await planner.BuildPreviewAsync(Root, [File("gone.mp3")], MetadataUpdate.Empty.WithSet(MetadataField.Album, "x"));

        Assert.Equal(1, preview.SkippedCount);
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task Unreadable_files_are_reported_as_failed_and_do_not_stop_the_preview()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var broken = File("broken.mp3");
        var fine = File("fine.mp3");
        probe.ExistingFiles.Add(broken.FilePath);
        probe.ExistingFiles.Add(fine.FilePath);
        reader.FailingPaths.Add(broken.FilePath);

        var planner = new UpdatePlanner(reader, probe, new RecordingLog());
        var preview = await planner.BuildPreviewAsync(Root, [broken, fine], MetadataUpdate.Empty.WithSet(MetadataField.Album, "x"));

        Assert.Equal(1, preview.FailedCount);
        Assert.Equal(1, preview.FilesToUpdateCount);
    }

    [Fact]
    public async Task Read_only_files_are_reported_as_failed_before_any_write()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var selected = File("ro.mp3");
        probe.ExistingFiles.Add(selected.FilePath);
        probe.ReadOnlyFiles.Add(selected.FilePath);

        var planner = new UpdatePlanner(reader, probe, new RecordingLog());
        var preview = await planner.BuildPreviewAsync(Root, [selected], MetadataUpdate.Empty.WithSet(MetadataField.Title, "x"));

        Assert.Equal(1, preview.FailedCount);
        Assert.Contains("read-only", preview.Files[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Progress_is_reported_for_every_file()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var files = new List<SelectedFile>();
        for (var i = 0; i < 5; i++)
        {
            var selected = File($"{i}.mp3");
            probe.ExistingFiles.Add(selected.FilePath);
            files.Add(selected);
        }

        var reports = new List<int>();
        var planner = new UpdatePlanner(reader, probe, new RecordingLog());
        var preview = await planner.BuildPreviewAsync(
            Root,
            files,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, "x"),
            new SynchronousProgress<int>(reports.Add));

        Assert.Equal(5, preview.FilesToUpdateCount);
        Assert.Equal([1, 2, 3, 4, 5], reports);
    }

    [Fact]
    public async Task Field_summaries_group_old_and_new_values_per_field()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var first = File("1.mp3");
        var second = File("2.mp3");
        probe.ExistingFiles.Add(first.FilePath);
        probe.ExistingFiles.Add(second.FilePath);
        reader.MetadataByPath[first.FilePath] = new AudioMetadata { Artist = "A" };
        reader.MetadataByPath[second.FilePath] = new AudioMetadata { Artist = "B" };

        var planner = new UpdatePlanner(reader, probe, new RecordingLog());
        var preview = await planner.BuildPreviewAsync(
            Root,
            [first, second],
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, "Shared"));

        var summary = Assert.Single(preview.BuildFieldSummaries());
        Assert.Equal(MetadataField.Artist, summary.Field);
        Assert.Equal(2, summary.Transitions.Count);
        Assert.All(summary.Transitions, transition => Assert.Equal("Shared", transition.NewValue));
        Assert.All(summary.Transitions, transition => Assert.Equal(1, transition.FileCount));
    }
}
