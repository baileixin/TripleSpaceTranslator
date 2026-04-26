using System.IO;

namespace TripleSpaceTranslator.Core.Utilities;

public static class AppDataPaths
{
    public static string BaseDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TripleSpaceTranslator");

    public static string SettingsFilePath => Path.Combine(BaseDirectory, "settings.json");

    public static string SecretsDirectory => Path.Combine(BaseDirectory, "secrets");
}
