using System.Windows;
namespace DownloadYarPro;
public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    public SettingsWindow(AppSettings value)
    {
        InitializeComponent(); settings = value; Loaded += (_, _) => LocalizationService.Apply(this, value.Language == "en");
        ConcurrentBox.ItemsSource = Enumerable.Range(1, 10); ConnectionsBox.ItemsSource = new[] { 1, 2, 4, 8, 12, 16 };
        ConcurrentBox.SelectedItem = value.MaxConcurrentDownloads; ConnectionsBox.SelectedItem = value.ConnectionsPerDownload;
        SpeedBox.Text = (value.SpeedLimitBytesPerSecond / 1024).ToString(); FolderBox.Text = value.DefaultFolder; ProxyBox.Text = value.ProxyUrl;
        NotificationBox.IsChecked = value.ShowNotifications; ScanBox.IsChecked = value.ScanAfterDownload; StartupBox.IsChecked = value.RunAtStartup; DarkBox.IsChecked = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        settings.MaxConcurrentDownloads = (int)(ConcurrentBox.SelectedItem ?? 3); settings.ConnectionsPerDownload = (int)(ConnectionsBox.SelectedItem ?? 8);
        settings.SpeedLimitBytesPerSecond = long.TryParse(SpeedBox.Text, out long kb) ? Math.Max(0, kb) * 1024 : 0; settings.DefaultFolder = FolderBox.Text.Trim();
        settings.ShowNotifications = NotificationBox.IsChecked == true; settings.ScanAfterDownload = ScanBox.IsChecked == true; settings.ProxyUrl = ProxyBox.Text.Trim(); settings.RunAtStartup = StartupBox.IsChecked == true; settings.DarkTheme = true; settings.Save();
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"); if (settings.RunAtStartup) key.SetValue("TaynDM", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("TaynDM", false); DialogResult = true;
    }
}
