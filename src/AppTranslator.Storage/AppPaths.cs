namespace AppTranslator.Storage;

/// <summary>Đường dẫn dữ liệu trong %AppData%\AppTranslator (mục 7 tài liệu).</summary>
public sealed class AppPaths(string root)
{
    public static AppPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppTranslator"));

    public string Root { get; } = root;
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string ProfilesDirectory => Path.Combine(Root, "profiles");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string DebugDirectory => Path.Combine(Root, "debug");
    public string SecretsFile => Path.Combine(Root, "secrets.bin");
    public string DataDirectory => Path.Combine(Root, "data");
    public string DatabaseFile => Path.Combine(DataDirectory, "translator.db");
}
