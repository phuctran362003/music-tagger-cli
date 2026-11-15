using MusicTagger.Core.Models;
using MusicTagger.IntegrationTests.Support;
using Xunit;

namespace MusicTagger.IntegrationTests;

/// <summary>
/// Round trips on every supported container, not just MP3: write with the application writer,
/// read back with the application reader, and prove the audio bytes are still there.
/// </summary>
public sealed class MultiFormatTagWriteTests
{
    private const string Title = "Đường Đến Ngày Vinh Quang";
    private const string Artist = "Trịnh Công Sơn";
    private const string Album = "Những Bản Tình Ca";

    private static readonly Dictionary<string, Func<byte[]>> Fixtures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".wav"] = AudioFixtures.Wav,
        [".aiff"] = AudioFixtures.Aiff,
        [".aif"] = AudioFixtures.Aiff,
        [".flac"] = AudioFixtures.Flac,
        [".m4a"] = AudioFixtures.M4a,
        [".m4b"] = AudioFixtures.M4a,
        [".aac"] = AudioFixtures.Aac,
        [".ogg"] = AudioFixtures.Ogg,
        [".oga"] = AudioFixtures.Ogg,
        [".opus"] = AudioFixtures.Opus,
    };

    public static TheoryData<string> Extensions => new(Fixtures.Keys);

    [Theory]
    [MemberData(nameof(Extensions))]
    public async Task Writing_vietnamese_metadata_round_trips_and_keeps_the_audio_bytes(string extension)
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("track" + extension, Fixtures[extension]());
        var audioBefore = AudioStream.HashAudio(file);

        var update = MetadataUpdate.Empty
            .WithSet(MetadataField.Title, Title)
            .WithSet(MetadataField.Artist, Artist)
            .WithSet(MetadataField.Album, Album)
            .WithSet(MetadataField.Year, "1972")
            .WithSet(MetadataField.TrackNumber, "3");

        var write = await harness.Writer.WriteAsync(file, update, Id3WriteVersion.V23);
        Assert.True(write.Success, write.ErrorMessage);

        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Equal(Title, read.Metadata.Title);
        Assert.Equal(Artist, read.Metadata.Artist);
        Assert.Equal(Album, read.Metadata.Album);
        Assert.Equal("1972", read.Metadata.Year);
        Assert.Equal("3", read.Metadata.TrackNumber);

        var expected = ExpectedTagFormat(extension);
        Assert.Equal(expected, read.TagFormat);

        AssertAudioPreserved(file, extension, audioBefore);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.mttmp-*"));
    }

    [Theory]
    [MemberData(nameof(Extensions))]
    public async Task Clearing_a_field_removes_it_for_every_format(string extension)
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("clear" + extension, Fixtures[extension]());

        var set = MetadataUpdate.Empty.WithSet(MetadataField.Comment, "temporary comment");
        var first = await harness.Writer.WriteAsync(file, set, Id3WriteVersion.V23);
        Assert.True(first.Success, first.ErrorMessage);

        var cleared = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithClear(MetadataField.Comment),
            Id3WriteVersion.V23);
        Assert.True(cleared.Success, cleared.ErrorMessage);

        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Null(read.Metadata.Comment);
    }

    [Fact]
    public async Task Id3_version_setting_applies_to_aiff_but_wav_always_stores_v24()
    {
        using var harness = new TestHarness();

        var aiff = harness.Workspace.CreateFile("v24.aiff", AudioFixtures.Aiff());
        var aiffWrite = await harness.Writer.WriteAsync(
            aiff,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, Title),
            Id3WriteVersion.V24);

        Assert.True(aiffWrite.Success, aiffWrite.ErrorMessage);
        var aiffRead = await harness.Reader.ReadAsync(aiff);
        Assert.True(aiffRead.Success, aiffRead.ErrorMessage);
        Assert.Equal(TagFormat.Id3v24, aiffRead.TagFormat);

        // The tag library always renders ID3v2.4 into a WAV "id3 " chunk, so v2.3 is not available.
        foreach (var requested in new[] { Id3WriteVersion.V23, Id3WriteVersion.V24 })
        {
            var wav = harness.Workspace.CreateFile($"wav-{requested}.wav", AudioFixtures.Wav());
            var write = await harness.Writer.WriteAsync(
                wav,
                MetadataUpdate.Empty.WithSet(MetadataField.Title, Title),
                requested);

            Assert.True(write.Success, write.ErrorMessage);
            var read = await harness.Reader.ReadAsync(wav);
            Assert.True(read.Success, read.ErrorMessage);
            Assert.Equal(TagFormat.Id3v24, read.TagFormat);
            Assert.Equal(Title, read.Metadata.Title);
        }
    }

    [Fact]
    public async Task Riff_info_metadata_is_kept_when_a_wav_file_is_edited()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("foreign.wav", AudioFixtures.WavWithRiffInfo());

        var before = await harness.Reader.ReadAsync(file);
        Assert.True(before.Success, before.ErrorMessage);
        Assert.Equal(TagFormat.RiffInfo, before.TagFormat);
        Assert.Equal("Old title", before.Metadata.Title);

        var write = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Album, Album),
            Id3WriteVersion.V23);

        Assert.True(write.Success, write.ErrorMessage);

        var after = await harness.Reader.ReadAsync(file);
        Assert.True(after.Success, after.ErrorMessage);
        Assert.Equal(TagFormat.Id3v24, after.TagFormat);
        Assert.Equal("Old title", after.Metadata.Title);
        Assert.Equal(Album, after.Metadata.Album);
        Assert.True(
            ContainsSequence(File.ReadAllBytes(file), AudioFixtures.Payload),
            "The audio payload is no longer present in the WAV file.");
    }

    [Fact]
    public async Task Vorbis_and_mp4_formats_are_not_affected_by_the_id3_version_setting()
    {
        using var harness = new TestHarness();

        foreach (var extension in new[] { ".flac", ".ogg", ".opus", ".m4a" })
        {
            var file = harness.Workspace.CreateFile("both" + extension, Fixtures[extension]());
            var write = await harness.Writer.WriteAsync(
                file,
                MetadataUpdate.Empty.WithSet(MetadataField.Title, Title),
                Id3WriteVersion.V24);

            Assert.True(write.Success, write.ErrorMessage);

            var read = await harness.Reader.ReadAsync(file);
            Assert.Equal(ExpectedTagFormat(extension), read.TagFormat);
            Assert.Equal(Title, read.Metadata.Title);
        }
    }

    [Fact]
    public async Task Unsupported_file_type_is_rejected_without_touching_the_file()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("notes.txt", "not audio"u8.ToArray());
        var before = File.ReadAllBytes(file);

        var write = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, Title),
            Id3WriteVersion.V23);

        Assert.False(write.Success);
        Assert.Contains("not supported", write.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(file));
    }

    private static TagFormat ExpectedTagFormat(string extension) => extension.ToLowerInvariant() switch
    {
        ".flac" or ".ogg" or ".oga" or ".opus" => TagFormat.VorbisComment,
        ".m4a" or ".m4b" => TagFormat.Mp4Atom,

        // The tag library renders ID3v2.4 into the WAV "id3 " chunk whatever version is asked for.
        ".wav" => TagFormat.Id3v24,
        _ => TagFormat.Id3v23,
    };

    private static void AssertAudioPreserved(string filePath, string extension, string audioHashBefore)
    {
        if (extension.Equals(".aac", StringComparison.OrdinalIgnoreCase))
        {
            // The raw ADTS frames sit after the ID3v2 tag, so the shared extractor works here.
            Assert.Equal(audioHashBefore, AudioStream.HashAudio(filePath));
            return;
        }

        var bytes = File.ReadAllBytes(filePath);
        Assert.True(
            ContainsSequence(bytes, AudioFixtures.Payload),
            $"The audio payload is no longer present in the {extension} file.");
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
