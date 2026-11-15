using Spectre.Console;

namespace MusicTagger.Cli.Presentation;

/// <summary>
/// Line based input on top of the rendered console. Every prompt can be answered with
/// <c>b</c> (back) so the user is never trapped in a sub menu, and nothing is written to disk
/// without an explicit confirmation (SRS FR-005, FR-009).
/// </summary>
public sealed class ConsoleInput
{
    private readonly IAnsiConsole _console;

    public ConsoleInput(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public bool IsInteractive => _console.Profile.Capabilities.Interactive;

    /// <summary>Reads a raw line. Returns null on end of input (Ctrl+Z / Ctrl+D).</summary>
    public string? ReadLine(string prompt, string? hint = null)
    {
        if (!string.IsNullOrEmpty(hint))
        {
            _console.MarkupLine($"[grey]{Markup.Escape(hint)}[/]");
        }

        _console.Markup($"[bold]{Markup.Escape(prompt)}[/] ");
        var line = Console.ReadLine();

        if (line is null)
        {
            _console.WriteLine();
            return null;
        }

        return line.Trim();
    }

    public bool IsBackToken(string value) =>
        value.Equals("b", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("back", StringComparison.OrdinalIgnoreCase);

    public bool IsQuitToken(string value) =>
        value.Equals("q", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("cancel", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asks for free text. Returns null when the user goes back or the input stream ends.
    /// </summary>
    public string? AskText(
        string prompt,
        string? hint = null,
        bool allowEmpty = false,
        Func<string, string?>? validate = null)
    {
        while (true)
        {
            var value = ReadLine(prompt, hint);
            if (value is null || IsBackToken(value))
            {
                return null;
            }

            if (value.Length == 0)
            {
                if (allowEmpty)
                {
                    return string.Empty;
                }

                _console.MarkupLine("[yellow]Please type a value, or b to go back.[/]");
                continue;
            }

            var error = validate?.Invoke(value);
            if (error is not null)
            {
                _console.MarkupLine($"[red]{Markup.Escape(error)}[/]");
                continue;
            }

            return value;
        }
    }

    /// <summary>
    /// Asks the user to pick one option and returns its zero based index.
    /// On an interactive terminal the list is navigated with the arrow keys and confirmed with
    /// Enter. When input is piped, the same menu accepts a number so scripts and tests keep
    /// working. Returns null when the user goes back or the input stream ends.
    /// <paramref name="showBackLine"/> is false when the caller already lists "Back" as one of
    /// the options; the <c>b</c> shortcut still works on the numbered fallback.
    /// </summary>
    public int? AskChoice(
        string prompt,
        IReadOnlyList<string> options,
        bool allowBack = true,
        bool showBackLine = true)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (IsInteractive)
        {
            return AskChoiceWithArrows(prompt, options, allowBack, showBackLine);
        }

        return AskChoiceByNumber(prompt, options, allowBack, showBackLine);
    }

    private int? AskChoiceWithArrows(
        string prompt,
        IReadOnlyList<string> options,
        bool allowBack,
        bool showBackLine)
    {
        var choices = new List<MenuChoice>(options.Count + 1);
        for (var i = 0; i < options.Count; i++)
        {
            choices.Add(new MenuChoice(i, options[i]));
        }

        if (allowBack && showBackLine)
        {
            choices.Add(new MenuChoice(null, "Back"));
        }

        if (choices.Count == 0)
        {
            return null;
        }

        _console.WriteLine();
        var selection = new SelectionPrompt<MenuChoice>()
            .Title($"[bold]{Markup.Escape(prompt)}[/]\n[grey]Arrow keys to move, Enter to choose.[/]")
            .PageSize(Math.Max(3, Math.Min(12, choices.Count)))
            .WrapAround()
            .MoreChoicesText("[grey](More options above or below)[/]")
            .UseConverter(choice => Markup.Escape(choice.Label))
            .AddChoices(choices);

        var picked = _console.Prompt(selection);
        return picked.Index;
    }

    private int? AskChoiceByNumber(
        string prompt,
        IReadOnlyList<string> options,
        bool allowBack,
        bool showBackLine)
    {
        _console.WriteLine();
        for (var i = 0; i < options.Count; i++)
        {
            _console.MarkupLine($"  [bold]{i + 1}[/]. {Markup.Escape(options[i])}");
        }

        if (allowBack && showBackLine)
        {
            _console.MarkupLine($"  [bold]b[/]. {Markup.Escape("Back")}");
        }

        while (true)
        {
            var value = ReadLine(prompt);
            if (value is null)
            {
                return null;
            }

            if (allowBack && IsBackToken(value))
            {
                return null;
            }

            if (int.TryParse(value, out var number) && number >= 1 && number <= options.Count)
            {
                return number - 1;
            }

            _console.MarkupLine($"[yellow]Enter a number between 1 and {options.Count}{(allowBack ? ", or b to go back" : string.Empty)}.[/]");
        }
    }

    private readonly record struct MenuChoice(int? Index, string Label);

    /// <summary>
    /// Asks for one or more numbers ("1,3", "1 4", "all"). Returns null when the user goes back.
    /// </summary>
    public IReadOnlyList<int>? AskMultipleChoice(
        string prompt,
        IReadOnlyList<string> options,
        bool allowAll = true,
        bool allowBack = true)
    {
        ArgumentNullException.ThrowIfNull(options);

        _console.WriteLine();
        for (var i = 0; i < options.Count; i++)
        {
            _console.MarkupLine($"  [bold]{i + 1}[/]. {Markup.Escape(options[i])}");
        }

        if (allowAll)
        {
            _console.MarkupLine($"  [bold]a[/]. {Markup.Escape("All of the above")}");
        }

        if (allowBack)
        {
            _console.MarkupLine($"  [bold]b[/]. {Markup.Escape("Back")}");
        }

        while (true)
        {
            var value = ReadLine(prompt, "Example: 1,3 or a for all");
            if (value is null)
            {
                return null;
            }

            if (allowBack && IsBackToken(value))
            {
                return null;
            }

            if (allowAll && value.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Range(0, options.Count).ToList();
            }

            var parts = value.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var selected = new SortedSet<int>();
            var valid = parts.Length > 0;

            foreach (var part in parts)
            {
                if (int.TryParse(part, out var number) && number >= 1 && number <= options.Count)
                {
                    selected.Add(number - 1);
                }
                else
                {
                    valid = false;
                }
            }

            if (valid)
            {
                return selected.ToList();
            }

            _console.MarkupLine($"[yellow]Enter numbers between 1 and {options.Count} separated by commas.[/]");
        }
    }

    /// <summary>Yes/No question. The default is "no" unless stated otherwise (SRS FR-009).</summary>
    public bool? AskYesNo(string prompt, bool defaultYes = false, bool allowBack = false)
    {
        var suffix = defaultYes ? "[Y/n]" : "[y/N]";

        while (true)
        {
            var value = ReadLine($"{prompt} {suffix}");
            if (value is null)
            {
                return null;
            }

            if (value.Length == 0)
            {
                return defaultYes;
            }

            if (allowBack && IsBackToken(value))
            {
                return null;
            }

            if (value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _console.MarkupLine("[yellow]Answer y or n.[/]");
        }
    }

    public void WaitForEnter(string message)
    {
        ReadLine(message);
    }
}
