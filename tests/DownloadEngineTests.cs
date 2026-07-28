using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TaynDM;
using Xunit;

namespace TaynDM.Tests;

public class DownloadEngineTests : IDisposable
{
    private readonly byte[] _payload;
    private readonly TestServer _server;
    private readonly string _root;

    public DownloadEngineTests()
    {
        _payload = new byte[3 * 1024 * 1024 + 731];
        RandomNumberGenerator.Fill(_payload);
        _server = new TestServer(_payload);
        _server.Start();
        _root = Path.Combine(Path.GetTempPath(), "TaynDMTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _server.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task SegmentedRangeDownload()
    {
        string path = Path.Combine(_root, "range.bin");
        var item = new DownloadItem
        {
            Url = _server.Url + "/range",
            FilePath = path
        };
        await new DownloadEngine().DownloadAsync(
            item, 8, 0, new Progress<DownloadProgress>(), CancellationToken.None);
        Assert.True(
            File.ReadAllBytes(path).SequenceEqual(_payload),
            "segmented download output mismatch");
    }

    [Fact]
    public async Task WrongHeadLength_FallsBack()
    {
        string path = Path.Combine(_root, "wrong-head.bin");
        var item = new DownloadItem
        {
            Url = _server.Url + "/range-head-wrong",
            FilePath = path
        };
        await new DownloadEngine().DownloadAsync(
            item, 8, 0, new Progress<DownloadProgress>(), CancellationToken.None);
        Assert.True(
            File.ReadAllBytes(path).SequenceEqual(_payload),
            "wrong HEAD length fallback output mismatch");
    }

    [Fact]
    public async Task SingleFallbackDownload()
    {
        string path = Path.Combine(_root, "norange.bin");
        var item = new DownloadItem
        {
            Url = _server.Url + "/norange",
            FilePath = path
        };
        await new DownloadEngine().DownloadAsync(
            item, 8, 0, new Progress<DownloadProgress>(), CancellationToken.None);
        Assert.True(
            File.ReadAllBytes(path).SequenceEqual(_payload),
            "single fallback download output mismatch");
    }

    [Fact]
    public async Task PauseAndResume()
    {
        string path = Path.Combine(_root, "resume.bin");
        var item = new DownloadItem
        {
            Url = _server.Url + "/range",
            FilePath = path
        };

        // Download partially, then cancel
        using var cts = new CancellationTokenSource(450);
        try
        {
            await new DownloadEngine().DownloadAsync(
                item, 8, 256 * 1024, new Progress<DownloadProgress>(), cts.Token);
        }
        catch (OperationCanceledException) { }

        Assert.True(
            Directory.Exists(path + ".dy-parts"),
            "partial segments were not preserved");

        // Resume and complete
        await new DownloadEngine().DownloadAsync(
            item, 8, 0, new Progress<DownloadProgress>(), CancellationToken.None);
        Assert.True(
            File.ReadAllBytes(path).SequenceEqual(_payload),
            "resumed output mismatch");
    }

    [Fact]
    public async Task ExpiredLink_Fails()
    {
        string path = Path.Combine(_root, "expired.bin");
        var item = new DownloadItem
        {
            Url = _server.Url + "/expired",
            FilePath = path
        };
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await new DownloadEngine().DownloadAsync(
                item, 4, 0, new Progress<DownloadProgress>(), CancellationToken.None));
    }

    [Fact]
    public async Task TruncatedResponse_Fails()
    {
        string path = Path.Combine(_root, "truncated.bin");
        var item = new DownloadItem
        {
            Url = _server.Url + "/truncated",
            FilePath = path
        };
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await new DownloadEngine().DownloadAsync(
                item, 1, 0, new Progress<DownloadProgress>(), CancellationToken.None));
    }

    [Fact]
    public void AppSettings_SaveLoad_Roundtrip()
    {
        var original = new AppSettings
        {
            MaxConcurrentDownloads = 5,
            ConnectionsPerDownload = 16,
            SpeedLimitBytesPerSecond = 1024 * 1024,
            DefaultFolder = "/tmp/test-downloads",
            ShowNotifications = true,
            ScanAfterDownload = false,
            RunAtStartup = true,
            ProxyUrl = "http://proxy:8080",
            Language = "en"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded.MaxConcurrentDownloads);
        Assert.Equal(16, loaded.ConnectionsPerDownload);
        Assert.Equal(1024 * 1024, loaded.SpeedLimitBytesPerSecond);
        Assert.Equal("/tmp/test-downloads", loaded.DefaultFolder);
        Assert.True(loaded.ShowNotifications);
        Assert.False(loaded.ScanAfterDownload);
        Assert.True(loaded.RunAtStartup);
        Assert.Equal("http://proxy:8080", loaded.ProxyUrl);
        Assert.Equal("en", loaded.Language);
    }

    [Fact]
    public void DownloadItem_Properties_Work()
    {
        var item = new DownloadItem
        {
            Url = "https://example.com/test.zip",
            FilePath = Path.Combine(_root, "test.zip"),
            Status = "تکمیل‌شده",
            Downloaded = 1024,
            Total = 1024
        };

        Assert.Equal("test.zip", item.FileName);
        Assert.Equal(100.0, item.Percent);
        Assert.Equal("▼", item.FileGlyph);  // .zip extension
        Assert.Equal("1.0 KB", DownloadItem.Format(1024));
    }

    [Fact]
    public void DownloadItem_Formats_BytesCorrectly()
    {
        Assert.Equal("0 B", DownloadItem.Format(0));
        Assert.Equal("512 B", DownloadItem.Format(512));
        Assert.Equal("1.0 KB", DownloadItem.Format(1024));
        Assert.Equal("1.5 MB", DownloadItem.Format(1572864));
        Assert.Equal("2.0 GB", DownloadItem.Format(2147483648L));
    }

    [Fact]
    public void LocalizationService_Status_Conversion()
    {
        // English mode
        LocalizationService.Apply(new System.Windows.Window(), true);

        string? status = LocalizationService.Status("تکمیل‌شده");
        Assert.Equal("Completed", status);

        status = LocalizationService.Status("متوقف‌شده");
        Assert.Equal("Paused", status);

        status = LocalizationService.Status("خطا: test error");
        Assert.Equal("Error: test error", status);

        // Persian mode
        LocalizationService.Apply(new System.Windows.Window(), false);

        status = LocalizationService.Status("تکمیل‌شده");
        Assert.Equal("تکمیل‌شده", status);
    }

    [Fact]
    public void LinkReceiver_DynamicPort()
    {
        using var receiver1 = new LinkReceiver();
        using var receiver2 = new LinkReceiver();

        receiver1.Start();
        receiver2.Start();

        Assert.NotEqual(0, receiver1.Port);
        Assert.NotEqual(0, receiver2.Port);
        Assert.NotEqual(receiver1.Port, receiver2.Port);
    }
}

/// <summary>
/// Lightweight HTTP test server for engine tests.
/// Supports range requests, HEAD, fallback, error, and truncation routes.
/// </summary>
sealed class TestServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly byte[] _data;
    private CancellationTokenSource _source = new();

    public string Url { get; }

    public TestServer(byte[] bytes)
    {
        _data = bytes;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var ep = (IPEndPoint)_listener.LocalEndpoint;
        Url = $"http://127.0.0.1:{ep.Port}";
    }

    public void Start() => _ = Loop();

    private async Task Loop()
    {
        while (!_source.IsCancellationRequested)
        {
            try
            {
                var c = await _listener.AcceptTcpClientAsync(_source.Token);
                _ = Handle(c);
            }
            catch { break; }
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
            string first = await reader.ReadLineAsync() ?? "";
            var request = first.Split(' ');
            string method = request.ElementAtOrDefault(0) ?? "",
                   route = request.ElementAtOrDefault(1) ?? "";
            string? rangeHeader = null;
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                    rangeHeader = line[6..].Trim();

            if (route == "/expired")
            {
                await Send(stream,
                    "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                    null);
                return;
            }

            bool ranges = route == "/range" || route == "/range-head-wrong";
            long start = 0, end = _data.Length - 1;
            bool partial = ranges && rangeHeader != null;
            if (partial)
            {
                var pair = rangeHeader![6..].Split('-');
                start = long.Parse(pair[0]);
                if (pair.Length > 1 && long.TryParse(pair[1], out long parsed))
                    end = Math.Min(parsed, end);
            }

            long length = end - start + 1;
            long advertised = method == "HEAD" && route == "/range-head-wrong"
                ? _data.Length / 2
                : partial ? length : _data.Length;

            string headers =
                $"HTTP/1.1 {(partial ? "206 Partial Content" : "200 OK")}\r\n" +
                $"Content-Length: {advertised}\r\n" +
                $"{(ranges ? "Accept-Ranges: bytes\r\n" : "")}" +
                $"{(partial ? $"Content-Range: bytes {start}-{end}/{_data.Length}\r\n" : "")}" +
                $"Connection: close\r\n\r\n";

            long sent = route == "/truncated" ? _data.Length / 2 : length;
            await Send(stream, headers,
                method == "HEAD" ? null : _data.AsMemory((int)start, (int)sent));
        }
    }

    private static async Task Send(NetworkStream stream, string headers, ReadOnlyMemory<byte>? body)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
        if (body.HasValue)
            await stream.WriteAsync(body.Value);
    }

    public void Dispose()
    {
        _source.Cancel();
        _listener.Stop();
        _source.Dispose();
    }
}
