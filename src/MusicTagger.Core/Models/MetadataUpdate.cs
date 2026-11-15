namespace MusicTagger.Core.Models;

/// <summary>
/// Immutable set of field edits that is applied to one or more files.
/// Fields that are not present in <see cref="Edits"/> are kept untouched.
/// </summary>
public sealed class MetadataUpdate
{
    private static readonly IReadOnlyDictionary<MetadataField, FieldEdit> NoEdits =
        new Dictionary<MetadataField, FieldEdit>();

    private readonly IReadOnlyDictionary<MetadataField, FieldEdit> _edits;

    public MetadataUpdate()
        : this(NoEdits)
    {
    }

    private MetadataUpdate(IReadOnlyDictionary<MetadataField, FieldEdit> edits)
    {
        _edits = edits;
    }

    public static MetadataUpdate Empty { get; } = new();

    public IReadOnlyDictionary<MetadataField, FieldEdit> Edits => _edits;

    public bool IsEmpty => _edits.Count == 0;

    public bool Contains(MetadataField field) => _edits.ContainsKey(field);

    public MetadataUpdate With(MetadataField field, FieldEdit edit)
    {
        if (edit.IsKeep)
        {
            return Without(field);
        }

        var copy = new Dictionary<MetadataField, FieldEdit>(_edits)
        {
            [field] = edit,
        };

        return new MetadataUpdate(copy);
    }

    public MetadataUpdate WithSet(MetadataField field, string value) => With(field, FieldEdit.Set(value));

    public MetadataUpdate WithClear(MetadataField field) => With(field, FieldEdit.Clear);

    public MetadataUpdate Without(MetadataField field)
    {
        if (!_edits.ContainsKey(field))
        {
            return this;
        }

        var copy = new Dictionary<MetadataField, FieldEdit>(_edits);
        copy.Remove(field);
        return new MetadataUpdate(copy);
    }

    /// <summary>Produces the metadata that would be stored after applying this update.</summary>
    public AudioMetadata ApplyTo(AudioMetadata current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var result = current;
        foreach (var (field, edit) in _edits)
        {
            switch (edit.Kind)
            {
                case FieldEditKind.Keep:
                    break;
                case FieldEditKind.Clear:
                    result = result.WithValue(field, null);
                    break;
                case FieldEditKind.Set:
                    if (MetadataText.Normalize(edit.Value) is null)
                    {
                        throw new InvalidOperationException(
                            "A Set edit must have a non-empty value. Use Clear to remove a field.");
                    }

                    result = result.WithValue(field, edit.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(edit), edit.Kind, "Unknown edit kind.");
            }
        }

        return result;
    }

    /// <summary>
    /// Applies the update and reports the effective changes. Returns false when the
    /// update would not change anything (those files must not be rewritten).
    /// </summary>
    public bool TryApply(AudioMetadata current, out AudioMetadata updated, out IReadOnlyList<FieldChange> changes)
    {
        ArgumentNullException.ThrowIfNull(current);

        updated = ApplyTo(current);
        var list = new List<FieldChange>();
        foreach (var (field, edit) in _edits)
        {
            if (edit.Kind == FieldEditKind.Keep)
            {
                continue;
            }

            var oldValue = MetadataText.Normalize(current.GetValue(field));
            var newValue = MetadataText.Normalize(updated.GetValue(field));
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                list.Add(new FieldChange(field, oldValue, newValue));
            }
        }

        changes = list;
        return list.Count > 0;
    }
}
