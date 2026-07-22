using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
namespace DownloadYarPro;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); var window = new MainWindow();
        string? screenshot = Environment.GetEnvironmentVariable("DOWNLOADYAR_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(screenshot)) window.ContentRendered += async (_, _) => { await Task.Delay(400); var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(screenshot); encoder.Save(file); window.Close(); };
        window.Show();
        if (e.Args.FirstOrDefault() is string arg && arg.StartsWith("downloadyar://", StringComparison.OrdinalIgnoreCase)) { try { var uri = new Uri(arg); string? url = HttpUtility.ParseQueryString(uri.Query)["url"]; if (url != null) window.AddExternalLink(url); } catch { } }
    }
}
