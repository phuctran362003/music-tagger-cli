using Spectre.Console;
using Xunit;

namespace MusicTagger.Cli.Tests.Support;

/// <summary>
/// The flows read answers with <see cref="Console.In"/>, which is process wide state, so every
/// test that drives a flow runs in this non parallel collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection
{
    public const string Name = "Console";
}
