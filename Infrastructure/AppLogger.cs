using System;
using System.IO;
using System.Threading;

namespace TaynDM;

/// <summary>
/// Simple file-based logger that writes timestamped entries to
/// %LOCALAPPDATA%/DownloadYar/logs/log-{date}.txt.
/// Thread-safe via lock. Implements ILogger for DI compatibility.
/// </summary>
public sealed class AppLogger : ILogger
{
    private static readonly object _lock = new();
    private static readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadYar",
        "logs");

    /// <summary>Shared singleton instance.</summary>
    public static AppLogger Instance { get; } = new();

    /// <summary>
    /// Log an informational message.
    /// </summary>
    public void LogInfo(string message)
    {
        WriteEntry("INFO", message);
    }

    /// <summary>
    /// Log a warning message.
    /// </summary>
    public void LogWarning(string message)
    {
        WriteEntry("WARN", message);
    }

    /// <summary>
    /// Log an error message, optionally including exception details.
    /// </summary>
    public void LogError(string message, Exception? ex = null)
    {
        string fullMessage = message;
        if (ex != null)
        {
            fullMessage += $"\n  Exception: {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
                fullMessage += $"\n  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            if (ex.StackTrace != null)
                fullMessage += $"\n  StackTrace: {ex.StackTrace}";
        }
        WriteEntry("ERROR", fullMessage);
    }

    private static void WriteEntry(string level, string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"[{timestamp}] [{level}] {message}";
            string filePath = GetLogFilePath();
            string? dir = Path.GetDirectoryName(filePath);

            lock (_lock)
            {
                if (dir != null) Directory.CreateDirectory(dir);
                File.AppendAllText(filePath, logLine + Environment.NewLine);
            }
        }
        catch
        {
            // Silent failure — logger should never crash the app
        }
    }

    private static string GetLogFilePath()
    {
        string dateSuffix = DateTime.Now.ToString("yyyy-MM-dd");
        return Path.Combine(_logDirectory, $"log-{dateSuffix}.txt");
    }
}
