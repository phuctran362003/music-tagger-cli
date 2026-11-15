using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;

namespace MusicTagger.Infrastructure.Backup;

/// <summary>
/// Stores backup manifests as JSON files inside a backup directory (application data by
/// default). A manifest only contains file paths and metadata: never audio bytes.
/// </summary>
public sealed class JsonBackupService : IBackupService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IAppLog _log;
    private readonly string _appVersion;

    public JsonBackupService(string backupDirectory, IAppLog log, string appVersion = "1.0.0")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? "1.0.0" : appVersion;
        BackupDirectory = Path.GetFullPath(backupDirectory);
    }

    public string BackupDirectory { get; }

    public async Task<BackupManifest> CreateBackupAsync(
        string rootFolder,
        string operation,
        IReadOnlyList<BackupCaptureItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(items);

        var createdUtc = DateTimeOffset.UtcNow;
        var manifestId = Guid.NewGuid().ToString("N");
        var fileName = $"musictagger-{Sanitize(operation)}-{createdUtc:yyyyMMdd-HHmmss}-{manifestId[..8]}.json";
        var manifestPath = Path.Combine(BackupDirectory, fileName);

        var manifest = new BackupManifest
        {
            ManifestId = manifestId,
            RootFolder = Path.GetFullPath(rootFolder),
            Operation = operation,
            CreatedUtc = createdUtc,
            AppVersion = _appVersion,
            ManifestPath = manifestPath,
            Entries = items
                .Select(item => new BackupManifestEntry
                {
                    RelativePath = item.RelativePath,
                    Metadata = item.Metadata,
                    TagFormat = item.TagFormat,
                    Status = item.Status,
                    Message = item.Message,
                })
                .ToList(),
        };

        await WriteManifestAsync(manifest, manifestPath, cancellationToken).ConfigureAwait(false);
        _log.Info($"Backup manifest created: {manifestPath} ({manifest.Entries.Count} entries).");
        return manifest;
    }

    public async Task<BackupManifest> SaveManifestAsync(BackupManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.ManifestPath))
        {
            throw new InvalidOperationException("The manifest has no target path.");
        }

        await WriteManifestAsync(manifest, manifest.ManifestPath, cancellationToken).ConfigureAwait(false);
        return manifest;
    }

    public async Task<IReadOnlyList<BackupManifestInfo>> ListManifestsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return [];
        }

        var infos = new List<BackupManifestInfo>();
        foreach (var path in Directory.EnumerateFiles(BackupDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var load = await LoadManifestAsync(path, cancellationToken).ConfigureAwait(false);
            if (load.Manifest is null)
            {
                _log.Warn($"Skipping unusable backup manifest '{path}': {string.Join(" ", load.Errors)}");
                continue;
            }

            var manifest = load.Manifest;
            infos.Add(new BackupManifestInfo(
                path,
                manifest.ManifestId,
                manifest.Operation,
                manifest.RootFolder,
                manifest.CreatedUtc,
                manifest.Entries.Count));
        }

        return infos
            .OrderByDescending(info => info.CreatedUtc)
            .ToList();
    }

    public async Task<BackupManifestLoad> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            return BackupManifestLoad.Invalid($"Backup file not found: {manifestPath}");
        }

        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer
                .DeserializeAsync<BackupManifest>(stream, Options, cancellationToken)
                .ConfigureAwait(false);

            if (manifest is null)
            {
                return BackupManifestLoad.Invalid("The backup file is empty.");
            }

            var errors = ManifestValidator.Validate(manifest);
            if (errors.Count > 0)
            {
                return new BackupManifestLoad(null, errors);
            }

            return new BackupManifestLoad(manifest with { ManifestPath = manifestPath }, []);
        }
        catch (JsonException ex)
        {
            _log.Error($"Backup file '{manifestPath}' is not valid JSON.", ex);
            return BackupManifestLoad.Invalid("The backup file is not a valid MusicTagger manifest (invalid JSON).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"Backup file '{manifestPath}' could not be read.", ex);
            return BackupManifestLoad.Invalid($"The backup file could not be read: {ex.Message}");
        }
    }

    private async Task WriteManifestAsync(BackupManifest manifest, string manifestPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, manifestPath, overwrite: true);

            // The backup must exist on disk before any file is modified.
            var info = new FileInfo(manifestPath);
            if (!info.Exists || info.Length == 0)
            {
                throw new IOException($"The backup manifest '{manifestPath}' could not be verified.");
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _log.Warn($"Temporary backup file '{tempPath}' could not be removed: {ex.Message}");
                }
            }
        }
    }

    private static string Sanitize(string value)
    {
        var chars = value
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }
}

