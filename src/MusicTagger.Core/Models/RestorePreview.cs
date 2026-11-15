using MusicTagger.Core.Models;

namespace MusicTagger.Core.Models;

/// <summary>Dry run of a restore operation.</summary>
public sealed record RestorePreview(
    BackupManifest Manifest,
    UpdatePreview Preview,
    IReadOnlyList<string> Warnings)
{
    public string RootFolder => Manifest.RootFolder;

    public int FilesToRestore => Preview.FilesToUpdateCount;

    public bool HasChanges => Preview.HasChanges;
}
