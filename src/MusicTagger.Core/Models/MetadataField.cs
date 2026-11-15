namespace MusicTagger.Core.Models;

/// <summary>
/// Metadata fields that the MVP can read and write. Every field is single valued
/// so that a Keep/Set/Clear edit has an unambiguous meaning.
/// </summary>
public enum MetadataField
{
    Title = 0,
    Artist = 1,
    Album = 2,
    AlbumArtist = 3,
    Genre = 4,
    Year = 5,
    TrackNumber = 6,
    DiscNumber = 7,
    Composer = 8,
    Comment = 9,
}

/// <summary>How a field value has to be validated.</summary>
public enum FieldValueKind
{
    FreeText = 0,
    Year = 1,
    Position = 2,
}

public sealed record MetadataFieldDescriptor(
    MetadataField Field,
    string DisplayName,
    FieldValueKind Kind,
    string Hint);

public static class MetadataFieldCatalog
{
    private static readonly MetadataFieldDescriptor[] Descriptors =
    [
        new(MetadataField.Title, "Title", FieldValueKind.FreeText, "Song title"),
        new(MetadataField.Artist, "Artist", FieldValueKind.FreeText, "Performing artist"),
        new(MetadataField.Album, "Album", FieldValueKind.FreeText, "Album name"),
        new(MetadataField.AlbumArtist, "Album artist", FieldValueKind.FreeText, "Album level artist"),
        new(MetadataField.Genre, "Genre", FieldValueKind.FreeText, "Music genre"),
        new(MetadataField.Year, "Year", FieldValueKind.Year, "Four digit release year, e.g. 1998"),
        new(MetadataField.TrackNumber, "Track number", FieldValueKind.Position, "Track number, e.g. 3 or 3/12"),
        new(MetadataField.DiscNumber, "Disc number", FieldValueKind.Position, "Disc number, e.g. 1 or 1/2"),
        new(MetadataField.Composer, "Composer", FieldValueKind.FreeText, "Composer"),
        new(MetadataField.Comment, "Comment", FieldValueKind.FreeText, "Free form comment"),
    ];

    public static IReadOnlyList<MetadataFieldDescriptor> All => Descriptors;

    /// <summary>Fields the SRS requires as a minimum.</summary>
    public static IReadOnlyList<MetadataField> Mandatory { get; } =
        [MetadataField.Title, MetadataField.Artist, MetadataField.Album];

    public static MetadataFieldDescriptor Get(MetadataField field) => Descriptors[(int)field];

    public static string DisplayName(MetadataField field) => Descriptors[(int)field].DisplayName;

    public static bool TryParse(string? token, out MetadataField field)
    {
        field = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var candidate = token.Trim();
        foreach (var descriptor in Descriptors)
        {
            if (string.Equals(descriptor.Field.ToString(), candidate, StringComparison.OrdinalIgnoreCase))
            {
                field = descriptor.Field;
                return true;
            }
        }

        return Enum.TryParse(candidate, ignoreCase: true, out field) && Enum.IsDefined(field);
    }
}
