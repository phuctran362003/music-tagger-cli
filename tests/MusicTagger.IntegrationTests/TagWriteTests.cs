using MusicTagger.Core.Models;
using MusicTagger.IntegrationTests.Support;
using Xunit;

namespace MusicTagger.IntegrationTests;

/// <summary>
/// Round trip tests against real MP3 files: write with the application writer, read with the
/// application reader, and independently verify the bytes with a hand written ID3v2 parser.
/// </summary>
public sealed class TagWriteTests
{
    private const string VietnameseTitle = "Đường Đến Ngày Vinh Quang";
    private const string VietnameseArtist = "Trịnh Công Sơn";
    private const string VietnameseAlbum = "Những Bản Tình Ca";
    private const string VietnameseComment = "Bài hát tiếng Việt – thử nghiệm Unicode: ă â ê ô ơ ư đ";

    [Fact]
    public async Task Read_returns_empty_metadata_for_a_file_without_tags()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("bare.mp3");

        var result = await harness.Reader.ReadAsync(file);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Null(result.Metadata.Title);
        Assert.Null(result.Metadata.Artist);
        Assert.Null(result.Metadata.Album);
        Assert.Equal(Id3Version.Unknown, result.Id3Version);
        Assert.False(RawId3.HasId3v2Tag(file));
    }

    [Fact]
    public async Task Write_writes_id3v23_utf16_and_round_trips_vietnamese_text()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("vietnamese.mp3");
        var audioBefore = AudioStream.HashAudio(file);

        var update = MetadataUpdate.Empty
            .WithSet(MetadataField.Title, VietnameseTitle)
            .WithSet(MetadataField.Artist, VietnameseArtist)
            .WithSet(MetadataField.Album, VietnameseAlbum)
            .WithSet(MetadataField.Comment, VietnameseComment);

        var write = await harness.Writer.WriteAsync(file, update, Id3WriteVersion.V23);
        Assert.True(write.Success, write.ErrorMessage);

        // Our own reader.
        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Equal(VietnameseTitle, read.Metadata.Title);
        Assert.Equal(VietnameseArtist, read.Metadata.Artist);
        Assert.Equal(VietnameseAlbum, read.Metadata.Album);
        Assert.Equal(VietnameseComment, read.Metadata.Comment);
        Assert.Equal(Id3Version.Id3v23, read.Id3Version);

        // Independent byte level verification.
        Assert.Equal(3, RawId3.ReadTagVersion(file));
        foreach (var id in new[] { "TIT2", "TPE1", "TALB", "COMM" })
        {
            var frame = RawId3.FindFrame(file, id);
            Assert.NotNull(frame);
            Assert.Equal(1, frame!.Encoding); // 1 = UTF-16 with BOM, allowed in ID3v2.3
        }

        Assert.Equal(VietnameseTitle, RawId3.GetText(file, "TIT2"));
        Assert.Equal(VietnameseArtist, RawId3.GetText(file, "TPE1"));
        Assert.Equal(VietnameseAlbum, RawId3.GetText(file, "TALB"));

        // No replacement characters and no mojibake.
        Assert.False(read.Metadata.Title!.Contains('\uFFFD', StringComparison.Ordinal));

        // Audio stream unchanged.
        Assert.Equal(audioBefore, AudioStream.HashAudio(file));
    }

    [Fact]
    public async Task Write_can_target_id3v24_with_utf8_encoding()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("v24.mp3");

        var update = MetadataUpdate.Empty
            .WithSet(MetadataField.Title, VietnameseTitle)
            .WithSet(MetadataField.Artist, VietnameseArtist);

        var write = await harness.Writer.WriteAsync(file, update, Id3WriteVersion.V24);
        Assert.True(write.Success, write.ErrorMessage);

        Assert.Equal(4, RawId3.ReadTagVersion(file));
        var title = RawId3.FindFrame(file, "TIT2");
        Assert.NotNull(title);
        Assert.Equal(3, title!.Encoding); // 3 = UTF-8, only valid in ID3v2.4
        Assert.Equal(VietnameseTitle, RawId3.GetText(file, "TIT2"));

        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Equal(Id3Version.Id3v24, read.Id3Version);
        Assert.Equal(VietnameseTitle, read.Metadata.Title);
    }

    [Fact]
    public async Task Write_rewrites_an_id3v24_file_as_id3v23_by_default()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("convert.mp3");

        var first = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, "First"),
            Id3WriteVersion.V24);
        Assert.True(first.Success, first.ErrorMessage);
        Assert.Equal(4, RawId3.ReadTagVersion(file));

        var second = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, VietnameseTitle),
            Id3WriteVersion.V23);
        Assert.True(second.Success, second.ErrorMessage);

        Assert.Equal(3, RawId3.ReadTagVersion(file));
        Assert.Equal(1, RawId3.FindFrame(file, "TIT2")!.Encoding);
        Assert.Equal(VietnameseTitle, RawId3.GetText(file, "TIT2"));
    }

    [Fact]
    public async Task Write_only_touches_the_selected_fields_and_keeps_the_rest()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("keep.mp3");

        var initial = MetadataUpdate.Empty
            .WithSet(MetadataField.Title, "Original title")
            .WithSet(MetadataField.Artist, "Original artist")
            .WithSet(MetadataField.Album, "Original album")
            .WithSet(MetadataField.Genre, "Ballad")
            .WithSet(MetadataField.Year, "1998")
            .WithSet(MetadataField.TrackNumber, "3/12");

        var setup = await harness.Writer.WriteAsync(file, initial, Id3WriteVersion.V23);
        Assert.True(setup.Success, setup.ErrorMessage);

        var edit = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Artist, VietnameseArtist),
            Id3WriteVersion.V23);
        Assert.True(edit.Success, edit.ErrorMessage);

        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Equal(VietnameseArtist, read.Metadata.Artist);
        Assert.Equal("Original title", read.Metadata.Title);
        Assert.Equal("Original album", read.Metadata.Album);
        Assert.Equal("Ballad", read.Metadata.Genre);
        Assert.Equal("1998", read.Metadata.Year);
        Assert.Equal("3/12", read.Metadata.TrackNumber);
    }

    [Fact]
    public async Task Clear_removes_the_field_from_the_file()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("clear.mp3");

        var setup = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty
                .WithSet(MetadataField.Title, "To be cleared")
                .WithSet(MetadataField.Artist, "Keep me"),
            Id3WriteVersion.V23);
        Assert.True(setup.Success, setup.ErrorMessage);
        Assert.NotNull(RawId3.FindFrame(file, "TIT2"));

        var clear = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithClear(MetadataField.Title),
            Id3WriteVersion.V23);
        Assert.True(clear.Success, clear.ErrorMessage);

        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Null(read.Metadata.Title);
        Assert.Equal("Keep me", read.Metadata.Artist);
        Assert.Null(RawId3.FindFrame(file, "TIT2"));
    }

    [Fact]
    public async Task Write_preserves_the_embedded_cover_picture()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("with-cover.mp3");

        AddCoverPicture(file);

        var write = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, VietnameseTitle),
            Id3WriteVersion.V23);
        Assert.True(write.Success, write.ErrorMessage);

        var frames = RawId3.ReadFrames(file);
        Assert.Contains(frames, frame => frame.Id == "APIC");

        using var reopened = TagLib.File.Create(file);
        Assert.Single(reopened.Tag.Pictures);
        Assert.Equal(VietnameseTitle, reopened.Tag.Title);
    }

    [Fact]
    public async Task Write_converts_a_legacy_id3v1_tag_without_losing_its_values()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("legacy.mp3");

        // A file that only carries an ID3v1 tag.
        using (var legacy = TagLib.File.Create(file))
        {
            var v1 = (TagLib.Id3v1.Tag)legacy.GetTag(TagLib.TagTypes.Id3v1, true);
            v1.Title = "Legacy title";
            v1.Performers = ["Legacy artist"];
            legacy.Save();
        }

        Assert.Equal(Id3Version.Id3v1, (await harness.Reader.ReadAsync(file)).Id3Version);

        var write = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Album, "New album"),
            Id3WriteVersion.V23);
        Assert.True(write.Success, write.ErrorMessage);

        var read = await harness.Reader.ReadAsync(file);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Equal(Id3Version.Id3v23, read.Id3Version);
        Assert.Equal("Legacy title", read.Metadata.Title);
        Assert.Equal("Legacy artist", read.Metadata.Artist);
        Assert.Equal("New album", read.Metadata.Album);

        using var reopened = TagLib.File.Create(file);
        Assert.Equal(TagLib.TagTypes.None, reopened.TagTypesOnDisk & TagLib.TagTypes.Id3v1);
    }

    [Fact]
    public async Task Write_to_a_read_only_file_fails_and_leaves_the_file_untouched()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.CreateFile("readonly.mp3");
        var bytesBefore = File.ReadAllBytes(file);

        MakeReadOnly(file);

        var write = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, "Should not be written"),
            Id3WriteVersion.V23);

        Assert.False(write.Success);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
        Assert.False(RawId3.HasId3v2Tag(file));
    }

    [Fact]
    public async Task Read_of_a_corrupt_file_reports_an_error_without_throwing()
    {
        using var harness = new TestHarness();
        var file = harness.Workspace.Combine("corrupt.mp3");
        await File.WriteAllBytesAsync(file, "this is definitely not an mp3 file"u8.ToArray());

        var read = await harness.Reader.ReadAsync(file);

        Assert.False(read.Success);
        Assert.False(string.IsNullOrWhiteSpace(read.ErrorMessage));
    }

    [Fact]
    public async Task Write_stores_unicode_folder_and_file_names_unchanged()
    {
        using var harness = new TestHarness();
        var relative = Path.Combine("Nhạc Trịnh", "Sơn Ca 7", "01 - Đường Đến Ngày Vinh Quang.mp3");
        var file = harness.Workspace.CreateFile(relative);

        var write = await harness.Writer.WriteAsync(
            file,
            MetadataUpdate.Empty.WithSet(MetadataField.Title, VietnameseTitle),
            Id3WriteVersion.V23);
        Assert.True(write.Success, write.ErrorMessage);

        Assert.True(File.Exists(file));
        var read = await harness.Reader.ReadAsync(file);
        Assert.Equal(VietnameseTitle, read.Metadata.Title);
    }

    private static void AddCoverPicture(string file)
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9 };
        using var taglibFile = TagLib.File.Create(file);
        var tag = (TagLib.Id3v2.Tag)taglibFile.GetTag(TagLib.TagTypes.Id3v2, true);
        var picture = new TagLib.Picture(new TagLib.ByteVector(jpeg))
        {
            Type = TagLib.PictureType.FrontCover,
            MimeType = "image/jpeg",
            Description = "cover",
        };

        tag.AddFrame(new TagLib.Id3v2.AttachmentFrame(picture));
        taglibFile.Save();
    }

    private static void MakeReadOnly(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
            return;
        }

        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }
}
