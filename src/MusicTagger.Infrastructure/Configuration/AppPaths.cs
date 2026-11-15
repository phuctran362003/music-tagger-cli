namespace MusicTagger.Infrastructure.Configuration;

/// <summary>
/// Per-user application data locations. Settings and backups never live inside the music
/// folder unless the user explicitly configures a backup directory (SRS FR-010).
/// </summary>
public static class AppPaths
{
    public static string AppDataDirectory { get; } = ResolveAppDataDirectory();

    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

    public static string DefaultBackupDirectory => Path.Combine(AppDataDirectory, "backups");

    public static string LogDirectory => Path.Combine(AppDataDirectory, "logs");

    private static string ResolveAppDataDirectory()
    {
        var baseDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolderOption.DoNotVerify);

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = Path.GetTempPath();
            }
            else
            {
                baseDirectory = Path.Combine(baseDirectory, ".config");
            }
        }

        return Path.Combine(baseDirectory, "MusicTagger");
    }
}
