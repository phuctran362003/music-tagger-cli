namespace MusicTagger.Cli.Tests.Support;

/// <summary>Throw away directory that is removed when the test finishes.</summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "musictagger-cli-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>Creates (and returns) a sub directory of the workspace.</summary>
    public string Subdirectory(string name)
    {
        var path = Path.Combine(Root, name);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Resolves a path inside the workspace, creating its parent directory.</summary>
    public string FilePath(string relativePath)
    {
        var path = Path.Combine(Root, relativePath);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            System.IO.Directory.CreateDirectory(parent);
        }

        return path;
    }

    public void Dispose()
    {
        try
        {
            if (!System.IO.Directory.Exists(Root))
            {
                return;
            }

            // The safe writer may leave a read-only file behind on some systems.
            foreach (var file in System.IO.Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch (IOException)
                {
                    // Best effort only.
                }
            }

            System.IO.Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary files are cleaned up by the OS.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: never fail a test because of clean up.
        }
    }
}
