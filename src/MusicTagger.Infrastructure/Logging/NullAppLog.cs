using MusicTagger.Core.Contracts;

namespace MusicTagger.Infrastructure.Logging;

/// <summary>Log implementation that discards everything; used by tests and by <c>--no-log</c> runs.</summary>
public sealed class NullAppLog : IAppLog
{
    public static NullAppLog Instance { get; } = new();

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
