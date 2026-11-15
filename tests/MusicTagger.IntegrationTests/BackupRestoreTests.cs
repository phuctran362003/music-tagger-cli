using System.Text.Json;
using System.Text.Json.Nodes;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;
using MusicTagger.IntegrationTests.Support;
using Xunit;

namespace MusicTagger.IntegrationTests;

/// <summary>Backup manifest creation, validation and the full restore round trip.</summary>
public sealed class BackupRestoreTests
{
    [Fact]
    public async Task Backup_manifest_contains_relative_paths_utc_timestamp_old_metadata_and_statuses()
    {
        using var harness = new TestHarness("backup");
        var selected = harness.Select("Nhạc Trịnh/sub/01.mp3");
        harness.Workspace.CreateFile("Nhạc Trịnh/sub/01.mp3");

        var setup = await harness.Writer.WriteAsync(
            selected.FilePath,
            MetadataUpdate.Empty
                .WithSet(MetadataField.Title, "Old title")
                .WithSet(MetadataField.Artist, "Old artist"),
            Id3WriteVersion.V23);
        Assert.True(setup.Success, setup.ErrorMessage);

        var update = MetadataUpdate.Empty.WithSet(MetadataField.Artist, "New artist");
        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [selected], update);
        var result = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        Assert.NotNull(result.BackupFilePath);
        Assert.True(File.Exists(result.BackupFilePath));

        var json = JsonNode.Parse(await File.ReadAllTextAsync(result.BackupFilePath!))!;
        Assert.False(string.IsNullOrWhiteSpace(json["manifestId"]!.GetValue<string>()));
        Assert.Equal(harness.Root, json["rootFolder"]!.GetValue<string>(), ignoreCase: true);
        Assert.Equal("edit", json["operation"]!.GetValue<string>());

        var createdAt = json["createdUtc"]!.GetValue<DateTimeOffset>();
        Assert.True(createdAt > DateTimeOffset.UtcNow.AddMinutes(-5), "createdUtc must be a recent UTC timestamp.");

        var entry = json["entries"]!.AsArray()[0]!;
        Assert.Equal("Nhạc Trịnh/sub/01.mp3", entry["relativePath"]!.GetValue<string>().Replace('\\', '/'));
        Assert.Equal("Old title", entry["metadata"]!["title"]!.GetValue<string>());
        Assert.Equal("Old artist", entry["metadata"]!["artist"]!.GetValue<string>());
        Assert.Equal("id3v23", entry["tagFormat"]!.GetValue<string>(), ignoreCase: true);
        Assert.Equal("updated", entry["status"]!.GetValue<string>(), ignoreCase: true);
        Assert.NotNull(entry["processedUtc"]);

        // The manifest must not contain audio bytes.
        Assert.DoesNotContain("ID3", await File.ReadAllTextAsync(result.BackupFilePath!));
    }

    [Fact]
    public async Task Restore_puts_the_original_metadata_back_and_backs_up_the_current_state_first()
    {
        using var harness = new TestHarness("restore");
        var selected = harness.Select("restore.mp3");
        harness.Workspace.CreateFile("restore.mp3");

        var original = MetadataUpdate.Empty
            .WithSet(MetadataField.Title, "Đường Đến Ngày Vinh Quang")
            .WithSet(MetadataField.Artist, "Trịnh Công Sơn")
            .WithSet(MetadataField.Album, "Những Bản Tình Ca");

        Assert.True((await harness.Writer.WriteAsync(selected.FilePath, original, Id3WriteVersion.V23)).Success);

        // Change two fields; this creates the backup with the original values.
        var change = MetadataUpdate.Empty
            .WithSet(MetadataField.Artist, "Wrong Artist")
            .WithSet(MetadataField.Album, "Wrong Album");

        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [selected], change);
        var editResult = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));
        Assert.Equal(1, editResult.UpdatedCount);

        var afterEdit = await harness.Reader.ReadAsync(selected.FilePath);
        Assert.Equal("Wrong Artist", afterEdit.Metadata.Artist);

        // Restore from that manifest.
        var manifests = await harness.Restore.ListBackupsAsync();
        Assert.Single(manifests);

        var load = await harness.Restore.LoadManifestAsync(manifests[0].ManifestPath);
        Assert.True(load.IsValid);
        Assert.NotNull(load.Manifest);

        var restorePreview = await harness.Restore.BuildPreviewAsync(load.Manifest!);
        Assert.Equal(1, restorePreview.FilesToRestore);

        var restoreResult = await harness.Restore.ExecuteAsync(restorePreview, Id3WriteVersion.V23);
        Assert.Equal(1, restoreResult.UpdatedCount);
        Assert.Equal(0, restoreResult.FailedCount);

        var restored = await harness.Reader.ReadAsync(selected.FilePath);
        Assert.True(restored.Success, restored.ErrorMessage);
        Assert.Equal("Đường Đến Ngày Vinh Quang", restored.Metadata.Title);
        Assert.Equal("Trịnh Công Sơn", restored.Metadata.Artist);
        Assert.Equal("Những Bản Tình Ca", restored.Metadata.Album);

        // A second manifest now holds the pre-restore state, so the restore itself can be undone.
        var manifestsAfter = await harness.Restore.ListBackupsAsync();
        Assert.Equal(2, manifestsAfter.Count);
        Assert.Equal("restore", manifestsAfter[0].Operation);
    }

    [Fact]
    public async Task Restore_skips_files_that_no_longer_exist_and_continues()
    {
        using var harness = new TestHarness("restore-missing");
        var first = harness.Select("keep.mp3");
        var second = harness.Select("gone.mp3");
        harness.Workspace.CreateFile("keep.mp3");
        harness.Workspace.CreateFile("gone.mp3");

        var update = MetadataUpdate.Empty.WithSet(MetadataField.Album, "Album Z");
        var preview = await harness.Planner.BuildPreviewAsync(harness.Root, [first, second], update);
        var editResult = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        File.Delete(second.FilePath);

        var load = await harness.Restore.LoadManifestAsync(editResult.BackupFilePath!);
        Assert.True(load.IsValid);

        // Restore to the state before the edit: the album must be cleared again.
        var restorePreview = await harness.Restore.BuildPreviewAsync(load.Manifest!);
        Assert.Equal(1, restorePreview.FilesToRestore);
        Assert.Equal(1, restorePreview.Preview.SkippedCount);

        var restoreResult = await harness.Restore.ExecuteAsync(restorePreview, Id3WriteVersion.V23);
        Assert.Equal(1, restoreResult.UpdatedCount);
        Assert.Equal(1, restoreResult.SkippedCount);

        var restored = await harness.Reader.ReadAsync(first.FilePath);
        Assert.Null(restored.Metadata.Album);
    }

    [Fact]
    public async Task Restore_rejects_a_manifest_with_a_path_outside_the_root_folder()
    {
        using var harness = new TestHarness("traversal");
        var manifestPath = harness.Workspace.Combine("backups").Replace("\\", "/");
        Directory.CreateDirectory(manifestPath);

        var file = Path.Combine(manifestPath, "malicious.json");
        await File.WriteAllTextAsync(file, ManifestJson(harness.Root, "../outside.mp3"));

        var load = await harness.Restore.LoadManifestAsync(file);

        Assert.False(load.IsValid);
        Assert.Contains(load.Errors, error => error.Contains("not safe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Restore_rejects_a_manifest_with_an_absolute_entry_path()
    {
        using var harness = new TestHarness("absolute");
        var directory = harness.Workspace.Combine("backups");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "absolute.json");

        await File.WriteAllTextAsync(file, ManifestJson(harness.Root, Path.Combine(Path.GetTempPath(), "evil.mp3")));

        var load = await harness.Restore.LoadManifestAsync(file);

        Assert.False(load.IsValid);
        Assert.NotEmpty(load.Errors);
    }

    [Fact]
    public async Task Restore_refuses_to_run_with_an_invalid_manifest()
    {
        using var harness = new TestHarness("invalid-manifest");
        var invalid = new BackupManifest
        {
            ManifestId = "",
            RootFolder = "relative/path",
            Operation = "",
            CreatedUtc = default,
            Entries = [],
        };

        await Assert.ThrowsAsync<InvalidManifestException>(() => harness.Restore.BuildPreviewAsync(invalid));
    }

    [Fact]
    public async Task Restore_preview_does_not_write_to_the_files()
    {
        using var harness = new TestHarness("restore-preview");
        var selected = harness.Select("file.mp3");
        harness.Workspace.CreateFile("file.mp3");

        var preview = await harness.Planner.BuildPreviewAsync(
            harness.Root,
            [selected],
            MetadataUpdate.Empty.WithSet(MetadataField.Album, "Changed"));
        var editResult = await harness.Executor.ExecuteAsync(new BatchUpdateRequest(harness.Root, "edit", preview.Files, Id3WriteVersion.V23));

        var bytesBefore = await File.ReadAllBytesAsync(selected.FilePath);

        var load = await harness.Restore.LoadManifestAsync(editResult.BackupFilePath!);
        var restorePreview = await harness.Restore.BuildPreviewAsync(load.Manifest!);

        Assert.Equal(1, restorePreview.FilesToRestore);
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(selected.FilePath));
    }

    private static string ManifestJson(string rootFolder, string relativePath)
    {
        var document = new
        {
            manifestId = Guid.NewGuid().ToString("N"),
            rootFolder,
            operation = "edit",
            createdUtc = DateTimeOffset.UtcNow,
            appVersion = "1.0.0",
            entries = new[]
            {
                new
                {
                    relativePath,
                    metadata = new { title = "Injected" },
                    tagFormat = "id3v23",
                    status = "captured",
                },
            },
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }
}
