using MusicTagger.Cli.Flows;
using MusicTagger.Cli.Presentation;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;
using MusicTagger.Infrastructure.Configuration;
using MusicTagger.Infrastructure.Logging;
using Spectre.Console;

namespace MusicTagger.Cli;

/// <summary>
/// Entry point: parses the command line switches, loads the settings, creates the console and
/// hands control to the menu flow. Everything the app needs is wired here.
/// </summary>
internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitError = 1;
    private const int ExitCancelled = 130;

    private static async Task<int> Main(string[] args)
    {
        if (HasArgument(args, "--help", "-h", "/?"))
        {
            PrintHelp();
            return ExitOk;
        }

        if (HasArgument(args, "--version", "-v"))
        {
            Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version}");
            return ExitOk;
        }

        var forceNoColor = HasArgument(args, "--no-color");

        // UTF-8 must be configured before the console is created: the console writer captures
        // the code page that is active at that moment (SRS NFR-001).
        var encodingWarning = ConsoleFactory.ConfigureUtf8();

        // The log exists before the console so that even a startup failure is recorded.
        var log = new FileLogger(AppPaths.LogDirectory);
        log.Info($"{AppInfo.ProductName} {AppInfo.Version} started (OS: {Environment.OSVersion}, runtime: {Environment.Version}).");

        var settingsStore = new JsonSettingsStore(AppPaths.SettingsFilePath, log);
        var settings = await settingsStore.LoadAsync().ConfigureAwait(false);

        if (forceNoColor)
        {
            settings = settings with { ConsoleColor = ColorMode.Off };
        }

        var console = ConsoleFactory.Create(settings.ConsoleColor);

        if (encodingWarning is not null)
        {
            log.Warn(encodingWarning);
            console.MarkupLine($"[grey]{Markup.Escape(encodingWarning)}[/]");
        }

        using var cancellation = new CancellationHost(console);

        var context = new CliContext(
            console,
            new ConsoleInput(console),
            new AppPresenter(console),
            new ProgressReporter(console),
            log,
            settingsStore,
            settings,
            cancellation.Token);

        try
        {
            await new MainMenuFlow(context).RunAsync().ConfigureAwait(false);
            return ExitOk;
        }
        catch (OperationCanceledException)
        {
            console.WriteLine();
            Console.Error.WriteLine("Cancelled.");
            return ExitCancelled;
        }
        catch (Exception ex)
        {
            log.Error("The application stopped because of an unexpected error.", ex);
            Console.Error.WriteLine($"Unexpected error: {UserFriendlyErrors.Describe(ex)}");
            Console.Error.WriteLine($"Details were written to the log: {AppPaths.LogDirectory}");
            return ExitError;
        }
        finally
        {
            log.Info($"{AppInfo.ProductName} stopped.");
        }
    }

    private static bool HasArgument(string[] args, params string[] names) =>
        args.Any(argument => names.Any(name => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)));

    private static void PrintHelp()
    {
        Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version}");
        Console.WriteLine(AppInfo.Description);
        Console.WriteLine();
        Console.WriteLine("Scan a folder of music files (MP3, FLAC, M4A, OGG, Opus, WAV, AIFF, AAC),");
        Console.WriteLine("review and edit their metadata in batches, and roll back from the metadata");
        Console.WriteLine("backup that is written before every change.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  MusicTagger.Cli [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help       Show this help text and exit.");
        Console.WriteLine("  -v, --version    Show the version and exit.");
        Console.WriteLine("      --no-color   Disable console color for this run (overrides the setting).");
        Console.WriteLine();
        Console.WriteLine("Nothing is written without a preview and an explicit confirmation.");
        Console.WriteLine("Before metadata is changed, the old metadata of every file is stored as JSON:");
        Console.WriteLine($"  backups:  {AppPaths.DefaultBackupDirectory}");
        Console.WriteLine($"  settings: {AppPaths.SettingsFilePath}");
        Console.WriteLine($"  logs:     {AppPaths.LogDirectory}");
    }
}
