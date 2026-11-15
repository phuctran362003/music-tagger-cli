using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;
using MusicTagger.Core.Services;
using MusicTagger.Core.Tests.Support;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class RestoreServiceTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "restore"));

    private sealed record Setup(
        FakeFileProbe Probe,
        FakeAudioMetadataReader Reader,
        FakeAudioMetadataWriter Writer,
        FakeBackupService Backup,
        RestoreService Restore);

    private static Setup CreateSetup()
    {
        var probe = new FakeFileProbe();
        var reader = new FakeAudioMetadataReader();
        var writer = new FakeAudioMetadataWriter();
        var backup = new FakeBackupService();
        var log = new RecordingLog();
        var executor = new BatchUpdateExecutor(writer, backup, probe, log);
        var restore = new RestoreService(reader, backup, executor, probe, log);
        return new Setup(probe, reader, writer, backup, restore);
    }

    private static BackupManifest Manifest(params BackupManifestEntry[] entries) => new()
    {
        ManifestId = Guid.NewGuid().ToString("N"),
        RootFolder = Root,
        Operation = "edit",
        CreatedUtc = DateTimeOffset.UtcNow,
        ManifestPath = Path.Combine(Root, "backup.json"),
        Entries = entries,
    };

    [Fact]
    public void Restore_update_is_null_when_the_values_already_match()
    {
        var current = new AudioMetadata { Title = "T", Artist = "A" };

        Assert.Null(RestoreService.BuildRestoreUpdate(current, current));
    }

    [Fact]
    public void Restore_update_sets_changed_values_and_clears_values_missing_from_the_backup()
    {
        var target = new AudioMetadata { Title = "Title", Artist = null };
        var current = new AudioMetadata { Title = "Other", Artist = "Artist" };

        var update = RestoreService.BuildRestoreUpdate(target, current);

        Assert.NotNull(update);
        Assert.Equal(FieldEditKind.Set, update!.Edits[MetadataField.Title].Kind);
        Assert.Equal("Title", update.Edits[MetadataField.Title].Value);
        Assert.Equal(FieldEditKind.Clear, update.Edits[MetadataField.Artist].Kind);
    }

    [Fact]
    public async Task Missing_files_are_skipped_during_the_restore_preview()
    {
        var setup = CreateSetup();
        setup.Probe.ExistingDirectories.Add(Root);
        var manifest = Manifest(
            new BackupManifestEntry { RelativePath = "missing.mp3", Metadata = new AudioMetadata { Title = "T" } });

        var preview = await setup.Restore.BuildPreviewAsync(manifest);

        Assert.Equal(1, preview.Preview.SkippedCount);
        Assert.False(preview.HasChanges);
    }

    [Fact]
    public async Task Unsafe_manifest_paths_are_rejected_before_any_restore()
    {
        var setup = CreateSetup();
        setup.Probe.ExistingDirectories.Add(Root);
        var manifest = Manifest(
            new BackupManifestEntry { RelativePath = "../../escape.mp3", Metadata = new AudioMetadata { Title = "T" } });

        await Assert.ThrowsAsync<InvalidManifestException>(() => setup.Restore.BuildPreviewAsync(manifest));
    }

    [Fact]
    public async Task Restore_writes_the_backup_values_and_creates_a_new_backup_first()
    {
        var setup = CreateSetup();
        setup.Probe.ExistingDirectories.Add(Root);
        var fullPath = Path.Combine(Root, "song.mp3");
        setup.Probe.ExistingFiles.Add(fullPath);
        setup.Reader.MetadataByPath[fullPath] = new AudioMetadata { Title = "Wrong", Artist = "Wrong" };

        var manifest = Manifest(new BackupManifestEntry
        {
            RelativePath = "song.mp3",
            Metadata = new AudioMetadata { Title = "Right", Artist = "Right" },
            TagFormat = TagFormat.Id3v23,
        });

        var preview = await setup.Restore.BuildPreviewAsync(manifest);
        Assert.Equal(1, preview.FilesToRestore);

        var result = await setup.Restore.ExecuteAsync(preview, Id3WriteVersion.V23);

        Assert.Equal(1, result.UpdatedCount);
        var write = Assert.Single(setup.Writer.Writes);
        Assert.Equal("Right", write.Update.Edits[MetadataField.Title].Value);

        var backupManifest = Assert.Single(setup.Backup.Manifests);
        Assert.Equal("restore", backupManifest.Operation);
        Assert.Equal("Wrong", backupManifest.Entries[0].Metadata.Title);
    }

    [Fact]
    public async Task Restore_preview_lists_the_changed_fields()
    {
        var setup = CreateSetup();
        setup.Probe.ExistingDirectories.Add(Root);
        var fullPath = Path.Combine(Root, "song.mp3");
        setup.Probe.ExistingFiles.Add(fullPath);
        setup.Reader.MetadataByPath[fullPath] = new AudioMetadata { Title = "Wrong" };

        var manifest = Manifest(new BackupManifestEntry
        {
            RelativePath = "song.mp3",
            Metadata = new AudioMetadata { Title = "Right", Album = "Album" },
        });

        var preview = await setup.Restore.BuildPreviewAsync(manifest);

        var plan = Assert.Single(preview.Preview.Files);
        Assert.Equal(2, plan.Changes.Count);
        Assert.Contains(plan.Changes, change => change.Field == MetadataField.Title && change.NewValue == "Right");
        Assert.Contains(plan.Changes, change => change.Field == MetadataField.Album && change.OldValue is null);
    }
}
