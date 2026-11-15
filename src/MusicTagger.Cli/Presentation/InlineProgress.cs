namespace MusicTagger.Cli.Presentation;

/// <summary>
/// <see cref="IProgress{T}"/> that invokes the handler on the reporting thread.
/// <see cref="Progress{T}"/> posts to the captured synchronization context, which means the
/// last progress values can arrive after the batch has already finished; the CLI needs the
/// values to be applied immediately so the progress bar and the counters stay correct.
/// </summary>
public sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public InlineProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value) => _handler(value);
}
