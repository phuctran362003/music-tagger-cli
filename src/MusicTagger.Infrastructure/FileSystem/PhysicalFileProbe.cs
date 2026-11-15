using MusicTagger.Core.Contracts;

namespace MusicTagger.Infrastructure.FileSystem;

/// <summary>Real filesystem checks used by the core services.</summary>
public sealed class PhysicalFileProbe : IFileProbe
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool IsFileReadOnly(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                return true;
            }

            if (OperatingSystem.IsWindows())
            {
                return false;
            }

            // On Unix the read-only attribute is not used; probe the actual write access.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            // Locked by another process: not read-only, but the write attempt will report it.
            return false;
        }
    }

    public string? CheckDirectoryAccess(string path)
    {
        try
        {
            using var enumerator = Directory
                .EnumerateFileSystemEntries(path)
                .GetEnumerator();

            // Force the first access, which is what throws for inaccessible folders.
            enumerator.MoveNext();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return $"Access denied for folder: {path}";
        }
        catch (DirectoryNotFoundException)
        {
            return $"Folder not found: {path}";
        }
        catch (IOException ex)
        {
            return $"The folder cannot be read: {ex.Message}";
        }
    }
}
