using MusicTagger.Core.Models;
using MusicTagger.Infrastructure.Backup;
using MusicTagger.Infrastructure.Configuration;
using MusicTagger.Infrastructure.Logging;
using Xunit;

namespace MusicTagger.IntegrationTests;

/// <summary>
/// The files in <c>samples/</c> are documentation, but users copy them, so they are checked
/// against the real stores instead of being kept in sync by hand.
/// </summary>
public sealed class SampleDocumentationTests
{
    [Fact]
    public async Task Sample_manifest_is_a_valid_backup_manifest()
    {
        var path = Path.Combine(RepositoryRoot(), "samples", "backup-manifest.example.json");
        Assert.True(File.Exists(path), $"Sample manifest not found: {path}");

        var service = new JsonBackupService(Path.Combine(Path.GetTempPath(), "mtt-sample-backups"), NullAppLog.Instance);
        var load = await service.LoadManifestAsync(path);

        Assert.True(load.IsValid, string.Join("; ", load.Errors));
        Assert.NotNull(load.Manifest);
        Assert.Equal(5, load.Manifest!.Entries.Count);

        // Every tag format that the application can report is spelled the way the enum is stored.
        Assert.Equal(TagFormat.Id3v23, load.Manifest.Entries[0].TagFormat);
        Assert.Equal(TagFormat.VorbisComment, load.Manifest.Entries[3].TagFormat);
        Assert.Equal(TagFormat.RiffInfo, load.Manifest.Entries[4].TagFormat);

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"tagFormat\": \"id3v23\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tagFormat\": \"vorbisComment\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tagFormat\": \"riffInfo\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sample_settings_load_into_the_real_settings_model()
    {
        var path = Path.Combine(RepositoryRoot(), "samples", "settings.json");
        Assert.True(File.Exists(path), $"Sample settings not found: {path}");

        var store = new JsonSettingsStore(path, NullAppLog.Instance);
        var settings = await store.LoadAsync();

        Assert.NotNull(settings);
        Assert.Equal(Id3WriteVersion.V23, settings.Id3WriteVersion);
        Assert.True(settings.RecursiveScan);
        Assert.Empty(settings.Validate());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MusicTaggerCli.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root (MusicTaggerCli.sln) could not be located.");
    }
}
