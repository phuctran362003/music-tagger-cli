using MusicTagger.Core.Tests.Support;
using MusicTagger.Core.Validation;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class PathValidationTests
{
    [Theory]
    [InlineData("\"D:\\Music\\Nhạc Trịnh\"", "D:\\Music\\Nhạc Trịnh")]
    [InlineData("'D:\\Music\\Nhạc Trịnh'", "D:\\Music\\Nhạc Trịnh")]
    [InlineData("  D:\\Music\\Nhạc Trịnh  ", "D:\\Music\\Nhạc Trịnh")]
    [InlineData("D:\\Music with spaces", "D:\\Music with spaces")]
    public void Quotes_and_whitespace_are_removed_but_the_path_is_not_rewritten(string input, string expected)
    {
        Assert.Equal(expected, FolderPathValidator.Normalize(input));
    }

    [Fact]
    public void Mismatched_quotes_are_left_alone()
    {
        Assert.Equal("\"D:\\Music'", FolderPathValidator.Normalize("\"D:\\Music'"));
    }

    [Fact]
    public void Missing_folder_is_reported()
    {
        var probe = new FakeFileProbe();

        var result = FolderPathValidator.Validate("/music/nope", probe);

        Assert.False(result.IsValid);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inaccessible_folder_is_reported()
    {
        var folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "locked"));
        var probe = new FakeFileProbe();
        probe.ExistingDirectories.Add(folder);
        probe.DirectoryAccessProblems[folder] = "Access denied for folder: " + folder;

        var result = FolderPathValidator.Validate(folder, probe);

        Assert.False(result.IsValid);
        Assert.Contains("Access denied", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Existing_folder_returns_the_full_path()
    {
        var folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "Nhạc Trịnh"));
        var probe = new FakeFileProbe();
        probe.ExistingDirectories.Add(folder);

        var result = FolderPathValidator.Validate("\"" + folder + "\"", probe);

        Assert.True(result.IsValid);
        Assert.Equal(folder, result.FullPath);
    }

    [Fact]
    public void Empty_input_is_rejected_with_a_clear_message()
    {
        var result = FolderPathValidator.Validate("   ", new FakeFileProbe());

        Assert.False(result.IsValid);
        Assert.Contains("enter a folder path", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Relative_paths_inside_the_root_are_allowed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "root"));

        Assert.True(PathSafety.TryResolveContained(root, "sub/song.mp3", out var full, out var error));
        Assert.Null(error);
        Assert.True(PathSafety.IsInside(root, full));
    }

    [Theory]
    [InlineData("../escape.mp3")]
    [InlineData("sub/../../escape.mp3")]
    [InlineData("..\\escape.mp3")]
    [InlineData("")]
    [InlineData("   ")]
    public void Relative_paths_that_escape_the_root_are_rejected(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "root"));

        Assert.False(PathSafety.TryResolveContained(root, relativePath, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Absolute_paths_are_rejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "root"));
        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "elsewhere", "song.mp3"));

        Assert.False(PathSafety.TryResolveContained(root, absolute, out _, out var error));
        Assert.Contains("Absolute", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Drive_relative_paths_are_rejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "root"));

        Assert.False(PathSafety.TryResolveContained(root, "D:song.mp3", out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Containment_check_handles_trailing_separators_and_prefixes()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "music"));

        Assert.True(PathSafety.IsInside(root, Path.Combine(root, "a", "b.mp3")));
        Assert.False(PathSafety.IsInside(root, Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MusicTaggerFake", "music-other", "b.mp3"))));
    }
}
