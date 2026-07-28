using System.Windows;
using System.Windows.Threading;

namespace TaynDM;

/// <summary>
/// WPF-compatible clipboard monitor that polls the clipboard every second
/// for URLs (http/https) and fires a ClipboardUrlDetected event.
/// Uses DispatcherTimer to ensure callbacks run on the UI thread.
/// </summary>
public sealed class ClipboardMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private string _lastText = string.Empty;

    /// <summary>
    /// Fires when a URL is detected on the clipboard.
    /// The string argument is the detected URL.
    /// </summary>
    public event EventHandler<string>? ClipboardUrlDetected;

    /// <summary>
    /// Fires when any clipboard text change is detected (including non-URLs).
    /// </summary>
    public event EventHandler<string>? ClipboardTextChanged;

    /// <summary>
    /// The polling interval in milliseconds. Default is 1000ms (1 second).
    /// </summary>
    public int IntervalMs { get; }

    /// <summary>
    /// When true, only http/https URLs trigger ClipboardUrlDetected.
    /// ClipboardTextChanged still fires for any text.
    /// </summary>
    public bool FilterUrlsOnly { get; set; } = true;

    /// <summary>
    /// Creates a new ClipboardMonitor with the specified polling interval.
    /// </summary>
    /// <param name="intervalMs">Polling interval in milliseconds (default: 1000).</param>
    public ClipboardMonitor(int intervalMs = 1000)
    {
        IntervalMs = intervalMs;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Starts monitoring the clipboard.
    /// </summary>
    public void Start()
    {
        if (_timer.IsEnabled) return;

        // Capture the current clipboard content so we don't re-fire on existing text
        try
        {
            if (Clipboard.ContainsText())
                _lastText = Clipboard.GetText();
        }
        catch
        {
            // Clipboard might be locked by another process; ignore
        }

        _timer.Start();
    }

    /// <summary>
    /// Stops monitoring the clipboard.
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
    }

    /// <summary>
    /// Returns whether monitoring is currently active.
    /// </summary>
    public bool IsRunning => _timer.IsEnabled;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        string? currentText = null;

        try
        {
            if (Clipboard.ContainsText())
                currentText = Clipboard.GetText();
        }
        catch
        {
            // Clipboard access can fail if another app holds a lock
            return;
        }

        if (currentText is null || currentText == _lastText)
            return;

        _lastText = currentText;
        ClipboardTextChanged?.Invoke(this, currentText);

        if (FilterUrlsOnly && !string.IsNullOrWhiteSpace(currentText))
        {
            string trimmed = currentText.Trim();
            if (IsUrl(trimmed))
            {
                ClipboardUrlDetected?.Invoke(this, trimmed);
            }
        }
    }

    private static bool IsUrl(string text)
    {
        return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
