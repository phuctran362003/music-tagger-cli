using System.Globalization;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;

namespace MusicTagger.Infrastructure.AudioTags;

/// <summary>Maps between the core metadata model and TagLib# objects.</summary>
internal static class TagLibMapping
{
    public static AudioMetadata ToAudioMetadata(TagLib.File file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var tag = file.Tag;

        return new AudioMetadata
        {
            Title = MetadataText.Normalize(tag.Title),
            Artist = MetadataText.FromList(tag.Performers),
            Album = MetadataText.Normalize(tag.Album),
            AlbumArtist = MetadataText.FromList(tag.AlbumArtists),
            Genre = MetadataText.FromList(tag.Genres),
            Year = tag.Year == 0 ? null : tag.Year.ToString(CultureInfo.InvariantCulture),
            TrackNumber = FormatPosition(tag.Track, tag.TrackCount),
            DiscNumber = FormatPosition(tag.Disc, tag.DiscCount),
            Composer = MetadataText.FromList(tag.Composers),
            Comment = MetadataText.Normalize(tag.Comment),
        };
    }

    public static Id3Version DetectVersion(TagLib.File file)
    {
        ArgumentNullException.ThrowIfNull(file);

        // TagLib# exposes an empty ID3v2 tag object for files without any tag, so the
        // on-disk tag types are the only reliable source here.
        var onDisk = file.TagTypesOnDisk;

        if ((onDisk & TagLib.TagTypes.Id3v2) != 0 &&
            file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3v2)
        {
            return id3v2.Version switch
            {
                2 => Id3Version.Id3v22,
                3 => Id3Version.Id3v23,
                4 => Id3Version.Id3v24,
                _ => Id3Version.Unknown,
            };
        }

        if ((onDisk & TagLib.TagTypes.Id3v1) != 0)
        {
            return Id3Version.Id3v1;
        }

        return Id3Version.Unknown;
    }

    /// <summary>
    /// Applies the edits to an ID3v2 tag. Only the requested fields are touched; every
    /// other frame (including pictures and unknown frames) is left alone.
    /// </summary>
    public static void ApplyEdits(TagLib.Id3v2.Tag tag, MetadataUpdate update)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(update);

        foreach (var (field, edit) in update.Edits)
        {
            if (edit.Kind == FieldEditKind.Keep)
            {
                continue;
            }

            var value = edit.Kind == FieldEditKind.Clear ? null : MetadataText.Normalize(edit.Value);
            var values = MetadataText.ToList(value);

            switch (field)
            {
                case MetadataField.Title:
                    tag.Title = value;
                    break;
                case MetadataField.Artist:
                    tag.Performers = values;
                    break;
                case MetadataField.Album:
                    tag.Album = value;
                    break;
                case MetadataField.AlbumArtist:
                    tag.AlbumArtists = values;
                    break;
                case MetadataField.Genre:
                    tag.Genres = values;
                    break;
                case MetadataField.Composer:
                    tag.Composers = values;
                    break;
                case MetadataField.Comment:
                    tag.Comment = value;
                    break;
                case MetadataField.Year:
                    tag.Year = ParseYear(value);
                    break;
                case MetadataField.TrackNumber:
                    ApplyPosition(value, out var track, out var trackCount);
                    tag.Track = track;
                    tag.TrackCount = trackCount;
                    break;
                case MetadataField.DiscNumber:
                    ApplyPosition(value, out var disc, out var discCount);
                    tag.Disc = disc;
                    tag.DiscCount = discCount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(update), field, "Unsupported metadata field.");
            }
        }
    }

    /// <summary>
    /// Removes a legacy ID3v1 tag so that two conflicting tags never exist side by side
    /// (SRS 7.14). Values that only exist in the ID3v1 tag are copied to ID3v2 first.
    /// </summary>
    public static void MigrateLegacyTag(TagLib.File file, TagLib.Id3v2.Tag target)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(target);

        var legacy = file.GetTag(TagLib.TagTypes.Id3v1, false);
        if (legacy is null)
        {
            return;
        }

        legacy.CopyTo(target, overwrite: false);
        file.RemoveTags(TagLib.TagTypes.Id3v1);
    }

    private static string? FormatPosition(uint number, uint count)
    {
        if (number == 0)
        {
            return null;
        }

        return MetadataValidator.FormatPosition(number, count == 0 ? null : count);
    }

    private static uint ParseYear(string? value)
    {
        if (value is null)
        {
            return 0;
        }

        return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            ? year
            : 0;
    }

    private static void ApplyPosition(string? value, out uint number, out uint count)
    {
        number = 0;
        count = 0;

        if (value is null)
        {
            return;
        }

        if (MetadataValidator.TryParsePosition(value, out var parsedNumber, out var parsedTotal))
        {
            number = parsedNumber;
            count = parsedTotal ?? 0;
        }
    }
}
