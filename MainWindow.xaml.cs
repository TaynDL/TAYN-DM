using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DownloadYarPro;
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string Connecting = "\u062F\u0631 \u062D\u0627\u0644 \u0627\u062A\u0635\u0627\u0644", Downloading = "\u062F\u0631 \u062D\u0627\u0644 \u062F\u0627\u0646\u0644\u0648\u062F", Paused = "\u0645\u062A\u0648\u0642\u0641\u200C\u0634\u062F\u0647", Completed = "\u062A\u06A9\u0645\u06CC\u0644\u200C\u0634\u062F\u0647", ErrorPrefix = "\u062E\u0637\u0627: ";
    private readonly AppSettings settings = AppSettings.Load();
    private DownloadEngine engine;
    private string engineProxy;
    private readonly List<DownloadItem> pending = [];
    private readonly LinkReceiver linkReceiver = new();
    private int activeCount;
    private readonly string stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DownloadYar");
    private string StateFile => Path.Combine(stateDir, "downloads.json");
    public ObservableCollection<DownloadItem> Downloads { get; } = [];
    private DownloadItem? selectedDownload;
    public DownloadItem? SelectedDownload { get => selectedDownload; set { selectedDownload = value; Notify(); Notify(nameof(HasSelection)); Notify(nameof(HasNoSelection)); } }
    public Visibility HasSelection => SelectedDownload == null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility HasNoSelection => SelectedDownload == null ? Visibility.Visible : Visibility.Collapsed;
    public string FooterText => LocalizationService.English ? $"{Downloads.Count(x => x.Cancellation != null)} active  |  {Downloads.Count} items in queue" : $"{Downloads.Count(x => x.Cancellation != null)} \u062F\u0627\u0646\u0644\u0648\u062F \u0641\u0639\u0627\u0644  |  {Downloads.Count} \u0645\u0648\u0631\u062F \u062F\u0631 \u0641\u0647\u0631\u0633\u062A";
    public string ActiveCountText => activeCount.ToString("00");
    public string TotalCountText => Downloads.Count.ToString("00");
    public event PropertyChangedEventHandler? PropertyChanged;
    public MainWindow() { string? forcedLanguage = Environment.GetEnvironmentVariable("TAYN_LANGUAGE"); if (forcedLanguage is "fa" or "en") settings.Language = forcedLanguage; engineProxy = settings.ProxyUrl; engine = new(engineProxy); ApplyTheme(); InitializeComponent(); DataContext = this; LoadState(); if (Environment.GetEnvironmentVariable("TAYN_DEMO") == "1") LoadDemoItems(); SelectedDownload = Downloads.FirstOrDefault(); Loaded += MainWindow_Loaded; linkReceiver.LinkReceived += url => Dispatcher.Invoke(() => AddBrowserLink(url)); linkReceiver.Start(); Closing += OnClosing; }
    private void LoadDemoItems()
    {
        Downloads.Clear();
        Downloads.Add(new DownloadItem { FilePath = @"C:\Downloads\ubuntu-24.04-desktop-amd64.iso", Url = "https://releases.ubuntu.com/24.04/ubuntu.iso", Downloaded = 2640000000, Total = 3200000000, Speed = 25400000, Status = Downloading });
        Downloads.Add(new DownloadItem { FilePath = @"C:\Downloads\Interstellar.2014.1080p.mkv", Url = "https://example.com/movie.mkv", Downloaded = 1230000000, Total = 3940000000, Speed = 8100000, Status = Downloading });
        Downloads.Add(new DownloadItem { FilePath = @"C:\Downloads\Perfect.mp3", Url = "https://example.com/perfect.mp3", Downloaded = 8700000, Total = 8700000, Status = Completed });
    }
    private void AddBrowserLink(string url) => AddExternalLink(url);
    public void AddExternalLink(string url) { try { if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")) return; Directory.CreateDirectory(settings.DefaultFolder); var item = new DownloadItem { Url = url, FilePath = UniquePath(Path.Combine(settings.DefaultFolder, GetFileName(url))) }; Downloads.Add(item); SaveState(); NotifyStats(); Enqueue(item); Activate(); } catch { } }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else DragMove();
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Language_Click(object sender, RoutedEventArgs e) { settings.Language = settings.Language == "en" ? "fa" : "en"; settings.Save(); ApplyLanguage(); }
    private void ApplyLanguage() { bool english = settings.Language == "en"; LocalizationService.Apply(this, english); FlowDirection = FlowDirection.RightToLeft; MainShell.FlowDirection = FlowDirection.LeftToRight; SidebarPanel.FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft; CenterPanel.FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft; DetailsPanel.FlowDirection = FlowDirection.LeftToRight; SearchHint.Text = english ? "Search downloads..." : "جست‌وجو..."; NoSelectionTitle.Text = english ? "Select a download" : "یک دانلود را انتخاب کنید"; NoSelectionHint.Text = english ? "File details will appear here" : "جزئیات فایل اینجا نمایش داده می‌شود"; LanguageButton.Content = english ? "FA" : "EN"; foreach (var item in Downloads) item.RefreshLocalization(); NotifyStats(); }
    private void MainWindow_Loaded(object sender, RoutedEventArgs e) { ApplyLanguage(); CenterPanel.Opacity = 0; var move = new TranslateTransform(0, 14); CenterPanel.RenderTransform = move; CenterPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }); move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }); }
    private void Settings_Click(object sender, RoutedEventArgs e) { if (new SettingsWindow(settings) { Owner = this }.ShowDialog() == true) { ApplyTheme(); RefreshEngineIfIdle(); PumpQueue(); } }
    private void RefreshEngineIfIdle() { if (activeCount == 0 && engineProxy != settings.ProxyUrl) { engine.Dispose(); engineProxy = settings.ProxyUrl; engine = new(engineProxy); } }
    private void ApplyTheme() { }
    private void Search_Changed(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid == null) return; string text = SearchBox?.Text?.Trim() ?? ""; string tag = (FilterBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        CollectionViewSource.GetDefaultView(Downloads).Filter = value => value is DownloadItem x && (text.Length == 0 || x.FileName.Contains(text, StringComparison.CurrentCultureIgnoreCase) || x.Url.Contains(text, StringComparison.OrdinalIgnoreCase)) && (tag == "all" || tag == "active" && x.Cancellation != null || tag == "done" && x.Status == Completed);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDownloadWindow(settings.DefaultFolder) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try { Directory.CreateDirectory(dialog.Folder); settings.DefaultFolder = dialog.Folder; settings.Save(); var item = new DownloadItem { Url = dialog.DownloadUrl, FilePath = UniquePath(Path.Combine(dialog.Folder, GetFileName(dialog.DownloadUrl))) }; Downloads.Add(item); SaveState(); NotifyStats(); Enqueue(item); }
        catch (Exception ex) { MessageBox.Show(this, ErrorPrefix + ex.Message, "DownloadYar", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Start_Click(object sender, RoutedEventArgs e) { if (SelectedDownload != null) Enqueue(SelectedDownload); }
    private void Pause_Click(object sender, RoutedEventArgs e) => SelectedDownload?.Cancellation?.Cancel();
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedDownload; if (item == null) return;
        string text = $"\u00AB{item.FileName}\u00BB \u0627\u0632 \u0641\u0647\u0631\u0633\u062A \u062D\u0630\u0641 \u0634\u0648\u062F\u061F\n\u0641\u0627\u06CC\u0644 \u0631\u0648\u06CC \u062F\u06CC\u0633\u06A9 \u067E\u0627\u06A9 \u0646\u0645\u06CC\u200C\u0634\u0648\u062F.";
        if (MessageBox.Show(this, text, "\u062D\u0630\u0641", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        item.Cancellation?.Cancel(); Downloads.Remove(item); SaveState(); NotifyStats();
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { string folder = SelectedDownload == null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : Path.GetDirectoryName(SelectedDownload.FilePath)!; Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ErrorPrefix + ex.Message, "DownloadYar", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Enqueue(DownloadItem item)
    {
        if (item.Cancellation != null || pending.Contains(item)) return;
        item.Status = "\u062F\u0631 \u0635\u0641"; pending.Add(item); pending.Sort((a, b) => b.Priority.CompareTo(a.Priority)); SaveState(); PumpQueue();
    }
    private void PriorityUp_Click(object sender, RoutedEventArgs e) { if (SelectedDownload == null) return; SelectedDownload.Priority++; pending.Sort((a,b) => b.Priority.CompareTo(a.Priority)); SaveState(); }
    private void PriorityDown_Click(object sender, RoutedEventArgs e) { if (SelectedDownload == null) return; SelectedDownload.Priority--; pending.Sort((a,b) => b.Priority.CompareTo(a.Priority)); SaveState(); }
    private void PumpQueue()
    {
        while (activeCount < Math.Clamp(settings.MaxConcurrentDownloads, 1, 10) && pending.Count > 0)
        {
            var item = pending[0]; pending.RemoveAt(0); activeCount++; _ = RunQueued(item);
        }
        NotifyStats();
    }
    private async Task RunQueued(DownloadItem item) { try { await StartDownload(item); } finally { activeCount--; RefreshEngineIfIdle(); PumpQueue(); } }
    private async Task StartDownload(DownloadItem item)
    {
        if (item.Cancellation != null) return;
        var cts = new CancellationTokenSource(); item.Cancellation = cts; item.Status = Connecting; item.Speed = 0; NotifyStats();
        try
        {
            item.Status = Downloading;
            var progress = new Progress<DownloadProgress>(p => { item.Downloaded = p.Downloaded; item.Total = p.Total; if (p.BytesPerSecond > 0) item.Speed = p.BytesPerSecond; });
            await engine.DownloadAsync(item, settings.ConnectionsPerDownload, settings.SpeedLimitBytesPerSecond, progress, cts.Token);
            item.Sha256 = await SecurityService.Sha256Async(item.FilePath, cts.Token);
            if (settings.ScanAfterDownload) await SecurityService.ScanWithDefenderAsync(item.FilePath, cts.Token);
            item.Status = Completed; item.Speed = 0;
            if (settings.ShowNotifications) new ToastWindow(item.FileName).Show();
        }
        catch (OperationCanceledException) { item.Status = Paused; item.Speed = 0; }
        catch (Exception ex) { item.Status = ErrorPrefix + ex.Message; item.Speed = 0; }
        finally { cts.Dispose(); if (item.Cancellation == cts) item.Cancellation = null; SaveState(); NotifyStats(); }
    }
    private void LoadState() { try { if (!File.Exists(StateFile)) return; foreach (var x in JsonSerializer.Deserialize<List<DownloadItem>>(File.ReadAllText(StateFile)) ?? []) { if (x.Status == Connecting || x.Status == Downloading) x.Status = Paused; Downloads.Add(x); } } catch { } }
    private void SaveState() { try { Directory.CreateDirectory(stateDir); string temp = StateFile + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(Downloads, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, StateFile, true); } catch { } }
    private void OnClosing(object? sender, CancelEventArgs e) { linkReceiver.Dispose(); foreach (var x in Downloads) if (x.Cancellation != null) { x.Status = Paused; x.Cancellation.Cancel(); } SaveState(); engine.Dispose(); }
    private static string GetFileName(string url) { string n = Uri.UnescapeDataString(Path.GetFileName(new Uri(url).AbsolutePath)); foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return string.IsNullOrWhiteSpace(n) ? $"download-{DateTimeOffset.Now.ToUnixTimeSeconds()}.bin" : n; }
    private static string UniquePath(string p) { if (!File.Exists(p)) return p; string dir = Path.GetDirectoryName(p)!, name = Path.GetFileNameWithoutExtension(p), ext = Path.GetExtension(p); for (int i = 2; ; i++) { string x = Path.Combine(dir, $"{name} ({i}){ext}"); if (!File.Exists(x)) return x; } }
    private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
    private void NotifyStats() { Notify(nameof(FooterText)); Notify(nameof(ActiveCountText)); Notify(nameof(TotalCountText)); }
}
