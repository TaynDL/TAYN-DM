using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
namespace DownloadYarPro;
public partial class AddDownloadWindow : Window
{
    public string DownloadUrl => UrlBox.Text.Trim(); public string Folder => FolderBox.Text.Trim();
    public AddDownloadWindow(string folder) { InitializeComponent(); FolderBox.Text = folder; Loaded += (_, _) => { LocalizationService.Apply(this, AppSettings.Load().Language == "en"); UrlBox.Focus(); }; }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Browse_Click(object sender, RoutedEventArgs e) { var d = new OpenFolderDialog { InitialDirectory = FolderBox.Text, Multiselect = false }; if (d.ShowDialog() == true) FolderBox.Text = d.FolderName; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https")) { MessageBox.Show(this, "\u06CC\u06A9 \u0622\u062F\u0631\u0633 \u0645\u0639\u062A\u0628\u0631 HTTP \u06CC\u0627 HTTPS \u0648\u0627\u0631\u062F \u06A9\u0646\u06CC\u062F.", "\u0622\u062F\u0631\u0633 \u0646\u0627\u0645\u0639\u062A\u0628\u0631", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (string.IsNullOrWhiteSpace(Folder)) { MessageBox.Show(this, "\u067E\u0648\u0634\u0647 \u0630\u062E\u06CC\u0631\u0647\u200C\u0633\u0627\u0632\u06CC \u0631\u0627 \u0627\u0646\u062A\u062E\u0627\u0628 \u06A9\u0646\u06CC\u062F."); return; }
        DialogResult = true;
    }
}
