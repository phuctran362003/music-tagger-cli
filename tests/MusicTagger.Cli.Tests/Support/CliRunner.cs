using MusicTagger.Cli.Flows;
using MusicTagger.Cli.Presentation;
using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;
using MusicTagger.Infrastructure.Configuration;
using MusicTagger.Infrastructure.Logging;
using Spectre.Console;

namespace MusicTagger.Cli.Tests.Support;

/// <summary>Builds the real CLI object graph (no mocks) and runs the menu flow.</summary>
internal static class CliRunner
{
    public static async Task RunAsync(IAnsiConsole console, AppSettings settings, TempWorkspace workspace)
    {
        var log = new NullAppLog();
        var settingsPath = workspace.FilePath("settings.json");
        var store = new JsonSettingsStore(settingsPath, log);

        var context = new CliContext(
            console,
            new ConsoleInput(console),
            new AppPresenter(console),
            new ProgressReporter(console),
            log,
            store,
            settings,
            CancellationToken.None);

        await new MainMenuFlow(context).RunAsync();
    }
}
