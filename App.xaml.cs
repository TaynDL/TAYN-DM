using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaynDM;

public partial class App : Application
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadYar", "logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers — log crash instead of silent failure
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            args.Handled = true; // prevent immediate crash
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("TaskScheduler", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);

        try
        {
            var window = new Views.MainWindow();

            string? screenshot = Environment.GetEnvironmentVariable("DOWNLOADYAR_SCREENSHOT");
            if (!string.IsNullOrWhiteSpace(screenshot))
            {
                window.ContentRendered += async (_, _) =>
                {
                    await Task.Delay(400);
                    var image = new RenderTargetBitmap(
                        (int)window.ActualWidth, (int)window.ActualHeight,
                        96, 96, PixelFormats.Pbgra32);
                    image.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using var file = File.Create(screenshot);
                    encoder.Save(file);
                    window.Close();
                };
            }

            window.Show();

            if (e.Args.FirstOrDefault() is string arg
                && arg.StartsWith("downloadyar://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(arg);
                    string? url = HttpUtility.ParseQueryString(uri.Query)["url"];
                    if (url != null) window.AddExternalLink(url);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogCrash("Startup", ex);
            MessageBox.Show(
                $"Application failed to start:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "TAYN DM — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            string path = Path.Combine(LogDir, $"crash-{DateTime.Now:yyyy-MM-dd}.log");
            string entry = $"[{DateTime.Now:HH:mm:ss}] [{source}] {ex}\n";
            File.AppendAllText(path, entry);
        }
        catch { /* logger should never crash */ }
    }
}
