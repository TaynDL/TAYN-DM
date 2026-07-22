using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DownloadYarPro;

public sealed record DownloadProbe(long Length, bool SupportsRanges, string? ETag, DateTimeOffset? LastModified);
public sealed record DownloadProgress(long Downloaded, long Total, double BytesPerSecond);

public sealed class DownloadEngine : IDisposable
{
    private readonly HttpClient Client;
    public DownloadEngine(string? proxyUrl = null) { Client = CreateClient(proxyUrl); }
    private static HttpClient CreateClient(string? proxyUrl)
    {
        var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None, ConnectTimeout = TimeSpan.FromSeconds(20), PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        if (!string.IsNullOrWhiteSpace(proxyUrl)) { handler.Proxy = new WebProxy(proxyUrl); handler.UseProxy = true; }
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DownloadYar/2.0");
        return client;
    }

    public async Task<DownloadProbe> ProbeAsync(string url, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        long headLength = response.IsSuccessStatusCode ? response.Content.Headers.ContentLength ?? 0 : 0;
        bool headRanges = response.IsSuccessStatusCode && response.Headers.AcceptRanges.Contains("bytes");
        string? etag = response.Headers.ETag?.Tag;
        DateTimeOffset? modified = response.Content.Headers.LastModified;

        // A surprising number of CDNs return a stale or approximate Content-Length to HEAD.
        // Confirm range support and the authoritative total using an actual one-byte GET.
        if (!response.IsSuccessStatusCode || headLength == 0 || headRanges)
        {
            using var range = new HttpRequestMessage(HttpMethod.Get, url); range.Headers.Range = new RangeHeaderValue(0, 0);
            using var ranged = await Client.SendAsync(range, HttpCompletionOption.ResponseHeadersRead, token); ranged.EnsureSuccessStatusCode();
            bool validRange = ranged.StatusCode == HttpStatusCode.PartialContent && ranged.Content.Headers.ContentRange is { From: 0, To: 0, Length: > 0 };
            long verifiedLength = validRange ? ranged.Content.Headers.ContentRange!.Length!.Value : ranged.Content.Headers.ContentLength ?? headLength;
            return new(verifiedLength, validRange, ranged.Headers.ETag?.Tag ?? etag, ranged.Content.Headers.LastModified ?? modified);
        }
        return new(headLength, false, etag, modified);
    }

    public async Task DownloadAsync(DownloadItem item, int connections, long speedLimit, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        var probe = await ProbeAsync(item.Url, token);
        string? root = Path.GetPathRoot(Path.GetFullPath(item.FilePath));
        if (probe.Length > 0 && root != null && new DriveInfo(root).AvailableFreeSpace < probe.Length + 16L * 1024 * 1024) throw new IOException("Not enough free disk space for this download.");
        item.ETag = probe.ETag; item.LastModified = probe.LastModified;
        if (probe.Length > 2 * 1024 * 1024 && probe.SupportsRanges && connections > 1)
            await DownloadSegmented(item, probe, Math.Clamp(connections, 2, 16), speedLimit, progress, token);
        else
            await DownloadSingle(item, probe, speedLimit, progress, token);
    }

    private async Task DownloadSegmented(DownloadItem item, DownloadProbe probe, int count, long speedLimit, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        string partsDir = item.FilePath + ".dy-parts"; Directory.CreateDirectory(partsDir);
        long segmentSize = (long)Math.Ceiling(probe.Length / (double)count);
        var ranges = Enumerable.Range(0, count).Select(i => (Index: i, Start: i * segmentSize, End: Math.Min(probe.Length - 1, (i + 1) * segmentSize - 1))).Where(x => x.Start <= x.End).ToArray();
        long initial = ranges.Sum(x => Math.Min(File.Exists(Part(x.Index)) ? new FileInfo(Part(x.Index)).Length : 0, x.End - x.Start + 1));
        long downloaded = initial; var speed = new SpeedMeter(initial); var limiter = new RateLimiter(speedLimit); progress.Report(new(initial, probe.Length, 0));
        await Parallel.ForEachAsync(ranges, new ParallelOptions { MaxDegreeOfParallelism = count, CancellationToken = token }, async (range, ct) =>
        {
            string path = Part(range.Index); long expected = range.End - range.Start + 1; long existing = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (existing > expected) { File.Delete(path); existing = 0; }
            if (existing == expected) return;
            using var req = new HttpRequestMessage(HttpMethod.Get, item.Url); req.Headers.Range = new RangeHeaderValue(range.Start + existing, range.End); SetIfRange(req, probe);
            using var res = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            long remaining = expected - existing;
            if (res.StatusCode != HttpStatusCode.PartialContent || res.Content.Headers.ContentRange?.From != range.Start + existing || res.Content.Headers.ContentRange?.To != range.End || res.Content.Headers.ContentRange?.Length != probe.Length || (res.Content.Headers.ContentLength is long contentLength && contentLength != remaining)) throw new InvalidDataException("Server returned an invalid byte range.");
            await using var input = await res.Content.ReadAsStreamAsync(ct); await using var output = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 131072, true);
            byte[] buffer = new byte[131072]; int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0) { await limiter.WaitAsync(read, ct); await output.WriteAsync(buffer.AsMemory(0, read), ct); long total = Interlocked.Add(ref downloaded, read); progress.Report(new(total, probe.Length, speed.Sample(total))); }
            await output.FlushAsync(ct);
            if (output.Length != expected) throw new EndOfStreamException("A download segment ended early.");
        });
        string temp = item.FilePath + ".assembling"; await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true)) { foreach (var r in ranges) { await using var input = new FileStream(Part(r.Index), FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true); await input.CopyToAsync(output, token); } }
        if (new FileInfo(temp).Length != probe.Length) throw new InvalidDataException("Merged file size does not match the server.");
        File.Move(temp, item.FilePath, true); Directory.Delete(partsDir, true); progress.Report(new(probe.Length, probe.Length, 0));
        string Part(int index) => Path.Combine(partsDir, $"part-{index:D2}.bin");
    }

    private async Task DownloadSingle(DownloadItem item, DownloadProbe probe, long speedLimit, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        long existing = File.Exists(item.FilePath) ? new FileInfo(item.FilePath).Length : 0;
        if (probe.Length > 0 && existing == probe.Length) { progress.Report(new(existing, probe.Length, 0)); return; }
        using var request = new HttpRequestMessage(HttpMethod.Get, item.Url); if (existing > 0 && probe.SupportsRanges) { request.Headers.Range = new(existing, null); SetIfRange(request, probe); }
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
        bool append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.From == existing; if (!append) existing = 0;
        long responseLength = response.Content.Headers.ContentLength ?? 0;
        if (probe.Length > 0 && responseLength > 0 && existing + responseLength != probe.Length) throw new InvalidDataException("Server reported inconsistent file sizes.");
        long total = probe.Length > 0 ? probe.Length : existing + (response.Content.Headers.ContentLength ?? 0); var meter = new SpeedMeter(existing); var limiter = new RateLimiter(speedLimit);
        await using var input = await response.Content.ReadAsStreamAsync(token); await using var output = new FileStream(item.FilePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 131072, true);
        byte[] buffer = new byte[131072]; int read; long downloaded = existing;
        while ((read = await input.ReadAsync(buffer, token)) > 0) { await limiter.WaitAsync(read, token); await output.WriteAsync(buffer.AsMemory(0, read), token); downloaded += read; progress.Report(new(downloaded, total, meter.Sample(downloaded))); }
        await output.FlushAsync(token);
        if (total > 0 && (downloaded != total || output.Length != total)) throw new EndOfStreamException("Download ended before the expected file size.");
    }
    private static void SetIfRange(HttpRequestMessage request, DownloadProbe probe) { if (!string.IsNullOrWhiteSpace(probe.ETag) && EntityTagHeaderValue.TryParse(probe.ETag, out var tag)) request.Headers.IfRange = new(tag); else if (probe.LastModified.HasValue) request.Headers.IfRange = new(probe.LastModified.Value); }
    private sealed class SpeedMeter(long initial) { private readonly Stopwatch watch = Stopwatch.StartNew(); private long last = initial; public double Sample(long current) { if (watch.ElapsedMilliseconds < 250) return 0; double value = (current - last) / watch.Elapsed.TotalSeconds; last = current; watch.Restart(); return value; } }
    private sealed class RateLimiter(long limit) { private readonly Stopwatch watch = Stopwatch.StartNew(); private long bytes; private readonly SemaphoreSlim gate = new(1, 1); public async Task WaitAsync(int count, CancellationToken token) { if (limit <= 0) return; await gate.WaitAsync(token); try { bytes += count; double wait = bytes / (double)limit - watch.Elapsed.TotalSeconds; if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), token); if (watch.Elapsed.TotalSeconds > 2) { bytes = 0; watch.Restart(); } } finally { gate.Release(); } } }
    public void Dispose() => Client.Dispose();
}
