using MusicTagger.Core.Errors;
using Spectre.Console;

namespace MusicTagger.Cli.Presentation;

/// <summary>
/// Turns Ctrl+C into a cooperative cancellation of the current operation instead of an
/// abrupt process exit. Cancelling is always safe: metadata is only ever replaced after the
/// backup manifest exists, and the manifest records what was already written.
/// </summary>
public sealed class CancellationHost : IDisposable
{
    private readonly IAnsiConsole _console;
    private readonly CancellationTokenSource _source = new();
    private bool _disposed;

    public CancellationHost(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public CancellationToken Token => _source.Token;

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // Keep the process alive: the running operation is unwound through the token.
        e.Cancel = true;

        if (_source.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _console.MarkupLine("[yellow]Cancelling...[/]");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= OnCancelKeyPress;
        _source.Dispose();
    }
}
