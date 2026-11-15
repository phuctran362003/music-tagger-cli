using MusicTagger.Core.Contracts;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;

namespace MusicTagger.Infrastructure.AudioTags;

/// <summary>
/// Reads audio metadata with TagLib# for every supported container. A TagLib file object is
/// opened per file and disposed immediately, so a folder with thousands of files is never
/// loaded into memory at once.
/// </summary>
public sealed class TagLibAudioMetadataReader : IAudioMetadataReader
{
    private readonly IAppLog _log;

    public TagLibAudioMetadataReader(IAppLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<MetadataReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Serialize with writes: TagLib# keeps the target ID3 version in static state.
        await TagLibGate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task
                .Run(() => ReadCore(filePath, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            TagLibGate.Semaphore.Release();
        }
    }

    private MetadataReadResult ReadCore(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            return MetadataReadResult.Failure("The file no longer exists.");
        }

        try
        {
            using var file = TagLib.File.Create(filePath);
            var metadata = TagLibMapping.ToAudioMetadata(file);
            var tagFormat = TagLibMapping.DetectTagFormat(file);

            string? warning = null;
            if (file.PossiblyCorrupt)
            {
                warning = "The tag data looks damaged; the file was read but writing it is not recommended.";
                _log.Warn($"Possibly corrupt tag data in '{filePath}': {string.Join("; ", file.CorruptionReasons)}");
            }

            return MetadataReadResult.Ok(metadata, tagFormat, warning);
        }
        catch (TagLib.CorruptFileException ex)
        {
            _log.Error($"Corrupt audio file '{filePath}'.", ex);
            return MetadataReadResult.Failure("The tag structure of this file is corrupt and cannot be read safely.");
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            _log.Error($"Unsupported audio format in '{filePath}'.", ex);
            return MetadataReadResult.Failure(
                $"This file type is not supported. Supported formats: {AudioFormatCatalog.DisplayList}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"Could not read '{filePath}'.", ex);
            return MetadataReadResult.Failure(UserFriendlyErrors.Describe(ex));
        }
    }
}
