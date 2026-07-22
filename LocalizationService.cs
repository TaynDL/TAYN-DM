using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DownloadYarPro;
public static class LocalizationService
{
    public static bool English { get; private set; }
    private static readonly Dictionary<string, string> Map = new()
    {
        ["مرکز دانلود"]="Download Center", ["فایل‌هایت را سریع، ایمن و بدون وقفه دریافت کن"]="Fast, secure and uninterrupted transfers",
        ["دانلود جدید"]="New Download", ["دانلود فعال"]="Active Downloads", ["کل فایل‌ها"]="Total Files", ["همه"]="All", ["فعال"]="Active",
        ["همه دانلودها"]="All Downloads", ["در حال دانلود"]="Downloading", ["متوقف‌شده"]="Paused", ["تکمیل‌شده"]="Completed", ["خطاها"]="Failed",
        ["ویدیو"]="Video", ["موسیقی"]="Music", ["اسناد"]="Documents", ["تنظیمات"]="Settings", ["شروع"]="Start", ["توقف"]="Pause", ["پوشه"]="Folder",
        ["صف دانلود خالی است"]="Your download queue is empty", ["اولین فایل خود را اضافه کن"]="Add your first file to get started", ["آماده برای دریافت"]="Ready for transfers",
        ["لینک مستقیم فایل"]="Direct file URL", ["محل ذخیره‌سازی"]="Save location", ["انتخاب"]="Browse", ["انصراف"]="Cancel", ["شروع دانلود"]="Start Download",
        ["تنظیمات دانلودیار"]="TAYN DM Settings", ["دانلود هم‌زمان"]="Concurrent downloads", ["اتصال هر فایل"]="Connections per file", ["محدودیت سرعت (KB/s)"]="Speed limit (KB/s)",
        ["پوشه پیش‌فرض"]="Default folder", ["نمایش اعلان پس از اتمام"]="Show completion notifications", ["اسکن فایل با Windows Defender"]="Scan with Windows Defender", ["اجرای خودکار با Windows"]="Start with Windows", ["ذخیره تنظیمات"]="Save Settings",
        ["در صف"]="Queued", ["در حال اتصال"]="Connecting", ["خطا: "]="Error: "
    };
    public static void Apply(Window window, bool english)
    {
        English = english; window.FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft; TranslateTree(window, english);
    }
    private static void TranslateTree(DependencyObject node, bool english)
    {
        if (node is TextBlock text && !string.IsNullOrEmpty(text.Text) && text.GetBindingExpression(TextBlock.TextProperty) == null) text.Text = Convert(text, text.Text, english);
        else if (node is Button button && button.Content is string value) button.Content = Convert(button, value, english);
        else if (node is CheckBox check && check.Content is string checkValue) check.Content = Convert(check, checkValue, english);
        else if (node is ComboBoxItem item && item.Content is string itemValue) item.Content = Convert(item, itemValue, english);
        int count = VisualTreeHelper.GetChildrenCount(node); for (int i = 0; i < count; i++) TranslateTree(VisualTreeHelper.GetChild(node, i), english);
    }
    private static string Convert(FrameworkElement element, string current, bool english)
    {
        if (english) { if (element.Tag == null) element.Tag = current; if (Map.TryGetValue(current.Trim(), out string? translated)) return PreservePadding(current, translated); foreach (var pair in Map.OrderByDescending(x => x.Key.Length)) if (current.Contains(pair.Key, StringComparison.Ordinal)) return current.Replace(pair.Key, pair.Value, StringComparison.Ordinal); return current; }
        return element.Tag is string original ? original : current;
    }
    private static string PreservePadding(string source, string value) => source.StartsWith(" ") ? " " + value : value;
    public static string Status(string value) { if (!English) return value; foreach (var pair in Map) if (value == pair.Key) return pair.Value; if (value.StartsWith("خطا: ")) return "Error: " + value[6..]; return value; }
}
