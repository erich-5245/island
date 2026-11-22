using System.IO;

namespace Island.App.Infrastructure;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Island");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string LogFile => Path.Combine(DataDirectory, "island.log");

    public static string ModuleDataDirectory(string moduleId)
    {
        var dir = Path.Combine(DataDirectory, "modules", moduleId);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
