using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class ManifestValidatorTests
{
    private static BackupManifest ValidManifest() => new()
    {
        ManifestId = Guid.NewGuid().ToString("N"),
        RootFolder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "root")),
        Operation = "edit",
        CreatedUtc = DateTimeOffset.UtcNow,
        Entries =
        [
            new BackupManifestEntry
            {
                RelativePath = Path.Combine("album", "01.mp3"),
                Metadata = new AudioMetadata { Title = "T" },
                TagFormat = TagFormat.Id3v23,
            },
        ],
    };

    [Fact]
    public void A_valid_manifest_has_no_errors()
    {
        Assert.Empty(ManifestValidator.Validate(ValidManifest()));
    }

    [Fact]
    public void Missing_manifest_is_reported()
    {
        Assert.NotEmpty(ManifestValidator.Validate(null));
    }

    [Fact]
    public void Missing_required_fields_are_reported()
    {
        var manifest = ValidManifest() with
        {
            ManifestId = "",
            RootFolder = "relative/folder",
            Operation = "",
            CreatedUtc = default,
            Entries = [],
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Absolute", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Operation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("timestamp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("entries", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unsafe_relative_paths_are_reported()
    {
        var manifest = ValidManifest();
        manifest = manifest with
        {
            Entries = [manifest.Entries[0] with { RelativePath = "../../etc/passwd" }],
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("not safe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Duplicate_entries_are_reported()
    {
        var manifest = ValidManifest();
        manifest = manifest with
        {
            Entries = [manifest.Entries[0], manifest.Entries[0] with { Metadata = new AudioMetadata { Album = "x" } }],
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
