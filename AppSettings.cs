using System.IO;
using System.Text.Json;

namespace DownloadYarPro;
public sealed class AppSettings
{
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int ConnectionsPerDownload { get; set; } = 8;
    public long SpeedLimitBytesPerSecond { get; set; }
    public string DefaultFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public bool DarkTheme { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool ScanAfterDownload { get; set; }
    public bool RunAtStartup { get; set; }
    public string ProxyUrl { get; set; } = "";
    public string Language { get; set; } = "fa";
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DownloadYar", "settings.json");
    public static AppSettings Load() { try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new() : new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); string temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, FilePath, true); }
}
