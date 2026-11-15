namespace MusicTagger.Core.Models;

public sealed record MetadataReadResult
{
    private MetadataReadResult(bool success, AudioMetadata metadata, TagFormat tagFormat, string? errorMessage, string? warningMessage)
    {
        Success = success;
        Metadata = metadata;
        TagFormat = tagFormat;
        ErrorMessage = errorMessage;
        WarningMessage = warningMessage;
    }

    public bool Success { get; }

    public AudioMetadata Metadata { get; }

    /// <summary>Tag system that was found on disk.</summary>
    public TagFormat TagFormat { get; }

    public string? ErrorMessage { get; }

    public string? WarningMessage { get; }

    public static MetadataReadResult Ok(AudioMetadata metadata, TagFormat tagFormat, string? warningMessage = null) =>
        new(true, metadata, tagFormat, null, warningMessage);

    public static MetadataReadResult Failure(string errorMessage) =>
        new(false, AudioMetadata.Empty, TagFormat.Unknown, errorMessage, null);
}

public sealed record MetadataWriteResult
{
    private MetadataWriteResult(bool success, AudioMetadata verifiedMetadata, string? errorMessage)
    {
        Success = success;
        VerifiedMetadata = verifiedMetadata;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    /// <summary>Metadata read back from the file after the write completed.</summary>
    public AudioMetadata VerifiedMetadata { get; }

    public string? ErrorMessage { get; }

    public static MetadataWriteResult Ok(AudioMetadata verifiedMetadata) => new(true, verifiedMetadata, null);

    public static MetadataWriteResult Failure(string errorMessage) =>
        new(false, AudioMetadata.Empty, errorMessage);
}
