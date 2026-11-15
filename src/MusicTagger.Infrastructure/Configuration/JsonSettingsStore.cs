using System.Text.Json;
using System.Text.Json.Serialization;
using MusicTagger.Core.Contracts;
using MusicTagger.Core.Models;

namespace MusicTagger.Infrastructure.Configuration;

/// <summary>Reads and writes <see cref="AppSettings"/> as JSON in the application data directory.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _settingsFilePath;
    private readonly IAppLog _log;

    public JsonSettingsStore(string settingsFilePath, IAppLog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        _settingsFilePath = settingsFilePath;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public string SettingsFilePath => _settingsFilePath;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            var settings = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, Options, cancellationToken)
                .ConfigureAwait(false);

            if (settings is null)
            {
                _log.Warn($"Settings file '{_settingsFilePath}' is empty; using defaults.");
                return AppSettings.Default;
            }

            var errors = settings.Validate();
            if (errors.Count > 0)
            {
                _log.Warn($"Settings file '{_settingsFilePath}' is invalid: {string.Join(" ", errors)} Using defaults.");
                return AppSettings.Default;
            }

            return settings;
        }
        catch (JsonException ex)
        {
            _log.Error($"Settings file '{_settingsFilePath}' could not be parsed; defaults are used.", ex);
            return AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"Settings file '{_settingsFilePath}' could not be read; defaults are used.", ex);
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(settings));
        }

        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _settingsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _settingsFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                TryDelete(tempPath);
            }
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Temporary settings file '{path}' could not be removed: {ex.Message}");
        }
    }
}
