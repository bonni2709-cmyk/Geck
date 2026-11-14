using GhostFox.Core.Models;

namespace GhostFox.Core.Services;

/// <summary>
/// Превращает текст адресной строки в URL:
/// "example.com" -> https://example.com/, "кот видео" -> поиск через выбранный поисковик.
/// </summary>
public static class UrlNormalizer
{
    private static readonly string[] DownloadExtensions =
        { ".zip", ".rar", ".7z", ".exe", ".msi", ".pdf", ".mp4", ".mp3", ".iso", ".dmg", ".apk", ".tar", ".gz" };

    public static string Normalize(string input, SearchEngine searchEngine)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0) return "about:blank";
        if (text.Equals("about:blank", StringComparison.OrdinalIgnoreCase)) return text;
        if (text.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return text;

        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == "file"))
            return absolute.ToString();

        var looksLikeHost = !text.Contains(' ') &&
            (text.Contains('.') || text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase));
        if (looksLikeHost)
        {
            if (Uri.TryCreate("https://" + text, UriKind.Absolute, out var hostUri))
                return hostUri.ToString();
        }

        return searchEngine.BuildUrl(text);
    }

    /// <summary>Эвристика Модуля 1: навигацию на файл отдаём загрузчику, а не движку.</summary>
    public static bool LooksLikeDownload(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var path = uri.AbsolutePath;
        return DownloadExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
