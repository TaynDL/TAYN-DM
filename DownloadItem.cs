using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DownloadYarPro;
public sealed class DownloadItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public int Priority { get; set; }
    public string Sha256 { get; set; } = "";
    private string _status = "\u062F\u0631 \u0635\u0641";
    private long _downloaded;
    private long _total;
    private double _speed;
    [JsonIgnore] public CancellationTokenSource? Cancellation { get; set; }
    [JsonIgnore] public string FileName => Path.GetFileName(FilePath);
    [JsonIgnore] public string FileGlyph => Path.GetExtension(FilePath).ToLowerInvariant() switch
    {
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "▶",
        ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "♫",
        ".iso" or ".img" => "◉",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "▥",
        ".pdf" or ".doc" or ".docx" or ".txt" => "▤",
        ".exe" or ".msi" or ".appx" => "◆",
        _ => "↓"
    };
    public string Status { get => _status; set { _status = value; Notify(); Notify(nameof(StatusDisplay)); } }
    [JsonIgnore] public string StatusDisplay => LocalizationService.Status(Status);
    public void RefreshLocalization() { Notify(nameof(StatusDisplay)); Notify(nameof(PercentText)); }
    public long Downloaded { get => _downloaded; set { _downloaded = value; Notify(); Notify(nameof(Percent)); Notify(nameof(PercentText)); Notify(nameof(SizeText)); } }
    public long Total { get => _total; set { _total = value; Notify(); Notify(nameof(Percent)); Notify(nameof(PercentText)); Notify(nameof(SizeText)); } }
    [JsonIgnore] public double Speed { get => _speed; set { _speed = value; Notify(); Notify(nameof(SpeedText)); Notify(nameof(EtaText)); } }
    [JsonIgnore] public double Percent => Total > 0 ? Math.Clamp(Downloaded * 100d / Total, 0, 100) : 0;
    [JsonIgnore] public string PercentText => Total > 0 ? $"{Percent:0}%" : "";
    [JsonIgnore] public string SizeText => Total > 0 ? $"{Format(Downloaded)} / {Format(Total)}" : Format(Downloaded);
    [JsonIgnore] public string SpeedText => Speed > 0 ? $"{Format((long)Speed)}/s" : "\u2014";
    [JsonIgnore] public string EtaText { get { if (Speed <= 0 || Total <= Downloaded) return "\u2014"; var t = TimeSpan.FromSeconds((Total - Downloaded) / Speed); return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes:D2}:{t.Seconds:D2}"; } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
    public static string Format(long n) { string[] u = ["B", "KB", "MB", "GB", "TB"]; double v = Math.Max(0, n); int i = 0; while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; } return i == 0 ? $"{v:0} {u[i]}" : $"{v:0.0} {u[i]}"; }
}
