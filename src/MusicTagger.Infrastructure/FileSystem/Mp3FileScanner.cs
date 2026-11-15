using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;

namespace MusicTagger.Infrastructure.FileSystem;

/// <summary>
/// Scans a folder for <c>.mp3</c> files (case insensitive). Symlinks and reparse points are
/// never followed, so a scan can neither loop forever nor leave the root folder (SRS FR-003).
/// </summary>
public sealed class Mp3FileScanner : IMp3FileScanner
{
    private static readonly string[] ForbiddenExtensions = [".tmp", ".mttmp"];

    /// <summary>Marker used by the safe writer for its temporary copy; never treated as a music file.</summary>
    private const string TemporaryFileMarker = ".mttmp-";

    private readonly IAppLog _log;

    public Mp3FileScanner(IAppLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<ScanResult> ScanAsync(string rootFolder, bool recursive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);

        return Task.Run(() => Scan(rootFolder, recursive, cancellationToken), cancellationToken);
    }

    private ScanResult Scan(string rootFolder, bool recursive, CancellationToken cancellationToken)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootFolder));
        var issues = new List<string>();
        var files = new List<ScannedFile>();

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Folder not found: {root}");
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = MatchType.Simple,
            ReturnSpecialDirectories = false,
        };

        var skipped = 0;
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(root, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsMp3File(filePath))
                {
                    continue;
                }

                string relativePath;
                try
                {
                    relativePath = PathSafety.ToRelativePath(root, filePath);
                }
                catch (ArgumentException)
                {
                    skipped++;
                    continue;
                }

                // Defensive check: never accept a file that resolves outside the root.
                if (!PathSafety.TryResolveContained(root, relativePath, out var resolved, out var error) ||
                    !PathSafety.IsInside(root, Path.GetFullPath(resolved)))
                {
                    issues.Add($"Skipped '{relativePath}': {error ?? "it resolves outside the selected folder."}");
                    skipped++;
                    continue;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(filePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    issues.Add($"Skipped '{relativePath}': {ex.Message}");
                    skipped++;
                    continue;
                }

                files.Add(new ScannedFile(
                    filePath,
                    relativePath,
                    info.Exists ? info.Length : 0,
                    info.Exists ? info.LastWriteTimeUtc : DateTime.UnixEpoch));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Error($"Access denied while scanning '{root}'.", ex);
            issues.Add($"Access denied while scanning: {ex.Message}");
        }

        if (skipped > 0)
        {
            _log.Warn($"Scan of '{root}' skipped {skipped} entries (symlinks or inaccessible items).");
        }

        files.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));

        return new ScanResult(root, recursive, files, issues);
    }

    private static bool IsMp3File(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);

        if (fileName.Contains(TemporaryFileMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var forbidden in ForbiddenExtensions)
        {
            if (fileName.EndsWith(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
