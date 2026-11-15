using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class MetadataValidatorTests
{
    [Theory]
    [InlineData(MetadataField.Title, "Đường Đến Ngày Vinh Quang")]
    [InlineData(MetadataField.Artist, "Trịnh Công Sơn")]
    [InlineData(MetadataField.Album, "Những Bản Tình Ca – thử nghiệm")]
    [InlineData(MetadataField.Comment, "Bài hát tiếng Việt: ă â ê ô ơ ư đ")]
    public void Free_text_values_are_trimmed_and_accepted(MetadataField field, string value)
    {
        var result = MetadataValidator.ValidateSetValue(field, "  " + value + "  ");

        Assert.True(result.IsValid);
        Assert.Equal(value, result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_values_are_rejected_so_that_clear_must_be_explicit(string? value)
    {
        var result = MetadataValidator.ValidateSetValue(MetadataField.Title, value);

        Assert.False(result.IsValid);
        Assert.Contains("Clear", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Too_long_values_are_rejected()
    {
        var value = new string('a', MetadataValidator.MaxTextLength + 1);

        var result = MetadataValidator.ValidateSetValue(MetadataField.Title, value);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("1998", "1998")]
    [InlineData(" 2026 ", "2026")]
    [InlineData("1000", "1000")]
    public void Valid_years_are_accepted(string input, string expected)
    {
        var result = MetadataValidator.ValidateSetValue(MetadataField.Year, input);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("99")]
    [InlineData("3000")]
    [InlineData("19 98")]
    [InlineData("-1998")]
    public void Invalid_years_are_rejected(string input)
    {
        Assert.False(MetadataValidator.ValidateSetValue(MetadataField.Year, input).IsValid);
    }

    [Theory]
    [InlineData("3", "3")]
    [InlineData("03", "3")]
    [InlineData("3/12", "3/12")]
    [InlineData(" 1 / 2 ", "1/2")]
    public void Valid_track_positions_are_normalized(string input, string expected)
    {
        var result = MetadataValidator.ValidateSetValue(MetadataField.TrackNumber, input);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0/5")]
    [InlineData("5/3")]
    [InlineData("5/0")]
    [InlineData("a/b")]
    [InlineData("1/2/3")]
    [InlineData("10000")]
    public void Invalid_positions_are_rejected(string input)
    {
        Assert.False(MetadataValidator.ValidateSetValue(MetadataField.DiscNumber, input).IsValid);
    }

    [Fact]
    public void Control_characters_are_rejected_except_newlines_in_comments()
    {
        Assert.False(MetadataValidator.ValidateSetValue(MetadataField.Title, "bad\u0007title").IsValid);
        Assert.True(MetadataValidator.ValidateSetValue(MetadataField.Comment, "line1\nline2").IsValid);
    }

    [Fact]
    public void Position_parsing_reports_number_and_total()
    {
        Assert.True(MetadataValidator.TryParsePosition("7/9", out var number, out var total));
        Assert.Equal(7u, number);
        Assert.Equal(9u, total);

        Assert.True(MetadataValidator.TryParsePosition("7", out number, out total));
        Assert.Equal(7u, number);
        Assert.Null(total);

        Assert.False(MetadataValidator.TryParsePosition("", out _, out _));
    }
}
