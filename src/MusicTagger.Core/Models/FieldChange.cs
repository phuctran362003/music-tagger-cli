namespace MusicTagger.Core.Models;

/// <summary>A single old/new value transition for one field.</summary>
public sealed record FieldChange(MetadataField Field, string? OldValue, string? NewValue)
{
    public string FieldName => MetadataFieldCatalog.DisplayName(Field);

    public bool IsClear => NewValue is null;

    public string Describe() =>
        $"{FieldName}: '{Display(OldValue)}' -> '{(NewValue is null ? "<cleared>" : NewValue)}'";

    public static string Display(string? value) => value ?? "<empty>";
}
