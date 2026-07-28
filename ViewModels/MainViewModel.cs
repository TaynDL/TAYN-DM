using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TaynDM.Views;

namespace TaynDM;

/// <summary>
/// ViewModel extracted from MainWindow.xaml.cs.
/// Owns download management, settings, state persistence,
/// search/filter, priority, and LinkReceiver integration.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    // ── Backing fields ────────────────────────────────────────────────
    private readonly ILogger _logger;
    private readonly Dispatcher _dispatcher;
    private readonly LinkReceiver _receiver = new();
    private readonly ClipboardMonitor _clipboard = new();
    private readonly List<DownloadItem> _pending = [];
    private CancellationTokenSource? _stateCts;

    private AppSettings _settings;
    private DownloadEngine? _engine;
    private string _engineProxy = "";
    private int _activeCount;
    private bool _disposed;

    private DownloadItem? _selectedDownload;
    private string _searchText = "";
    private string _footerText = "";
    private string _activeCountText = "00";
    private string _totalCountText = "00";
    private readonly SpeedHistory _speedHistory = new();

    // ── Events ────────────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? LinkReceived;
    public event Action<string>? ClipboardUrlDetected;

    // ── Constants ─────────────────────────────────────────────────────
    private const string Connecting = "در حال اتصال";
    private const string Downloading = "در حال دانلود";
    private const string Paused = "متوقف‌شده";
    private const string Completed = "تکمیل‌شده";
    private const string ErrorPrefix = "خطا: ";

    // ── Constructor ───────────────────────────────────────────────────
    public MainViewModel(ILogger logger, Dispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _settings = AppSettings.Load();
        _engineProxy = _settings.ProxyUrl;
        _engine = new DownloadEngine(_engineProxy, _logger);
    }

    // ── Public properties ─────────────────────────────────────────────

    public AppSettings Settings => _settings;
    public DownloadEngine? Engine => _engine;
    public ObservableCollection<DownloadItem> Downloads { get; } = [];

    /// <summary>Tracks aggregate download speed over time for the chart.</summary>
    public SpeedHistory SpeedHistory => _speedHistory;

    public DownloadItem? SelectedDownload
    {
        get => _selectedDownload;
        set
        {
            _selectedDownload = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
        }
    }

    public bool HasSelection => SelectedDownload != null;
    public bool HasNoSelection => SelectedDownload == null;

    public string FooterText
    {
        get => _footerText;
        set { _footerText = value; OnPropertyChanged(); }
    }

    public string ActiveCountText
    {
        get => _activeCountText;
        set { _activeCountText = value; OnPropertyChanged(); }
    }

    public string TotalCountText
    {
        get => _totalCountText;
        set { _totalCountText = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); }
    }

    /// <summary>Port the LinkReceiver bound to.</summary>
    public int ListenerPort => _receiver.Port;

    /// <summary>Is the current theme dark?</summary>
    public bool IsDarkTheme => ThemeManager.IsDark;

    // ── Initialization ────────────────────────────────────────────────

    /// <summary>
    /// Call once from MainWindow after construction.
    /// Loads persisted state, starts the engine and the link/clipboard receivers.
    /// </summary>
    public void Initialize()
    {
        ThemeManager.LoadFromSettings(_settings);
        ThemeManager.Apply();
        _logger.LogInfo("MainViewModel initializing...");

        ApplyLanguage();
        LoadState();
        StartAutoSave();
        StartLinkReceiver();
        StartClipboardMonitor();
        RefreshStats();

        _logger.LogInfo($"Ready — listener on port {_receiver.Port}");
    }

    // ── Download management ───────────────────────────────────────────

    /// <summary>
    /// Add a download from the UI dialog.
    /// </summary>
    public void AddDownload(string folder, string url)
    {
        Directory.CreateDirectory(folder);
        _settings.DefaultFolder = folder;
        _settings.Save();

        var item = new DownloadItem
        {
            Url = url,
            FilePath = UniquePath(Path.Combine(folder, GetFileName(url)))
        };
        Downloads.Add(item);
        SaveState();
        RefreshStats();
        Enqueue(item);
    }

    /// <summary>
    /// Add a link received from the TCP listener (browser extension)
    /// or clipboard monitor.
    /// </summary>
    public void AddExternalLink(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
                return;

            Directory.CreateDirectory(_settings.DefaultFolder);
            var item = new DownloadItem
            {
                Url = url,
                FilePath = UniquePath(
                    Path.Combine(_settings.DefaultFolder, GetFileName(url)))
            };
            Downloads.Add(item);
            SaveState();
            RefreshStats();
            Enqueue(item);

            _logger.LogInfo($"External link added: {url}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to add external link: {url}", ex);
        }
    }

    /// <summary>
    /// Enqueue an item for download.
    /// </summary>
    public void Enqueue(DownloadItem item)
    {
        if (item.Cancellation != null || _pending.Contains(item))
            return;

        item.Status = "در صف";
        _pending.Add(item);
        _pending.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        SaveState();
        PumpQueue();
    }

    /// <summary>
    /// Enqueue the currently selected item.
    /// </summary>
    public void EnqueueSelected(DownloadItem? item)
    {
        if (item != null) Enqueue(item);
    }

    /// <summary>
    /// Pause the currently selected download.
    /// </summary>
    public void PauseSelected(DownloadItem? item)
    {
        item?.Cancellation?.Cancel();
    }

    /// <summary>
    /// Remove the selected download after confirmation.
    /// </summary>
    public void RemoveSelected(DownloadItem? item, System.Windows.Window owner)
    {
        if (item == null) return;

        string text = $"\u00AB{item.FileName}\u00BB \u0627\u0632 \u0641\u0647\u0631\u0633\u062A \u062D\u0630\u0641 \u0634\u0648\u062F?\n\u0641\u0627\u06CC\u0644 \u0631\u0648\u06CC \u062F\u06CC\u0633\u06A9 \u067E\u0627\u06A9 \u0646\u0645\u06CC\u200C\u0634\u0648\u062F.";
        var result = System.Windows.MessageBox.Show(
            owner, text, "حذف",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        item.Cancellation?.Cancel();
        Downloads.Remove(item);
        SaveState();
        RefreshStats();
    }

    /// <summary>
    /// Open the folder containing the selected download.
    /// </summary>
    public void OpenFolder(DownloadItem? item)
    {
        try
        {
            string folder = item == null
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads")
                : Path.GetDirectoryName(item.FilePath)!;

            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to open folder", ex);
        }
    }

    /// <summary>
    /// Change priority of the selected item.
    /// </summary>
    public void ChangePriority(DownloadItem? item, int delta)
    {
        if (item == null) return;
        item.Priority += delta;
        _pending.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        SaveState();
    }

    // ── Queue processing ──────────────────────────────────────────────

    private void PumpQueue()
    {
        while (_activeCount < Math.Clamp(_settings.MaxConcurrentDownloads, 1, 10)
               && _pending.Count > 0)
        {
            var item = _pending[0];
            _pending.RemoveAt(0);
            _activeCount++;
            _ = RunQueued(item);
        }
        RefreshStats();
    }

    private async Task RunQueued(DownloadItem item)
    {
        try
        {
            await StartDownload(item);
        }
        finally
        {
            _activeCount--;
            RefreshEngineIfIdle();
            PumpQueue();
        }
    }

    private async Task StartDownload(DownloadItem item)
    {
        if (item.Cancellation != null) return;

        var cts = new CancellationTokenSource();
        item.Cancellation = cts;
        item.Status = Connecting;
        item.Speed = 0;
        _speedHistory.Clear();
        RefreshStats();

        try
        {
            item.Status = Downloading;

            var progress = new Progress<DownloadProgress>(p =>
            {
                item.Downloaded = p.Downloaded;
                item.Total = p.Total;
                if (p.BytesPerSecond > 0)
                {
                    item.Speed = p.BytesPerSecond;
                    _speedHistory.AddSample(p.BytesPerSecond);
                }
            });

            await _engine!.DownloadAsync(
                item,
                _settings.ConnectionsPerDownload,
                _settings.SpeedLimitBytesPerSecond,
                progress,
                cts.Token);

            item.Sha256 = await SecurityService.Sha256Async(item.FilePath, cts.Token);

            if (_settings.ScanAfterDownload)
                await SecurityService.ScanWithDefenderAsync(item.FilePath, cts.Token);

            item.Status = Completed;
            item.Speed = 0;

            if (_settings.ShowNotifications)
                new ToastWindow(item.FileName).Show();

            _logger.LogInfo($"Completed: {item.FileName}");
        }
        catch (OperationCanceledException)
        {
            item.Status = Paused;
            item.Speed = 0;
        }
        catch (Exception ex)
        {
            item.Status = ErrorPrefix + ex.Message;
            item.Speed = 0;
            _logger.LogError($"Download failed: {item.FileName}", ex);
        }
        finally
        {
            cts.Dispose();
            if (item.Cancellation == cts) item.Cancellation = null;
            SaveState();
            RefreshStats();
        }
    }

    // ── Engine management ─────────────────────────────────────────────

    public void RefreshEngineIfIdle()
    {
        if (_activeCount == 0 && _engineProxy != _settings.ProxyUrl)
        {
            _engine?.Dispose();
            _engineProxy = _settings.ProxyUrl;
            _engine = new DownloadEngine(_engineProxy, _logger);
        }
    }

    // ── Language ──────────────────────────────────────────────────────

    public void ToggleLanguage()
    {
        _settings.Language = _settings.Language == "en" ? "fa" : "en";
        _settings.Save();
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        foreach (var item in Downloads)
            item.RefreshLocalization();
        RefreshStats();
    }

    // ── Search / Filter ───────────────────────────────────────────────

    public void ApplySearchFilter(string? searchText, object? filterItem)
    {
        string text = searchText?.Trim() ?? "";
        string tag = (filterItem as System.Windows.Controls.ComboBoxItem)
            ?.Tag?.ToString() ?? "all";

        System.Windows.Data.CollectionViewSource
            .GetDefaultView(Downloads).Filter = value =>
        {
            if (value is not DownloadItem x) return false;

            bool matchesText = text.Length == 0
                || x.FileName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || x.Url.Contains(text, StringComparison.OrdinalIgnoreCase);

            bool matchesTag = tag == "all"
                || (tag == "active" && x.Cancellation != null)
                || (tag == "done" && x.Status == Completed);

            return matchesText && matchesTag;
        };
    }

    // ── State persistence ─────────────────────────────────────────────

    private string StateDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadYar");

    private string StateFile => Path.Combine(StateDir, "downloads.json");

    private void LoadState()
    {
        try
        {
            if (!File.Exists(StateFile)) return;

            var items = JsonSerializer.Deserialize<List<DownloadItem>>(
                File.ReadAllText(StateFile)) ?? [];

            foreach (var x in items)
            {
                if (x.Status == Connecting || x.Status == Downloading)
                    x.Status = Paused;
                Downloads.Add(x);
            }

            _logger.LogInfo($"Restored {items.Count} download(s) from state.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load state", ex);
        }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(StateDir);
            string temp = StateFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(
                Downloads,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, StateFile, true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save state", ex);
        }
    }

    private void StartAutoSave()
    {
        _stateCts = new CancellationTokenSource();
        _ = AutoSaveLoop(_stateCts.Token);
    }

    private async Task AutoSaveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                SaveState();
            }
            catch when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError("Auto-save error", ex);
            }
        }
    }

    // ── Link receiver ────────────────────────────────────────────────

    private void StartLinkReceiver()
    {
        _receiver.LinkReceived += url =>
        {
            _dispatcher.BeginInvoke(() =>
            {
                LinkReceived?.Invoke(url);
                AddExternalLink(url);
            });
        };
        _receiver.Start();
        _logger.LogInfo($"LinkReceiver started on port {_receiver.Port}");
    }

    // ── Clipboard monitor ─────────────────────────────────────────────

    private void StartClipboardMonitor()
    {
        _clipboard.ClipboardUrlDetected += (_, url) =>
        {
            _dispatcher.BeginInvoke(() =>
            {
                ClipboardUrlDetected?.Invoke(url);
                AddExternalLink(url);
            });
        };
        _clipboard.Start();
        _logger.LogInfo("ClipboardMonitor started");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void RefreshStats()
    {
        int active = Downloads.Count(x => x.Cancellation != null);
        int total = Downloads.Count;
        ActiveCountText = active.ToString("00");
        TotalCountText = total.ToString("00");

        FooterText = LocalizationService.English
            ? $"{active} active  |  {total} items in queue"
            : $"{active} دانلود فعال  |  {total} مورد در فهرست";
    }

    private static string GetFileName(string url)
    {
        string n = Uri.UnescapeDataString(
            Path.GetFileName(new Uri(url).AbsolutePath));

        foreach (char c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');

        return string.IsNullOrWhiteSpace(n)
            ? $"download-{DateTimeOffset.Now.ToUnixTimeSeconds()}.bin"
            : n;
    }

    private static string UniquePath(string p)
    {
        if (!File.Exists(p)) return p;

        string dir = Path.GetDirectoryName(p)!;
        string name = Path.GetFileNameWithoutExtension(p);
        string ext = Path.GetExtension(p);

        for (int i = 2; ; i++)
        {
            string x = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(x)) return x;
        }
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Dispose ──────────────────────────────────────────────────────

    public void Shutdown()
    {
        _clipboard.Dispose();
        _receiver.Dispose();
        foreach (var x in Downloads)
        {
            if (x.Cancellation != null)
            {
                x.Status = Paused;
                x.Cancellation.Cancel();
            }
        }
        SaveState();
        _engine?.Dispose();
        _stateCts?.Cancel();
        _stateCts?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }
}
