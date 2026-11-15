namespace MusicTagger.Infrastructure.AudioTags;

/// <summary>
/// TagLib# stores the preferred ID3v2 version in static state, so every read and write
/// is serialized through this gate. The application writes one file at a time anyway.
/// </summary>
internal static class TagLibGate
{
    public static SemaphoreSlim Semaphore { get; } = new(1, 1);
}
