namespace MusicTagger.Core.Errors;

/// <summary>Raised when a backup manifest is structurally invalid or unsafe to use.</summary>
public sealed class InvalidManifestException : Exception
{
    public InvalidManifestException(IReadOnlyList<string> errors)
        : base("The backup manifest is not valid: " + string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
