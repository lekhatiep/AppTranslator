using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AppTranslator.Storage;

/// <summary>
/// Lưu bí mật (API key) vào secrets.bin, mã hóa bằng Windows DPAPI theo tài khoản Windows hiện tại:
/// chép file sang máy/tài khoản khác thì không giải mã được. Không bao giờ ghi giá trị ra log.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SecretStore(AppPaths paths)
{
    public const string GeminiApiKey = "geminiApiKey";

    // Thêm vào DPAPI để app khác cùng tài khoản không giải mã nhầm được file này một cách tình cờ.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AppTranslator.secrets.v1");

    private readonly object _lock = new();

    public string? Get(string name)
    {
        lock (_lock)
        {
            var secrets = Load();
            return secrets.TryGetValue(name, out string? value) ? value : null;
        }
    }

    /// <summary>value rỗng hoặc null thì xóa.</summary>
    public void Set(string name, string? value)
    {
        lock (_lock)
        {
            var secrets = Load();
            if (string.IsNullOrWhiteSpace(value))
                secrets.Remove(name);
            else
                secrets[name] = value.Trim();

            byte[] plain = JsonSerializer.SerializeToUtf8Bytes(secrets);
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.SecretsFile)!);
            File.WriteAllBytes(paths.SecretsFile, encrypted);
        }
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(paths.SecretsFile))
            return [];

        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(paths.SecretsFile), Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? [];
    }
}
