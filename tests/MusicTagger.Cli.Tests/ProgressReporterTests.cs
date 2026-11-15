using MusicTagger.Cli.Presentation;
using MusicTagger.Cli.Tests.Support;
using Xunit;

namespace MusicTagger.Cli.Tests;

/// <summary>
/// A live progress bar needs a real terminal. When the output is redirected the reporter must
/// fall back to plain lines instead of writing carriage return frames into a log file.
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class ProgressReporterTests
{
    [Fact]
    public async Task RunAsync_WithoutATerminal_PrintsPlainLinesAndReportsProgress()
    {
        using var console = new TestConsole([]);
        var reporter = new ProgressReporter(console.Ansi);
        var reported = new List<int>();

        Assert.False(reporter.IsInteractive);

        var result = await reporter.RunAsync(
            "Reading metadata",
            2,
            handle =>
            {
                handle.Report(1);
                reported.Add(handle.Processed);
                handle.Report(2);
                reported.Add(handle.Processed);

                return Task.FromResult("done");
            });

        Assert.Equal("done", result);
        Assert.Equal([1, 2], reported);
        Assert.Contains("Reading metadata", console.Output, StringComparison.Ordinal);
        Assert.Contains("Done: 2/2", console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", console.Output.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ClampsProgressToTheTotal()
    {
        using var console = new TestConsole([]);
        var reporter = new ProgressReporter(console.Ansi);

        var processed = await reporter.RunAsync(
            "Writing",
            1,
            handle =>
            {
                handle.Report(5);
                return Task.FromResult(handle.Processed);
            });

        Assert.Equal(1, processed);
    }

    [Fact]
    public async Task RunAsync_PropagatesExceptions()
    {
        using var console = new TestConsole([]);
        var reporter = new ProgressReporter(console.Ansi);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reporter.RunAsync<int>(
                "Failing",
                1,
                _ => throw new InvalidOperationException("boom")));
    }
}
