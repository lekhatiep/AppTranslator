using System.Text.Json;
using AppTranslator.Core.Settings;

namespace AppTranslator.Storage;

public sealed class JsonSettingsStore(AppPaths paths)
{
    /// <summary>Đọc settings.json; chưa có file thì tạo file với giá trị mặc định.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(paths.SettingsFile))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(paths.SettingsFile);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(paths.Root);
        File.WriteAllText(paths.SettingsFile, JsonSerializer.Serialize(settings, JsonDefaults.Options));
    }
}
