using MusicTagger.Core.Errors;

namespace MusicTagger.Cli.Flows;

/// <summary>
/// Top level menu (FR-001). Each sub flow returns when the user goes back; unexpected errors
/// are reported without terminating the app, so one broken folder cannot end the session.
/// </summary>
public sealed class MainMenuFlow
{
    private readonly CliContext _context;

    public MainMenuFlow(CliContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task RunAsync()
    {
        var options = new[]
        {
            "Scan and edit music folder",
            "Restore metadata backup",
            "Settings",
            "Exit",
        };

        while (true)
        {
            _context.Presenter.ShowBlankLine();
            _context.Presenter.ShowBanner();

            var choice = _context.Input.AskChoice("Select an option", options, allowBack: false);
            if (choice is null)
            {
                _context.Presenter.ShowInfo("Input ended - goodbye.");
                return;
            }

            try
            {
                switch (choice.Value)
                {
                    case 0:
                        await new ScanEditFlow(_context).RunAsync().ConfigureAwait(false);
                        break;
                    case 1:
                        await new RestoreFlow(_context).RunAsync().ConfigureAwait(false);
                        break;
                    case 2:
                        await new SettingsFlow(_context).RunAsync().ConfigureAwait(false);
                        break;
                    default:
                        _context.Presenter.ShowInfo("Goodbye.");
                        return;
                }
            }
            catch (OperationCanceledException) when (_context.CancellationToken.IsCancellationRequested)
            {
                _context.Presenter.ShowWarning(
                    "Operation cancelled. Files that were already written are recorded in the backup manifest.");
                return;
            }
            catch (Exception ex)
            {
                _context.Log.Error("An unexpected error occurred.", ex);
                _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
                _context.Presenter.ShowHint("The details were written to the log file.");
            }
        }
    }
}
