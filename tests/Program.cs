using DownloadYarPro;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;

byte[] payload = new byte[3 * 1024 * 1024 + 731]; RandomNumberGenerator.Fill(payload);
using var server = new TestServer(payload); server.Start();
string root = Path.Combine(Path.GetTempPath(), "DownloadYarTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try
{
    await Test("segmented range download", "/range", 8, true);
    await Test("wrong HEAD length", "/range-head-wrong", 8, true);
    await Test("single fallback download", "/norange", 8, true);
    await TestResume();
    bool failed = false; try { await Test("expired link", "/expired", 4, false); } catch (HttpRequestException) { failed = true; }
    Assert(failed, "expired link must fail");
    failed = false; try { await Test("truncated response", "/truncated", 1, false); } catch { failed = true; }
    Assert(failed, "truncated response must fail"); Console.WriteLine("PASS: truncated response rejected");
    await TestBrowserBridge();
    Console.WriteLine("ALL ENGINE TESTS PASSED");
}
finally { Directory.Delete(root, true); }

async Task TestBrowserBridge()
{
    using var receiver = new LinkReceiver(); var completion = new TaskCompletionSource<string>(); receiver.LinkReceived += x => completion.TrySetResult(x); receiver.Start();
    using var client = new HttpClient(); string expected = "https://example.com/file name.zip"; string result = await client.GetStringAsync("http://127.0.0.1:17845/add?url=" + Uri.EscapeDataString(expected));
    Assert(result == "OK" && await completion.Task.WaitAsync(TimeSpan.FromSeconds(2)) == expected, "browser bridge failed"); Console.WriteLine("PASS: browser extension bridge");
}

async Task TestResume()
{
    string path = Path.Combine(root, "resume.bin"); var item = new DownloadItem { Url = server.Url + "/range", FilePath = path }; using var cts = new CancellationTokenSource(450);
    try { await new DownloadEngine().DownloadAsync(item, 8, 256 * 1024, new Progress<DownloadProgress>(), cts.Token); } catch (OperationCanceledException) { }
    Assert(Directory.Exists(path + ".dy-parts"), "partial segments were not preserved");
    await new DownloadEngine().DownloadAsync(item, 8, 0, new Progress<DownloadProgress>(), CancellationToken.None);
    Assert(File.ReadAllBytes(path).SequenceEqual(payload), "resumed output mismatch"); Console.WriteLine("PASS: pause and resume segmented download");
}

async Task Test(string name, string route, int connections, bool verify)
{
    string path = Path.Combine(root, name.Replace(' ', '-') + ".bin"); var item = new DownloadItem { Url = server.Url + route, FilePath = path };
    await new DownloadEngine().DownloadAsync(item, connections, 0, new Progress<DownloadProgress>(), CancellationToken.None);
    if (verify) Assert(File.ReadAllBytes(path).SequenceEqual(payload), name + " output mismatch");
    Console.WriteLine("PASS: " + name);
}
void Assert(bool value, string message) { if (!value) throw new Exception(message); }

sealed class TestServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 17846); private readonly byte[] data; private CancellationTokenSource source = new();
    public string Url { get; } = "http://127.0.0.1:17846";
    public TestServer(byte[] bytes) { data = bytes; }
    public void Start() { listener.Start(); _ = Loop(); }
    private async Task Loop() { while (!source.IsCancellationRequested) try { var c = await listener.AcceptTcpClientAsync(source.Token); _ = Handle(c); } catch { break; } }
    private async Task Handle(TcpClient client)
    {
        using (client) { var stream = client.GetStream(); var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true); string first = await reader.ReadLineAsync() ?? ""; var request = first.Split(' '); string method = request.ElementAtOrDefault(0) ?? "", route = request.ElementAtOrDefault(1) ?? ""; string? rangeHeader = null; string? line; while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) rangeHeader = line[6..].Trim();
        if (route == "/expired") { await Send(stream, "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", null); return; }
        bool ranges = route == "/range" || route == "/range-head-wrong";
        long start = 0, end = data.Length - 1;
        bool partial = ranges && rangeHeader != null; if (partial) { var pair = rangeHeader![6..].Split('-'); start = long.Parse(pair[0]); if (pair.Length > 1 && long.TryParse(pair[1], out long parsed)) end = Math.Min(parsed, end); }
        long length = end - start + 1; long advertised = method == "HEAD" && route == "/range-head-wrong" ? data.Length / 2 : partial ? length : data.Length; string headers = $"HTTP/1.1 {(partial ? "206 Partial Content" : "200 OK")}\r\nContent-Length: {advertised}\r\n{(ranges ? "Accept-Ranges: bytes\r\n" : "")}{(partial ? $"Content-Range: bytes {start}-{end}/{data.Length}\r\n" : "")}Connection: close\r\n\r\n";
        long sent = route == "/truncated" ? data.Length / 2 : length; await Send(stream, headers, method == "HEAD" ? null : data.AsMemory((int)start, (int)sent)); }
    }
    private static async Task Send(NetworkStream stream, string headers, ReadOnlyMemory<byte>? body) { await stream.WriteAsync(Encoding.ASCII.GetBytes(headers)); if (body.HasValue) await stream.WriteAsync(body.Value); }
    public void Dispose() { source.Cancel(); listener.Stop(); source.Dispose(); }
}
