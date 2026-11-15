using MusicTagger.Cli.Presentation;
using MusicTagger.Core.Models;
using MusicTagger.Core.Validation;
using Spectre.Console;

namespace MusicTagger.Cli.Flows;

/// <summary>
/// Turns the user's answers into a <see cref="MetadataUpdate"/>. Every field is edited with an
/// explicit <c>Keep</c> / <c>Set</c> / <c>Clear</c> choice, so an empty line can never delete a
/// tag by accident (SRS 3.2, FR-006, FR-007).
/// </summary>
public sealed class MetadataEditPrompts
{
    private readonly IAnsiConsole _console;
    private readonly ConsoleInput _input;

    public MetadataEditPrompts(IAnsiConsole console, ConsoleInput input)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <summary>
    /// Batch flow: first pick the fields to touch, then decide per field what to do.
    /// Returns null when the user goes back (nothing is written), or an empty update when
    /// every chosen field was kept.
    /// </summary>
    public MetadataUpdate? AskBatchUpdate()
    {
        var options = MetadataFieldCatalog.All.Select(descriptor => descriptor.DisplayName).ToList();

        var selected = _input.AskMultipleChoice("Choose the fields to update", options);
        if (selected is null)
        {
            return null;
        }

        var update = MetadataUpdate.Empty;
        foreach (var index in selected)
        {
            var descriptor = MetadataFieldCatalog.All[index];
            var edit = AskEdit(descriptor, currentValue: null);
            if (edit is null)
            {
                return null;
            }

            update = update.With(descriptor.Field, edit.Value);
        }

        return update;
    }

    /// <summary>
    /// Single file flow: walk every field with its current value. Pressing Enter keeps the
    /// current value, so nothing changes unless the user asks for it.
    /// </summary>
    public MetadataUpdate? AskSingleFileUpdate(AudioMetadata current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var update = MetadataUpdate.Empty;
        foreach (var descriptor in MetadataFieldCatalog.All)
        {
            var edit = AskEdit(descriptor, current.GetValue(descriptor.Field));
            if (edit is null)
            {
                return null;
            }

            update = update.With(descriptor.Field, edit.Value);
        }

        return update;
    }

    private FieldEdit? AskEdit(MetadataFieldDescriptor descriptor, string? currentValue)
    {
        var current = string.IsNullOrWhiteSpace(currentValue) ? "(empty)" : $"'{currentValue}'";

        while (true)
        {
            var answer = _input.ReadLine($"{descriptor.DisplayName} - current {current}: keep / set / clear?");

            if (answer is null)
            {
                return null;
            }

            switch (answer.ToLowerInvariant())
            {
                case "":
                case "k":
                case "keep":
                    return FieldEdit.Keep;

                case "b":
                case "back":
                    return null;

                case "c":
                case "clear":
                    return FieldEdit.Clear;

                case "s":
                case "set":
                    var value = AskValidatedValue(descriptor);
                    if (value is null)
                    {
                        continue;
                    }

                    return FieldEdit.Set(value);

                default:
                    _console.MarkupLine("[yellow]Answer k (keep), s (set), c (clear) or b (back).[/]");
                    continue;
            }
        }
    }

    private string? AskValidatedValue(MetadataFieldDescriptor descriptor)
    {
        while (true)
        {
            var raw = _input.AskText($"{descriptor.DisplayName} value:", descriptor.Hint);
            if (raw is null)
            {
                // The user went back: return to the keep/set/clear question.
                return null;
            }

            var result = MetadataValidator.ValidateSetValue(descriptor.Field, raw);
            if (result.IsValid && result.Value is not null)
            {
                return result.Value;
            }

            _console.MarkupLine($"[red]{Markup.Escape(result.Error ?? "The value is not valid.")}[/]");
        }
    }
}
