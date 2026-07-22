using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace DownloadYarPro;
public sealed class LinkReceiver : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 17845); private CancellationTokenSource? cancellation;
    public event Action<string>? LinkReceived;
    public void Start() { try { listener.Start(); cancellation = new(); _ = Listen(cancellation.Token); } catch { } }
    private async Task Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested) try { var client = await listener.AcceptTcpClientAsync(token); _ = Handle(client, token); } catch when (token.IsCancellationRequested) { break; } catch { await Task.Delay(250, token); }
    }
    private async Task Handle(TcpClient client, CancellationToken token)
    {
        using (client) { var stream = client.GetStream(); var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true); string first = await reader.ReadLineAsync(token) ?? ""; while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token))) { }
        string target = first.Split(' ').ElementAtOrDefault(1) ?? "/"; bool valid = Uri.TryCreate("http://127.0.0.1" + target, UriKind.Absolute, out var local) && local.AbsolutePath == "/add"; string? url = valid ? ParseUrl(local!.Query) : null; valid = Uri.TryCreate(url, UriKind.Absolute, out var parsed) && (parsed.Scheme == "http" || parsed.Scheme == "https"); if (valid) LinkReceived?.Invoke(url!);
        byte[] body = Encoding.ASCII.GetBytes(valid ? "OK" : "INVALID"); byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"); await stream.WriteAsync(headers, token); await stream.WriteAsync(body, token); }
    }
    private static string? ParseUrl(string query) { foreach (string pair in query.TrimStart('?').Split('&')) { int i = pair.IndexOf('='); if (i > 0 && pair[..i] == "url") return Uri.UnescapeDataString(pair[(i + 1)..].Replace('+', ' ')); } return null; }
    public void Dispose() { cancellation?.Cancel(); listener.Stop(); cancellation?.Dispose(); }
}
