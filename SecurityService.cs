using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace DownloadYarPro;
public static class SecurityService
{
    public static async Task<string> Sha256Async(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        byte[] hash = await SHA256.HashDataAsync(stream, token); return Convert.ToHexString(hash).ToLowerInvariant();
    }
    public static async Task ScanWithDefenderAsync(string path, CancellationToken token)
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string platform = Path.Combine(programData, "Microsoft", "Windows Defender", "Platform");
        string? exe = Directory.Exists(platform) ? Directory.GetDirectories(platform).OrderByDescending(x => x).Select(x => Path.Combine(x, "MpCmdRun.exe")).FirstOrDefault(File.Exists) : null;
        exe ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
        if (!File.Exists(exe)) return;
        using var process = Process.Start(new ProcessStartInfo(exe, $"-Scan -ScanType 3 -File \"{path}\" -DisableRemediation") { CreateNoWindow = true, UseShellExecute = false });
        if (process != null) await process.WaitForExitAsync(token);
    }
}
