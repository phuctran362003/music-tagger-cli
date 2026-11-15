using Spectre.Console;

namespace MusicTagger.Cli.Presentation;

/// <summary>
/// Long running work (scan, preview, batch write) is shown with a progress bar when the
/// terminal is interactive (SRS FR-012). When the output is redirected - or the terminal
/// cannot render live output - the same work prints plain start/finish lines, because a
/// live display requires a real terminal.
/// </summary>
public sealed class ProgressReporter
{
    private readonly IAnsiConsole _console;

    public ProgressReporter(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public bool IsInteractive => _console.Profile.Capabilities.Interactive;

    /// <summary>
    /// Runs <paramref name="work"/> and reports its progress. <paramref name="total"/> is the
    /// number of units (files) that will be reported.
    /// </summary>
    public async Task<T> RunAsync<T>(string title, int total, Func<ProgressHandle, Task<T>> work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(work);

        var maxValue = Math.Max(total, 1);

        if (!IsInteractive)
        {
            _console.MarkupLine($"[grey]{Markup.Escape(title)}[/]");

            var plain = new ProgressHandle(maxValue);
            var result = await work(plain).ConfigureAwait(false);
            _console.MarkupLine($"[grey]Done: {plain.Processed}/{maxValue}[/]");
            return result;
        }

        T? value = default;
        await _console
            .Progress()
            .Columns(
                new SpinnerColumn(),
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn())
            .StartAsync(async context =>
            {
                var task = context.AddTask(title, maxValue: maxValue);
                var handle = new ProgressHandle(maxValue, processed => task.Value = processed);

                value = await work(handle).ConfigureAwait(false);

                task.Value = task.MaxValue;
                task.StopTask();
            })
            .ConfigureAwait(false);

        return value!;
    }
}

/// <summary>Handle passed to the work callback so it can report how far it has progressed.</summary>
public sealed class ProgressHandle
{
    private readonly Action<double> _apply;

    internal ProgressHandle(double maxValue, Action<double>? apply = null)
    {
        MaxValue = maxValue;
        _apply = apply ?? (_ => { });
    }

    public double MaxValue { get; }

    public int Processed { get; private set; }

    public void Report(int processed)
    {
        Processed = Math.Clamp(processed, 0, (int)MaxValue);
        _apply(Processed);
    }
}
