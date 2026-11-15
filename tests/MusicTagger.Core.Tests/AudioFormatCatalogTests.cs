using MusicTagger.Core.Models;
using Xunit;

namespace MusicTagger.Core.Tests;

/// <summary>
/// The format registry is the single source of truth for what the application offers to edit,
/// so its contract is pinned down here.
/// </summary>
public sealed class AudioFormatCatalogTests
{
    [Fact]
    public void Every_supported_extension_is_unique_and_complete()
    {
        var extensions = AudioFormatCatalog.Extensions;

        Assert.Equal(11, extensions.Count);
        Assert.Equal(extensions.Count, extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var expected in new[] { ".mp3", ".flac", ".m4a", ".m4b", ".aac", ".ogg", ".oga", ".opus", ".wav", ".aiff", ".aif" })
        {
            Assert.Contains(expected, extensions);
        }
    }

    [Theory]
    [InlineData("C:\\music\\song.mp3", AudioTagSystem.Id3)]
    [InlineData("C:\\music\\song.flac", AudioTagSystem.VorbisComment)]
    [InlineData("C:\\music\\song.m4a", AudioTagSystem.Mp4Atom)]
    [InlineData("C:\\music\\song.m4b", AudioTagSystem.Mp4Atom)]
    [InlineData("C:\\music\\song.aac", AudioTagSystem.Id3)]
    [InlineData("C:\\music\\song.ogg", AudioTagSystem.VorbisComment)]
    [InlineData("C:\\music\\song.oga", AudioTagSystem.VorbisComment)]
    [InlineData("C:\\music\\song.opus", AudioTagSystem.VorbisComment)]
    [InlineData("C:\\music\\song.wav", AudioTagSystem.Id3)]
    [InlineData("C:\\music\\song.aiff", AudioTagSystem.Id3)]
    [InlineData("C:\\music\\song.aif", AudioTagSystem.Id3)]
    public void Every_catalogued_format_has_the_expected_tag_system(string path, AudioTagSystem expected)
    {
        Assert.True(AudioFormatCatalog.TryGetForPath(path, out var format));
        Assert.Equal(expected, format.TagSystem);
        Assert.Equal(expected, AudioFormatCatalog.All.Single(f => f.Extension == format.Extension).TagSystem);
    }

    [Theory]
    [InlineData("song.txt")]
    [InlineData("song.wma")]
    [InlineData("song.wave")]
    [InlineData("song.ape")]
    [InlineData("song.m4v")]
    [InlineData("song")]
    [InlineData("")]
    public void Unsupported_paths_are_rejected(string path)
    {
        Assert.False(AudioFormatCatalog.IsSupportedPath(path));
        Assert.False(AudioFormatCatalog.TryGetForPath(path, out _));
    }

    [Fact]
    public void Extension_matching_ignores_case()
    {
        foreach (var extension in AudioFormatCatalog.Extensions)
        {
            Assert.True(AudioFormatCatalog.IsSupportedPath("track" + extension.ToUpperInvariant()));
            Assert.True(AudioFormatCatalog.IsSupportedPath("track" + extension.ToLowerInvariant()));
        }
    }

    [Fact]
    public void Only_id3_containers_react_to_the_id3_write_version_setting()
    {
        foreach (var format in AudioFormatCatalog.All)
        {
            Assert.Equal(
                format.TagSystem == AudioTagSystem.Id3 && format.Extension != ".wav",
                format.HonorsId3WriteVersion);
        }
    }

    [Fact]
    public void Wav_is_reported_as_fixed_at_id3v24_and_excluded_from_the_setting()
    {
        // Measured with TagLib# 2.3.0: a WAV "id3 " chunk is always written as ID3v2.4.
        Assert.Equal("WAV", AudioFormatCatalog.FixedId3v24DisplayList);
        Assert.DoesNotContain("WAV", AudioFormatCatalog.Id3FormatDisplayList.Split(", "));
        Assert.Contains("MP3", AudioFormatCatalog.Id3FormatDisplayList.Split(", "));
        Assert.Contains("AIFF", AudioFormatCatalog.Id3FormatDisplayList.Split(", "));
    }

    [Fact]
    public void Riff_chunk_containers_are_flagged()
    {
        Assert.True(AudioFormatCatalog.TryGetForPath("a.wav", out var wav));
        Assert.True(AudioFormatCatalog.TryGetForPath("a.aiff", out var aiff));
        Assert.True(AudioFormatCatalog.TryGetForPath("a.aif", out var aif));
        Assert.True(AudioFormatCatalog.TryGetForPath("a.mp3", out var mp3));
        Assert.True(AudioFormatCatalog.TryGetForPath("a.flac", out var flac));

        Assert.True(AudioFormatCatalog.UsesRiffChunks(wav));
        Assert.True(AudioFormatCatalog.UsesRiffChunks(aiff));
        Assert.True(AudioFormatCatalog.UsesRiffChunks(aif));
        Assert.False(AudioFormatCatalog.UsesRiffChunks(mp3));
        Assert.False(AudioFormatCatalog.UsesRiffChunks(flac));
    }

    [Fact]
    public void Display_lists_are_human_readable_and_free_of_duplicates()
    {
        var names = AudioFormatCatalog.DisplayList.Split(", ");

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("OGG Vorbis", names);
        Assert.All(names, name => Assert.DoesNotContain(".", name, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(TagFormat.NoTag, "none")]
    [InlineData(TagFormat.Id3v1, "ID3v1")]
    [InlineData(TagFormat.Id3v22, "ID3v2.2")]
    [InlineData(TagFormat.Id3v23, "ID3v2.3")]
    [InlineData(TagFormat.Id3v24, "ID3v2.4")]
    [InlineData(TagFormat.VorbisComment, "Vorbis comment")]
    [InlineData(TagFormat.Mp4Atom, "MP4 atoms")]
    [InlineData(TagFormat.RiffInfo, "RIFF INFO")]
    [InlineData(TagFormat.Unknown, "unknown")]
    public void Tag_formats_have_ui_labels(TagFormat format, string expected)
    {
        Assert.Equal(expected, format.ToDisplayString());
    }
}
