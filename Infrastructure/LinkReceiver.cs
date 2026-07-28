using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace TaynDM;

public sealed class LinkReceiver : IDisposable
{
    private TcpListener? listener;
    private CancellationTokenSource? cancellation;

    public event Action<string>? LinkReceived;

    /// <summary>
    /// The port actually bound by the TCP listener.
    /// 0 if <see cref="Start"/> has not been called yet.
    /// </summary>
    public int Port { get; private set; }

    public void Start()
    {
        try
        {
            // Try to find an available port starting from 17845
            const int preferredPort = 17845;
            const int maxAttempts = 20;
            int port = preferredPort;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var testListener = new TcpListener(IPAddress.Loopback, port);
                    testListener.Start();
                    testListener.Stop();
                    // Port is available — use it
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    Port = port;
                    break;
                }
                catch (SocketException)
                {
                    // Port in use, try next
                    port++;
                }
            }

            // If we exhausted attempts, fall back to OS-assigned port
            if (listener == null)
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            }

            cancellation = new CancellationTokenSource();
            _ = Listen(cancellation.Token);
        }
        catch
        {
            // Silent failure — matching original behavior
        }
    }

    private async Task Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await listener!.AcceptTcpClientAsync(token);
                _ = Handle(client, token);
            }
            catch when (token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, token);
            }
        }
    }

    private async Task Handle(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
            string first = await reader.ReadLineAsync(token) ?? "";

            // Consume remaining headers
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token))) { }

            string target = first.Split(' ').ElementAtOrDefault(1) ?? "/";
            bool valid = Uri.TryCreate("http://127.0.0.1" + target, UriKind.Absolute, out var local)
                         && local.AbsolutePath == "/add";
            string? url = valid ? ParseUrl(local!.Query) : null;

            valid = Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                    && (parsed.Scheme == "http" || parsed.Scheme == "https");

            if (valid) LinkReceived?.Invoke(url!);

            byte[] body = Encoding.ASCII.GetBytes(valid ? "OK" : "INVALID");
            byte[] headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\n" +
                $"Access-Control-Allow-Origin: *\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                $"Connection: close\r\n\r\n");

            await stream.WriteAsync(headers, token);
            await stream.WriteAsync(body, token);
        }
    }

    private static string? ParseUrl(string query)
    {
        foreach (string pair in query.TrimStart('?').Split('&'))
        {
            int i = pair.IndexOf('=');
            if (i > 0 && pair[..i] == "url")
                return Uri.UnescapeDataString(pair[(i + 1)..].Replace('+', ' '));
        }
        return null;
    }

    public void Dispose()
    {
        cancellation?.Cancel();
        listener?.Stop();
        cancellation?.Dispose();
    }
}
