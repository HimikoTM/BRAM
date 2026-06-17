using System.IO;
using System.Text.Json;

namespace RobloxAccountManager.Services;

public sealed class Settings
{
    public string Theme { get; set; } = "Dark";
    public long DefaultPlaceId { get; set; } = 0;
    public bool MultiInstance { get; set; } = true;
    public int AutoRefreshSeconds { get; set; } = 60;

    public static Settings Load()
    {
        try
        {
            if (!File.Exists(Storage.SettingsPath)) return new();
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Storage.SettingsPath)) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Storage.SettingsPath)!);
            File.WriteAllText(Storage.SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
