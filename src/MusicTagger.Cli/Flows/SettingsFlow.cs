using MusicTagger.Cli.Presentation;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;

namespace MusicTagger.Cli.Flows;

/// <summary>
/// Settings required by FR-014: ID3 write version, recursive scan, backup directory and
/// console colour. They are stored as JSON in the application data directory.
/// </summary>
public sealed class SettingsFlow
{
    private readonly CliContext _context;

    public SettingsFlow(CliContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task RunAsync()
    {
        while (true)
        {
            var backupCount = await CountBackupsAsync().ConfigureAwait(false);

            _context.Presenter.ShowBlankLine();
            _context.Presenter.ShowSettings(
                _context.Settings,
                backupCount,
                _context.SettingsFilePath,
                _context.BackupDirectory,
                _context.LogDirectory);

            var choice = _context.Input.AskChoice(
                "Change a setting",
                ["ID3 write version", "Recursive scan", "Backup directory", "Console color", "Back"],
                allowBack: true,
                showBackLine: false);

            if (choice is null)
            {
                return;
            }

            switch (choice.Value)
            {
                case 0:
                    await ChangeId3VersionAsync().ConfigureAwait(false);
                    break;
                case 1:
                    await ChangeRecursiveScanAsync().ConfigureAwait(false);
                    break;
                case 2:
                    await ChangeBackupDirectoryAsync().ConfigureAwait(false);
                    break;
                case 3:
                    await ChangeConsoleColorAsync().ConfigureAwait(false);
                    break;
                default:
                    return;
            }
        }
    }

    private async Task<int> CountBackupsAsync()
    {
        try
        {
            var backups = await _context.RestoreService
                .ListBackupsAsync(_context.CancellationToken)
                .ConfigureAwait(false);

            return backups.Count;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _context.Log.Warn($"The number of backups could not be determined: {ex.Message}");
            return 0;
        }
    }

    private async Task ChangeId3VersionAsync()
    {
        var options = new[]
        {
            "ID3v2.3 - UTF-16 text frames, best compatibility with older players (default)",
            "ID3v2.4 - UTF-8 text frames",
        };

        _context.Presenter.ShowHint($"This setting applies to {AudioFormatCatalog.Id3FormatDisplayList}.");
        _context.Presenter.ShowHint("FLAC/OGG/Opus always use UTF-8 Vorbis comments and M4A uses UTF-8 MP4 atoms.");

        if (AudioFormatCatalog.FixedId3v24DisplayList.Length > 0)
        {
            _context.Presenter.ShowHint(
                $"{AudioFormatCatalog.FixedId3v24DisplayList} files always store ID3v2.4 inside the file, whatever this setting says.");
        }

        var choice = _context.Input.AskChoice("Write metadata as", options);
        if (choice is null)
        {
            return;
        }

        var version = choice.Value == 1 ? Id3WriteVersion.V24 : Id3WriteVersion.V23;
        if (version == _context.Settings.Id3WriteVersion)
        {
            _context.Presenter.ShowInfo("That is already the current setting.");
            return;
        }

        if (await SaveAsync(_context.Settings with { Id3WriteVersion = version }).ConfigureAwait(false))
        {
            _context.Presenter.ShowSuccess(
                $"Metadata is now written as {version.ToDisplayString()} ({version.EncodingLabel()}).");
        }
    }

    private async Task ChangeRecursiveScanAsync()
    {
        var enabled = _context.Input.AskYesNo(
            "Scan sub folders by default?",
            defaultYes: _context.Settings.RecursiveScan,
            allowBack: true);

        if (enabled is null)
        {
            return;
        }

        if (enabled.Value == _context.Settings.RecursiveScan)
        {
            _context.Presenter.ShowInfo("That is already the current setting.");
            return;
        }

        if (await SaveAsync(_context.Settings with { RecursiveScan = enabled.Value }).ConfigureAwait(false))
        {
            _context.Presenter.ShowSuccess($"Recursive scan is now {(enabled.Value ? "on" : "off")}.");
        }
    }

    private async Task ChangeBackupDirectoryAsync()
    {
        var options = new[]
        {
            $"Use the default location ({_context.DefaultBackupDirectory})",
            "Enter a different folder",
        };

        var choice = _context.Input.AskChoice("Backup directory", options);
        if (choice is null)
        {
            return;
        }

        string? configured;
        if (choice.Value == 0)
        {
            configured = null;
        }
        else
        {
            configured = AskDirectory();
            if (configured is null)
            {
                return;
            }
        }

        if (await SaveAsync(_context.Settings with { BackupDirectory = configured }).ConfigureAwait(false))
        {
            _context.Presenter.ShowSuccess($"Backups are now stored in '{_context.BackupDirectory}'.");
        }
    }

    private async Task ChangeConsoleColorAsync()
    {
        var options = new[]
        {
            "Auto - use color when the terminal supports it",
            "On - always use color",
            "Off - never use color",
        };

        var current = (int)_context.Settings.ConsoleColor;
        var choice = _context.Input.AskChoice("Console color", options);
        if (choice is null)
        {
            return;
        }

        if (choice.Value == current)
        {
            _context.Presenter.ShowInfo("That is already the current setting.");
            return;
        }

        if (await SaveAsync(_context.Settings with { ConsoleColor = (ColorMode)choice.Value }).ConfigureAwait(false))
        {
            _context.Presenter.ShowSuccess("The color setting is saved and takes effect the next time you start the app.");
        }
    }

    /// <summary>Asks for a folder that can be created and written to. Null means "back".</summary>
    private string? AskDirectory()
    {
        while (true)
        {
            var raw = _context.Input.AskText(
                "Backup folder path:",
                "The folder is created when it does not exist. Type b to go back.");

            if (raw is null)
            {
                return null;
            }

            var normalized = FolderPathValidator.Normalize(raw);
            if (normalized.Length == 0)
            {
                _context.Presenter.ShowError("Please enter a folder path.");
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(normalized);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                _context.Presenter.ShowError("That path is not a valid file system path.");
                continue;
            }

            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _context.Log.Error($"The backup folder '{fullPath}' could not be created.", ex);
                _context.Presenter.ShowError(UserFriendlyErrors.Describe(ex));
                continue;
            }

            var problem = _context.Probe.CheckDirectoryAccess(fullPath);
            if (problem is not null)
            {
                _context.Presenter.ShowError(problem);
                continue;
            }

            return fullPath;
        }
    }

    private async Task<bool> SaveAsync(AppSettings settings)
    {
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                _context.Presenter.ShowError(error);
            }

            return false;
        }

        try
        {
            await _context.ApplySettingsAsync(settings, _context.CancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context.Log.Error("The settings could not be saved.", ex);
            _context.Presenter.ShowError($"The settings could not be saved: {UserFriendlyErrors.Describe(ex)}");
            return false;
        }
    }
}
