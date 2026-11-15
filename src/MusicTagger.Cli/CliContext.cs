using MusicTagger.Cli.Presentation;
using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;
using MusicTagger.Core.Services;
using MusicTagger.Infrastructure.AudioTags;
using MusicTagger.Infrastructure.Backup;
using MusicTagger.Infrastructure.Configuration;
using MusicTagger.Infrastructure.FileSystem;
using Spectre.Console;

namespace MusicTagger.Cli;

/// <summary>
/// Composition root of the CLI: wires the concrete infrastructure to the core services and
/// carries the state the flows need (console, settings, services, cancellation token).
/// The settings dependent services are rebuilt whenever the settings change, so a new
/// backup directory takes effect immediately.
/// </summary>
public sealed class CliContext
{
    public CliContext(
        IAnsiConsole console,
        ConsoleInput input,
        AppPresenter presenter,
        ProgressReporter progress,
        IAppLog log,
        ISettingsStore settingsStore,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        Console = console ?? throw new ArgumentNullException(nameof(console));
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        SettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        CancellationToken = cancellationToken;

        Reader = new TagLibAudioMetadataReader(log);
        Writer = new TagLibAudioMetadataWriter(log);
        Scanner = new AudioFileScanner(log);
        Probe = new PhysicalFileProbe();
        Planner = new UpdatePlanner(Reader, Probe, log);

        RebuildServices();
    }

    public IAnsiConsole Console { get; }

    public ConsoleInput Input { get; }

    public AppPresenter Presenter { get; }

    public ProgressReporter Progress { get; }

    public IAppLog Log { get; }

    public ISettingsStore SettingsStore { get; }

    public AppSettings Settings { get; private set; }

    public CancellationToken CancellationToken { get; }

    public IAudioMetadataReader Reader { get; }

    public IAudioMetadataWriter Writer { get; }

    public IAudioFileScanner Scanner { get; }

    public IFileProbe Probe { get; }

    public UpdatePlanner Planner { get; }

    public IBackupService BackupService { get; private set; } = null!;

    public BatchUpdateExecutor Executor { get; private set; } = null!;

    public RestoreService RestoreService { get; private set; } = null!;

    public string SettingsFilePath => SettingsStore.SettingsFilePath;

    public string BackupDirectory => BackupService.BackupDirectory;

    public string DefaultBackupDirectory => AppPaths.DefaultBackupDirectory;

    public string LogDirectory => AppPaths.LogDirectory;

    public bool UsesCustomBackupDirectory => !string.IsNullOrWhiteSpace(Settings.BackupDirectory);

    /// <summary>Persists the settings and rebuilds the services that depend on them.</summary>
    public async Task ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await SettingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        Settings = settings;
        RebuildServices();
        Log.Info($"Settings saved (write version {settings.Id3WriteVersion.ToDisplayString()}, recursive scan {settings.RecursiveScan}, backups '{BackupDirectory}').");
    }

    /// <summary>Resolves the configured backup directory to an absolute path.</summary>
    public string ResolveBackupDirectory(AppSettings settings)
    {
        var configured = settings.BackupDirectory;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return AppPaths.DefaultBackupDirectory;
        }

        try
        {
            return Path.GetFullPath(configured);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Log.Warn($"Configured backup directory '{configured}' is not usable ({ex.Message}); using the default location.");
            return AppPaths.DefaultBackupDirectory;
        }
    }

    private void RebuildServices()
    {
        BackupService = new JsonBackupService(ResolveBackupDirectory(Settings), Log, AppInfo.Version);
        Executor = new BatchUpdateExecutor(Writer, BackupService, Probe, Log);
        RestoreService = new RestoreService(Reader, BackupService, Executor, Probe, Log);
    }
}

