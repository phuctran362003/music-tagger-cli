namespace MusicTagger.Core.Models;

/// <summary>Intent of a single field edit.</summary>
public enum FieldEditKind
{
    /// <summary>Do not touch the field (default, and the only value of an unset edit).</summary>
    Keep = 0,

    /// <summary>Write the supplied value.</summary>
    Set = 1,

    /// <summary>Remove the field from the tag on purpose.</summary>
    Clear = 2,
}

/// <summary>
/// One field edit. <see cref="FieldEditKind.Keep"/> and <see cref="FieldEditKind.Clear"/>
/// are explicit states; an empty string is never used to mean "clear".
/// </summary>
public readonly record struct FieldEdit
{
    private FieldEdit(FieldEditKind kind, string? value)
    {
        Kind = kind;
        Value = value;
    }

    public FieldEditKind Kind { get; }

    public string? Value { get; }

    public static FieldEdit Keep => default;

    public static FieldEdit Clear => new(FieldEditKind.Clear, null);

    public static FieldEdit Set(string value) =>
        new(FieldEditKind.Set, value ?? throw new ArgumentNullException(nameof(value)));

    public bool IsKeep => Kind == FieldEditKind.Keep;
}
