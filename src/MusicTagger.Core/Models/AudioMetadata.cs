namespace MusicTagger.Core.Models;

/// <summary>Values that the application supports per metadata field.</summary>
public sealed record AudioMetadata
{
    public static AudioMetadata Empty { get; } = new();

    public string? Title { get; init; }

    public string? Artist { get; init; }

    public string? Album { get; init; }

    public string? AlbumArtist { get; init; }

    public string? Genre { get; init; }

    public string? Year { get; init; }

    public string? TrackNumber { get; init; }

    public string? DiscNumber { get; init; }

    public string? Composer { get; init; }

    public string? Comment { get; init; }

    public string? GetValue(MetadataField field) => field switch
    {
        MetadataField.Title => Title,
        MetadataField.Artist => Artist,
        MetadataField.Album => Album,
        MetadataField.AlbumArtist => AlbumArtist,
        MetadataField.Genre => Genre,
        MetadataField.Year => Year,
        MetadataField.TrackNumber => TrackNumber,
        MetadataField.DiscNumber => DiscNumber,
        MetadataField.Composer => Composer,
        MetadataField.Comment => Comment,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported metadata field."),
    };

    public AudioMetadata WithValue(MetadataField field, string? value)
    {
        var normalized = MetadataText.Normalize(value);
        return field switch
        {
            MetadataField.Title => this with { Title = normalized },
            MetadataField.Artist => this with { Artist = normalized },
            MetadataField.Album => this with { Album = normalized },
            MetadataField.AlbumArtist => this with { AlbumArtist = normalized },
            MetadataField.Genre => this with { Genre = normalized },
            MetadataField.Year => this with { Year = normalized },
            MetadataField.TrackNumber => this with { TrackNumber = normalized },
            MetadataField.DiscNumber => this with { DiscNumber = normalized },
            MetadataField.Composer => this with { Composer = normalized },
            MetadataField.Comment => this with { Comment = normalized },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported metadata field."),
        };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAnyValue => EnumerateValues().Any(pair => pair.Value is not null);

    public IEnumerable<(MetadataField Field, string? Value)> EnumerateValues()
    {
        foreach (var descriptor in MetadataFieldCatalog.All)
        {
            yield return (descriptor.Field, GetValue(descriptor.Field));
        }
    }
}
