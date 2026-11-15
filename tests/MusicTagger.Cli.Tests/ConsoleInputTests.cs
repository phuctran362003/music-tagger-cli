using MusicTagger.Cli.Presentation;
using MusicTagger.Cli.Tests.Support;
using Xunit;

namespace MusicTagger.Cli.Tests;

/// <summary>
/// The prompt helpers turn typed answers into decisions. These tests pin down the parsing and
/// the "back"/end of input behaviour the flows rely on.
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class ConsoleInputTests
{
    private static readonly string[] Options = ["First", "Second", "Third"];

    [Fact]
    public void AskChoice_ReturnsZeroBasedIndex()
    {
        using var console = new TestConsole(["2"]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal(1, input.AskChoice("Pick", Options));
    }

    [Fact]
    public void AskChoice_AsksAgainAfterInvalidInput()
    {
        using var console = new TestConsole(["9", "abc", "3"]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal(2, input.AskChoice("Pick", Options));
        Assert.Contains("Enter a number between 1 and 3", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AskChoice_ReturnsNullForBack()
    {
        using var console = new TestConsole(["b"]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Null(input.AskChoice("Pick", Options));
    }

    [Fact]
    public void AskChoice_ReturnsNullWhenInputEnds()
    {
        using var console = new TestConsole([]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Null(input.AskChoice("Pick", Options));
    }

    [Theory]
    [InlineData("1,3", new[] { 0, 2 })]
    [InlineData("1 2", new[] { 0, 1 })]
    [InlineData("3;1", new[] { 0, 2 })]
    [InlineData("2,2", new[] { 1 })]
    public void AskMultipleChoice_ParsesNumberLists(string answer, int[] expected)
    {
        using var console = new TestConsole([answer]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal(expected, input.AskMultipleChoice("Pick", Options)!.ToArray());
    }

    [Fact]
    public void AskMultipleChoice_AllTokenSelectsEverything()
    {
        using var console = new TestConsole(["a"]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal([0, 1, 2], input.AskMultipleChoice("Pick", Options)!.ToArray());
    }

    [Fact]
    public void AskMultipleChoice_ReturnsNullForBack()
    {
        using var console = new TestConsole(["b"]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Null(input.AskMultipleChoice("Pick", Options));
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("yes", true)]
    [InlineData("n", false)]
    [InlineData("no", false)]
    public void AskYesNo_ParsesAnswers(string answer, bool expected)
    {
        using var console = new TestConsole([answer]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal(expected, input.AskYesNo("Continue?"));
    }

    [Fact]
    public void AskYesNo_DefaultsToNo()
    {
        using var console = new TestConsole([""]);
        var input = new ConsoleInput(console.Ansi);

        Assert.False(input.AskYesNo("Apply?"));
    }

    [Fact]
    public void AskYesNo_UsesTheProvidedDefault()
    {
        using var console = new TestConsole([""]);
        var input = new ConsoleInput(console.Ansi);

        Assert.True(input.AskYesNo("Apply?", defaultYes: true));
    }

    [Fact]
    public void AskText_RunsTheValidatorUntilTheValueIsAccepted()
    {
        using var console = new TestConsole(["x", "ok"]);
        var input = new ConsoleInput(console.Ansi);

        var value = input.AskText(
            "Value",
            validate: candidate => candidate == "ok" ? null : "not ok");

        Assert.Equal("ok", value);
        Assert.Contains("not ok", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AskText_RejectsAnEmptyAnswerUnlessItIsAllowed()
    {
        using var console = new TestConsole(["", "text"]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal("text", input.AskText("Value"));
    }

    [Fact]
    public void AskText_AcceptsAnEmptyAnswerWhenAllowed()
    {
        using var console = new TestConsole([""]);
        var input = new ConsoleInput(console.Ansi);

        Assert.Equal(string.Empty, input.AskText("Value", allowEmpty: true));
    }

    [Fact]
    public void AskText_ReturnsNullForBackAndForEndOfInput()
    {
        using (var back = new TestConsole(["b"]))
        {
            Assert.Null(new ConsoleInput(back.Ansi).AskText("Value"));
        }

        using (var end = new TestConsole([]))
        {
            Assert.Null(new ConsoleInput(end.Ansi).AskText("Value"));
        }
    }
}
