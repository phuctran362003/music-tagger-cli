using MusicTagger.Core.Validation;

namespace MusicTagger.Cli.Flows;

/// <summary>
/// Asks for a folder path until it points at an existing, readable directory (FR-002).
/// Pasting a quoted path is accepted; the quotes are removed and nothing else is rewritten.
/// </summary>
public sealed class FolderPrompt
{
    private readonly CliContext _context;

    public FolderPrompt(CliContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Returns the full path of a usable folder, or null when the user goes back.</summary>
    public string? Ask(string prompt = "Music folder path:")
    {
        while (true)
        {
            var raw = _context.Input.AskText(
                prompt,
                "Tip: paste the path; quotes around it are removed. Type b to go back.");

            if (raw is null)
            {
                return null;
            }

            var result = FolderPathValidator.Validate(raw, _context.Probe);
            if (result.IsValid && result.FullPath is not null)
            {
                return result.FullPath;
            }

            _context.Presenter.ShowError(result.Error ?? "That folder cannot be used.");
        }
    }
}
