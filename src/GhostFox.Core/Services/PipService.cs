using System.Text.Json;

namespace GhostFox.Core.Services;

/// <summary>Информация о видео для PiP, как её возвращает ExtractVideoScript.</summary>
public sealed record PipVideoInfo(string? Source, double CurrentTime, bool Paused, string? PageTitle);

/// <summary>
/// Картинка-в-картинке (Модуль 3).
/// Честное ограничение Gecko 60: requestPictureInPicture там нет, а стриминги (YouTube)
/// играют через MSE/blob — у таких видео нет прямого src, и PiP для них невозможен в принципе.
/// Для видео с прямым файлом строим мини-страницу и играем её во втором движке-оверлее.
/// </summary>
public static class PipService
{
    public const string ExtractVideoScript = """
        (() => {
          const videos = [...document.querySelectorAll('video')]
            .map(v => ({ v, area: (v.clientWidth || v.videoWidth || 0) * (v.clientHeight || v.videoHeight || 0) }))
            .sort((a, b) => b.area - a.area);
          if (!videos.length) return JSON.stringify({ error: 'no-video' });
          const v = videos[0].v;
          const src = v.currentSrc || v.src || (v.querySelector('source') || {}).src || '';
          if (!src || src.startsWith('blob:')) return JSON.stringify({ error: 'streaming-mse' });
          return JSON.stringify({ source: src, currentTime: v.currentTime || 0, paused: v.paused, pageTitle: document.title });
        })();
        """;

    public static PipVideoInfo? ParseInfo(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) return null;
            return new PipVideoInfo(
                root.TryGetProperty("source", out var s) ? s.GetString() : null,
                root.TryGetProperty("currentTime", out var t) ? t.GetDouble() : 0,
                root.TryGetProperty("paused", out var p) && p.GetBoolean(),
                root.TryGetProperty("pageTitle", out var title) ? title.GetString() : null);
        }
        catch { return null; }
    }

    public static string? ErrorOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "no-video";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        catch { return "parse"; }
    }

    public static string BuildPipHtml(PipVideoInfo info)
    {
        var src = System.Net.WebUtility.HtmlEncode(info.Source ?? "");
        var start = info.CurrentTime.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var autoplay = info.Paused ? "" : "autoplay";
        return $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><title>GhostFox PiP</title>
            <style>html,body{margin:0;background:#000;height:100%}video{width:100%;height:100%}</style></head>
            <body><video src="{{src}}" controls {{autoplay}}></video>
            <script>const v=document.querySelector('video'); v.addEventListener('loadedmetadata',()=>{ try{ v.currentTime = {{start}}; }catch(e){} });</script>
            </body></html>
            """;
    }
}
