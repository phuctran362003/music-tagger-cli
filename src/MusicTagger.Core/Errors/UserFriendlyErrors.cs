namespace MusicTagger.Core.Errors;

/// <summary>Turns exceptions into short sentences that are safe to show in the terminal.</summary>
public static class UserFriendlyErrors
{
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            UnauthorizedAccessException => "Access denied. The file may be read-only or owned by another user.",
            FileNotFoundException or DirectoryNotFoundException => "File or folder not found.",
            PathTooLongException => "The path is too long for this system.",
            DriveNotFoundException => "The drive could not be found.",
            IOException io when IsDiskFull(io) => "Not enough free space to write the file.",
            IOException io => $"I/O error: {io.Message}",
            InvalidOperationException => exception.Message,
            NotSupportedException => $"Operation not supported: {exception.Message}",
            OperationCanceledException => "Operation cancelled.",
            _ => $"Unexpected error: {exception.Message}",
        };
    }

    private static bool IsDiskFull(IOException exception)
    {
        // HRESULT 0x80070070 = ERROR_DISK_FULL, 0x80070027 = ERROR_HANDLE_DISK_FULL
        var hr = exception.HResult;
        return hr is unchecked((int)0x80070070) or unchecked((int)0x80070027);
    }
}
