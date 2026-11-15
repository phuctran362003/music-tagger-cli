using MusicTagger.Core.Contracts;
using MusicTagger.Core.Errors;
using MusicTagger.Core.Models;

namespace MusicTagger.Infrastructure.AudioTags;

/// <summary>
/// Writes audio metadata safely for every supported container: the tag is written to a
/// temporary copy next to the original, the copy is verified, and only then does it replace the
/// original file. The audio stream is never re-encoded or moved, and the original file stays
/// intact when anything fails (SRS FR-011, NFR-002).
/// </summary>
/// <remarks>
/// TagLib# writes in place, so the temporary-copy strategy is what provides the atomic
/// replacement. The temp file always lives in the same directory (and keeps the original
/// extension, because the tag library detects the container from it) so the rename cannot cross
/// a volume boundary.
/// </remarks>
public sealed class TagLibAudioMetadataWriter : IAudioMetadataWriter
{
    private readonly IAppLog _log;

    public TagLibAudioMetadataWriter(IAppLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<MetadataWriteResult> WriteAsync(
        string filePath,
        MetadataUpdate update,
        Id3WriteVersion targetVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(update);

        var fullPath = Path.GetFullPath(filePath);

        await TagLibGate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task
                .Run(() => WriteCore(fullPath, update, targetVersion, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            TagLibGate.Semaphore.Release();
        }
    }

    private MetadataWriteResult WriteCore(
        string fullPath,
        MetadataUpdate update,
        Id3WriteVersion targetVersion,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return MetadataWriteResult.Failure("The file no longer exists.");
        }

        if (!AudioFormatCatalog.TryGetForPath(fullPath, out var format))
        {
            return MetadataWriteResult.Failure(
                $"This file type is not supported. Supported formats: {AudioFormatCatalog.DisplayList}.");
        }

        if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
        {
            return MetadataWriteResult.Failure("The file is read-only.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            return MetadataWriteResult.Failure("The file path has no parent folder.");
        }

        // The temporary copy lives in the same folder and keeps the original extension, because
        // TagLib# picks the container from the file name.
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(fullPath)}.mttmp-{Guid.NewGuid():N}{Path.GetExtension(fullPath)}");
        var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
        var unixMode = TryGetUnixFileMode(fullPath);

        var previousVersion = TagLib.Id3v2.Tag.DefaultVersion;
        var previousForceVersion = TagLib.Id3v2.Tag.ForceDefaultVersion;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Work on a copy so the original can never be lost.
            File.Copy(fullPath, tempPath, overwrite: false);

            var before = ReadMetadata(tempPath);
            var expected = update.ApplyTo(before);

            // 2. Write the tag on the copy.
            using (var file = TagLib.File.Create(tempPath))
            {
                if (!file.Writeable)
                {
                    return MetadataWriteResult.Failure("The file is not writable (it may be locked by another program).");
                }

                if (format.HonorsId3WriteVersion)
                {
                    TagLib.Id3v2.Tag.DefaultVersion = targetVersion.ToTagLibVersion();
                    TagLib.Id3v2.Tag.ForceDefaultVersion = true;
                }

                var tagType = TagLibMapping.PrimaryTagType(format.TagSystem);
                if (file.GetTag(tagType, true) is not { } tag)
                {
                    return MetadataWriteResult.Failure(
                        $"A {format.DisplayName} tag could not be created for this file.");
                }

                if (tag is TagLib.Id3v2.Tag id3v2)
                {
                    TagLibMapping.MigrateLegacyTag(file, id3v2);

                    // WAV/AIFF: fold LIST/INFO and IDVX chunks into ID3v2 and drop them, so the
                    // file keeps one tag that the read-back verification can actually check.
                    if (AudioFormatCatalog.UsesRiffChunks(format))
                    {
                        TagLibMapping.MigrateRiffChunkTags(file, id3v2);
                    }
                }

                TagLibMapping.ApplyEdits(tag, update);
                file.Save();
            }

            // 3. Verify the copy before replacing the original.
            var afterWrite = ReadMetadata(tempPath);
            var verification = VerifyEditedFields(update, expected, afterWrite);
            if (verification is not null)
            {
                return MetadataWriteResult.Failure(
                    "The new metadata could not be verified, so the original file was left unchanged. " + verification);
            }

            cancellationToken.ThrowIfCancellationRequested();
            TrySetLastWriteTimeUtc(tempPath, lastWriteUtc);
            TrySetUnixFileMode(tempPath, unixMode);

            // 4. Swap the verified copy in.
            ReplaceFile(tempPath, fullPath);

            // 5. Read back from the real file to be sure what is on disk now.
            var finalMetadata = ReadMetadata(fullPath);
            var finalVerification = VerifyEditedFields(update, expected, finalMetadata);
            if (finalVerification is not null)
            {
                return MetadataWriteResult.Failure(
                    "Metadata was written but the read-back check failed. Restore the file from the backup. " +
                    finalVerification);
            }

            return MetadataWriteResult.Ok(finalMetadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TagLib.CorruptFileException ex)
        {
            _log.Error($"Corrupt tag data in '{fullPath}'; the file was not modified.", ex);
            return MetadataWriteResult.Failure("The tag structure of this file is corrupt; the file was not modified.");
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            _log.Error($"Unsupported audio format in '{fullPath}'.", ex);
            return MetadataWriteResult.Failure(
                $"This file type is not supported. Supported formats: {AudioFormatCatalog.DisplayList}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"Could not write metadata to '{fullPath}'; the original file was kept.", ex);
            return MetadataWriteResult.Failure(UserFriendlyErrors.Describe(ex));
        }
        finally
        {
            TagLib.Id3v2.Tag.DefaultVersion = previousVersion;
            TagLib.Id3v2.Tag.ForceDefaultVersion = previousForceVersion;
            DeleteTemporaryFile(tempPath);
        }
    }

    private static AudioMetadata ReadMetadata(string path)
    {
        using var file = TagLib.File.Create(path);
        return TagLibMapping.ToAudioMetadata(file);
    }

    private static string? VerifyEditedFields(MetadataUpdate update, AudioMetadata expected, AudioMetadata actual)
    {
        foreach (var (field, edit) in update.Edits)
        {
            if (edit.Kind == FieldEditKind.Keep)
            {
                continue;
            }

            var expectedValue = expected.GetValue(field);
            var actualValue = actual.GetValue(field);
            if (!MetadataText.AreEquivalent(expectedValue, actualValue))
            {
                return $"Field '{MetadataFieldCatalog.DisplayName(field)}' was stored as " +
                       $"'{FieldChange.Display(actualValue)}' but '{FieldChange.Display(expectedValue)}' was requested.";
            }
        }

        return null;
    }

    private static void ReplaceFile(string tempPath, string targetPath)
    {
        try
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or IOException or UnauthorizedAccessException)
        {
            // Same-directory rename fallback: still atomic on every platform we support.
            File.Move(tempPath, targetPath, overwrite: true);
        }
    }

    private UnixFileMode? TryGetUnixFileMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return File.GetUnixFileMode(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _log.Warn($"Unix permissions of '{path}' could not be read: {ex.Message}");
            return null;
        }
    }

    private void TrySetUnixFileMode(string path, UnixFileMode? mode)
    {
        if (mode is null || OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, mode.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _log.Warn($"Unix permissions could not be re-applied to '{path}': {ex.Message}");
        }
    }

    private void TrySetLastWriteTimeUtc(string path, DateTime lastWriteUtc)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, lastWriteUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            _log.Warn($"The timestamp of '{path}' could not be restored: {ex.Message}");
        }
    }

    private void DeleteTemporaryFile(string tempPath)
    {
        if (!File.Exists(tempPath))
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Temporary file '{tempPath}' could not be removed: {ex.Message}");
        }
    }
}
