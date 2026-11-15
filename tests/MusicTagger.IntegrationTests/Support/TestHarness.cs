using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;
using MusicTagger.Core.Services;
using MusicTagger.Infrastructure.AudioTags;
using MusicTagger.Infrastructure.Backup;
using MusicTagger.Infrastructure.FileSystem;
using MusicTagger.Infrastructure.Logging;

namespace MusicTagger.IntegrationTests.Support;

/// <summary>Wires the real infrastructure services against a temporary workspace.</summary>
public sealed class TestHarness : IDisposable
{
    public TestHarness(string? workspaceName = null)
    {
        Workspace = new TempWorkspace(workspaceName);
        Log = NullAppLog.Instance;
        Probe = new PhysicalFileProbe();
        Reader = new TagLibAudioMetadataReader(Log);
        Writer = new TagLibAudioMetadataWriter(Log);
        Backup = new JsonBackupService(Workspace.Combine("backups"), Log);
        Planner = new UpdatePlanner(Reader, Probe, Log);
        Executor = new BatchUpdateExecutor(Writer, Backup, Probe, Log);
        Restore = new RestoreService(Reader, Backup, Executor, Probe, Log);
        Scanner = new Mp3FileScanner(Log);
    }

    public TempWorkspace Workspace { get; }

    public IAppLog Log { get; }

    public PhysicalFileProbe Probe { get; }

    public TagLibAudioMetadataReader Reader { get; }

    public TagLibAudioMetadataWriter Writer { get; }

    public JsonBackupService Backup { get; }

    public UpdatePlanner Planner { get; }

    public BatchUpdateExecutor Executor { get; }

    public RestoreService Restore { get; }

    public Mp3FileScanner Scanner { get; }

    public string Root => Workspace.Root;

    public SelectedFile Select(string relativePath)
    {
        var fullPath = Workspace.Combine(relativePath);
        return new SelectedFile(fullPath, relativePath.Replace('\\', '/'));
    }

    public void Dispose() => Workspace.Dispose();
}
