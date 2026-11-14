using GhostFox.Core.Models;

namespace GhostFox.Core.Services;

/// <summary>
/// Модуль 1/3: загрузчик с докачкой (HTTP Range) поверх HttpClient.
/// Файл пишется в "*.ghostfox.part", по завершении переименовывается.
/// </summary>
public sealed class DownloadService
{
    private readonly HttpClient _http = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _running = new();

    public event EventHandler<DownloadItem>? Changed;

    private readonly List<DownloadItem> _items = new();
    /// <summary>Все загрузки текущей сессии приложения (Модуль 3: экран загрузок).</summary>
    public IReadOnlyList<DownloadItem> Items => _items;

    public async Task<DownloadItem> DownloadAsync(string url, string destinationDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var fileName = GetFileName(url);
        var finalPath = Path.Combine(destinationDirectory, fileName);
        var item = new DownloadItem { Url = url, FilePath = finalPath };
        _items.Insert(0, item);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _running[item.Id] = linked;

        var partPath = finalPath + ".ghostfox.part";
        try
        {
            item.Status = DownloadStatus.Downloading; Raise(item);
            var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existing > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var resumed = existing > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            item.BytesReceived = resumed ? existing : 0;
            item.TotalBytes = response.Content.Headers.ContentLength is { } len
                ? len + (resumed ? existing : 0) : null;

            await using var input = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            await using var output = new FileStream(partPath, resumed ? FileMode.Append : FileMode.Create,
                FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, linked.Token).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), linked.Token).ConfigureAwait(false);
                item.BytesReceived += read;
                Raise(item);
            }

            File.Move(partPath, finalPath, overwrite: true);
            item.Status = DownloadStatus.Completed;
        }
        catch (OperationCanceledException) { item.Status = DownloadStatus.Paused; }
        catch (Exception ex) { item.Status = DownloadStatus.Failed; item.Error = ex.Message; }
        finally { _running.Remove(item.Id); Raise(item); }
        return item;
    }

    public void Pause(Guid id) { if (_running.TryGetValue(id, out var cts)) cts.Cancel(); }

    private void Raise(DownloadItem item) => Changed?.Invoke(this, item);

    internal static string GetFileName(string url)
    {
        var name = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath)) : string.Empty;
        if (string.IsNullOrWhiteSpace(name)) name = "download.bin";
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name;
    }
}
