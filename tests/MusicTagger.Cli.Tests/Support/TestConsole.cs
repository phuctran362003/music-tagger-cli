using Spectre.Console;

namespace MusicTagger.Cli.Tests.Support;

/// <summary>
/// Captured console for the flow tests: colours are off, the terminal is not interactive and
/// the answers are read from a script instead of the keyboard.
/// </summary>
public sealed class TestConsole : IDisposable
{
    private readonly StringWriter _writer;
    private readonly TextReader _originalInput;
    private bool _disposed;

    public TestConsole(IEnumerable<string> answers)
    {
        _writer = new StringWriter();
        _originalInput = Console.In;

        Ansi = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(_writer),
        });

        Console.SetIn(new StringReader(string.Join("\n", answers) + "\n"));
    }

    public IAnsiConsole Ansi { get; }

    /// <summary>Everything the UI has written so far.</summary>
    public string Output => _writer.ToString();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.SetIn(_originalInput);
        _writer.Dispose();
    }
}
