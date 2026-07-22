using System.Windows;
namespace DownloadYarPro;
public partial class ToastWindow : Window
{
    public ToastWindow(string message) { InitializeComponent(); MessageText.Text = message; Loaded += async (_, _) => { var area = SystemParameters.WorkArea; Left = area.Right - Width - 18; Top = area.Bottom - Height - 18; await Task.Delay(4500); Close(); }; }
}
