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

    /// <summary>Tag system that the writer has to use for a container.</summary>
    public static TagLib.TagTypes PrimaryTagType(AudioTagSystem tagSystem) => tagSystem switch
    {
        AudioTagSystem.VorbisComment => TagLib.TagTypes.Xiph,
        AudioTagSystem.Mp4Atom => TagLib.TagTypes.Apple,
        _ => TagLib.TagTypes.Id3v2,
    };

    /// <summary>
    /// Describes the tag that is really stored in the file. TagLib# exposes an empty tag object
    /// for untagged files, so the on-disk tag types are the only reliable source here.
    /// </summary>
    public static TagFormat DetectTagFormat(TagLib.File file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var onDisk = file.TagTypesOnDisk;

        if ((onDisk & TagLib.TagTypes.Id3v2) != 0 &&
            file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3v2)
        {
            return id3v2.Version switch
            {
                2 => TagFormat.Id3v22,
                3 => TagFormat.Id3v23,
                4 => TagFormat.Id3v24,
                _ => TagFormat.Unknown,
            };
        }

        if ((onDisk & TagLib.TagTypes.Xiph) != 0)
        {
            return TagFormat.VorbisComment;
        }

        if ((onDisk & TagLib.TagTypes.Apple) != 0)
        {
            return TagFormat.Mp4Atom;
        }

        if ((onDisk & TagLib.TagTypes.Id3v1) != 0)
        {
            return TagFormat.Id3v1;
        }

        if ((onDisk & TagLib.TagTypes.RiffInfo) != 0)
        {
            return TagFormat.RiffInfo;
        }

        // Anything else means the file carries no tag we can report on (FLAC files, for
        // instance, always have a metadata block but no comment yet).
        return TagFormat.NoTag;
    }

    /// <summary>
    /// Applies the edits to the tag of the file. Only the requested fields are touched; every
    /// other frame (including pictures and unknown frames) is left alone.
    /// </summary>
    public static void ApplyEdits(TagLib.Tag tag, MetadataUpdate update)
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

    /// <summary>
    /// Consolidates the extra RIFF style tags a WAV/AIFF container can carry (LIST/INFO,
    /// IDVX) into the ID3v2 tag and removes them.
    /// </summary>
    /// <remarks>
    /// TagLib# rebuilds those chunks from the combined tag object while saving, which makes a
    /// cleared field reappear and leaves two disagreeing copies of the same value. Copying the
    /// values that only exist there into ID3v2 keeps the user's metadata, and removing the
    /// chunks leaves exactly one tag behind, which is also the tag the verification reads back.
    /// </remarks>
    public static void MigrateRiffChunkTags(TagLib.File file, TagLib.Id3v2.Tag target)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(target);

        const TagLib.TagTypes riffTags =
            TagLib.TagTypes.RiffInfo | TagLib.TagTypes.MovieId | TagLib.TagTypes.DivX;

        if (file.GetTag(TagLib.TagTypes.RiffInfo, false) is { } info)
        {
            CopyMissingValues(info, target);
        }

        // Unconditional: the tag library also holds these chunks in memory for a file that has
        // none on disk yet, and it would write them back on the next save.
        file.RemoveTags(riffTags);
    }

    /// <summary>
    /// Copies the fields that only exist in the source tag; existing target values always win,
    /// and empty source values never overwrite anything.
    /// </summary>
    private static void CopyMissingValues(TagLib.Tag source, TagLib.Id3v2.Tag target)
    {
        if (string.IsNullOrEmpty(target.Title) && !string.IsNullOrEmpty(source.Title))
        {
            target.Title = source.Title;
        }

        if (target.Performers.Length == 0 && source.Performers.Length > 0)
        {
            target.Performers = source.Performers;
        }

        if (string.IsNullOrEmpty(target.Album) && !string.IsNullOrEmpty(source.Album))
        {
            target.Album = source.Album;
        }

        if (target.AlbumArtists.Length == 0 && source.AlbumArtists.Length > 0)
        {
            target.AlbumArtists = source.AlbumArtists;
        }

        if (target.Genres.Length == 0 && source.Genres.Length > 0)
        {
            target.Genres = source.Genres;
        }

        if (target.Composers.Length == 0 && source.Composers.Length > 0)
        {
            target.Composers = source.Composers;
        }

        if (string.IsNullOrEmpty(target.Comment) && !string.IsNullOrEmpty(source.Comment))
        {
            target.Comment = source.Comment;
        }

        if (target.Year == 0 && source.Year != 0)
        {
            target.Year = source.Year;
        }

        if (target.Track == 0 && source.Track != 0)
        {
            target.Track = source.Track;
            target.TrackCount = source.TrackCount;
        }

        if (target.Disc == 0 && source.Disc != 0)
        {
            target.Disc = source.Disc;
            target.DiscCount = source.DiscCount;
        }
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
