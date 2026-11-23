using System.Text.Json;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;

namespace AppTranslator.Storage;

/// <summary>Mỗi hồ sơ game là một file profiles\{id}.json.</summary>
public sealed class JsonProfileStore(AppPaths paths) : IProfileStore
{
    public IReadOnlyList<GameProfile> LoadAll()
    {
        if (!Directory.Exists(paths.ProfilesDirectory))
            return [];

        return Directory.EnumerateFiles(paths.ProfilesDirectory, "*.json")
            .Select(Read)
            .OrderBy(p => p.GameName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public GameProfile? Get(string id)
    {
        var file = FileFor(id);
        return File.Exists(file) ? Read(file) : null;
    }

    public void Save(GameProfile profile)
    {
        Directory.CreateDirectory(paths.ProfilesDirectory);
        File.WriteAllText(FileFor(profile.Id), JsonSerializer.Serialize(profile, JsonDefaults.Options));
    }

    /// <summary>Chép các hồ sơ đóng gói kèm app vào thư mục người dùng nếu chưa có; không ghi đè bản người dùng đã sửa.</summary>
    public void SeedMissing(string bundledDirectory)
    {
        if (!Directory.Exists(bundledDirectory))
            return;

        Directory.CreateDirectory(paths.ProfilesDirectory);
        foreach (var source in Directory.EnumerateFiles(bundledDirectory, "*.json"))
        {
            var target = Path.Combine(paths.ProfilesDirectory, Path.GetFileName(source));
            if (!File.Exists(target))
                File.Copy(source, target);
        }
    }

    private string FileFor(string id) => Path.Combine(paths.ProfilesDirectory, id + ".json");

    private static GameProfile Read(string file) =>
        JsonSerializer.Deserialize<GameProfile>(File.ReadAllText(file), JsonDefaults.Options)
        ?? throw new InvalidDataException($"Hồ sơ rỗng: {file}");
}
