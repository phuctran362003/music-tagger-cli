using MusicTagger.Core.Models;
using Xunit;

namespace MusicTagger.Core.Tests;

public sealed class MetadataUpdateTests
{
    private static AudioMetadata Current => new()
    {
        Title = "Old title",
        Artist = "Old artist",
        Album = "Old album",
        Year = "1998",
    };

    [Fact]
    public void Empty_update_changes_nothing()
    {
        var changed = MetadataUpdate.Empty.TryApply(Current, out var updated, out var changes);

        Assert.False(changed);
        Assert.Empty(changes);
        Assert.Equal(Current, updated);
    }

    [Fact]
    public void Keep_edits_are_not_stored_and_change_nothing()
    {
        var update = MetadataUpdate.Empty.With(MetadataField.Title, FieldEdit.Keep);

        Assert.True(update.IsEmpty);
        Assert.False(update.TryApply(Current, out var updated, out _));
        Assert.Equal("Old title", updated.Title);
    }

    [Fact]
    public void Set_replaces_only_the_selected_field()
    {
        var update = MetadataUpdate.Empty.WithSet(MetadataField.Artist, "Trịnh Công Sơn");

        var changed = update.TryApply(Current, out var updated, out var changes);

        Assert.True(changed);
        var change = Assert.Single(changes);
        Assert.Equal(MetadataField.Artist, change.Field);
        Assert.Equal("Old artist", change.OldValue);
        Assert.Equal("Trịnh Công Sơn", change.NewValue);

        Assert.Equal("Trịnh Công Sơn", updated.Artist);
        Assert.Equal("Old title", updated.Title);
        Assert.Equal("Old album", updated.Album);
        Assert.Equal("1998", updated.Year);
    }

    [Fact]
    public void Clear_removes_the_value_on_purpose()
    {
        var update = MetadataUpdate.Empty.WithClear(MetadataField.Album);

        var changed = update.TryApply(Current, out var updated, out var changes);

        Assert.True(changed);
        Assert.Null(updated.Album);
        var change = Assert.Single(changes);
        Assert.True(change.IsClear);
    }

    [Fact]
    public void Setting_the_same_value_is_not_a_change()
    {
        var update = MetadataUpdate.Empty.WithSet(MetadataField.Title, "  Old title  ");

        Assert.False(update.TryApply(Current, out _, out var changes));
        Assert.Empty(changes);
    }

    [Fact]
    public void Clearing_an_already_empty_field_is_not_a_change()
    {
        var update = MetadataUpdate.Empty.WithClear(MetadataField.Comment);

        Assert.False(update.TryApply(Current, out _, out _));
    }

    [Fact]
    public void Multiple_fields_can_change_in_one_update()
    {
        var update = MetadataUpdate.Empty
            .WithSet(MetadataField.Title, "New title")
            .WithSet(MetadataField.Artist, "New artist")
            .WithClear(MetadataField.Year);

        var changed = update.TryApply(Current, out var updated, out var changes);

        Assert.True(changed);
        Assert.Equal(3, changes.Count);
        Assert.Equal("New title", updated.Title);
        Assert.Equal("New artist", updated.Artist);
        Assert.Null(updated.Year);
    }

    [Fact]
    public void Set_with_an_empty_value_throws_instead_of_clearing_silently()
    {
        var update = MetadataUpdate.Empty.WithSet(MetadataField.Title, "   ");

        Assert.Throws<InvalidOperationException>(() => update.ApplyTo(Current));
    }

    [Fact]
    public void Updates_are_immutable()
    {
        var original = MetadataUpdate.Empty.WithSet(MetadataField.Title, "A");
        var derived = original.WithSet(MetadataField.Artist, "B");

        Assert.Single(original.Edits);
        Assert.Equal(2, derived.Edits.Count);
        Assert.False(original.Contains(MetadataField.Artist));
    }
}
