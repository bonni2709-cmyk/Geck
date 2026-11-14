using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GhostFox.Core.Models;

namespace GhostFox.Core.Services;

/// <summary>
/// Импорт/экспорт данных (Модуль 2):
///  - экспорт всего профиля (закладки+история+настройки) в JSON;
///  - импорт закладок из Netscape Bookmark File (Firefox/Chrome/Edge экспортируют именно его)
///    и экспорт закладок обратно в этот же формат.
/// </summary>
public sealed class DataTransferService
{
    private static readonly Regex BookmarkAnchor = new(
        "<A\\s+[^>]*HREF=\"([^\"]+)\"[^>]*>([^<]*)</A>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BookmarkFolder = new(
        "<H3[^>]*>([^<]*)</H3>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record ProfileExport(List<Bookmark> Bookmarks, List<HistoryEntry> History, BrowserSettings Settings);

    public string ExportProfileJson(IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<HistoryEntry> history, BrowserSettings settings) =>
        JsonSerializer.Serialize(new ProfileExport(bookmarks.ToList(), history.ToList(), settings),
            new JsonSerializerOptions { WriteIndented = true });

    public List<Bookmark> ParseBookmarksHtml(string html)
    {
        var result = new List<Bookmark>();
        if (string.IsNullOrWhiteSpace(html)) return result;

        // Папки в Netscape-формате идут H3 перед вложенным DL — присваиваем последнюю встреченную.
        var folder = "Imported";
        var position = 0;
        while (position < html.Length)
        {
            var folderMatch = BookmarkFolder.Match(html, position);
            var anchorMatch = BookmarkAnchor.Match(html, position);
            if (!anchorMatch.Success) break;
            if (folderMatch.Success && folderMatch.Index < anchorMatch.Index)
            {
                folder = System.Net.WebUtility.HtmlDecode(folderMatch.Groups[1].Value.Trim());
                position = folderMatch.Index + folderMatch.Length;
                continue;
            }
            var url = System.Net.WebUtility.HtmlDecode(anchorMatch.Groups[1].Value.Trim());
            var title = System.Net.WebUtility.HtmlDecode(anchorMatch.Groups[2].Value.Trim());
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                result.Add(new Bookmark { Url = url, Title = title.Length > 0 ? title : url, Folder = folder });
            position = anchorMatch.Index + anchorMatch.Length;
        }
        return result;
    }

    public string ExportBookmarksHtml(IReadOnlyList<Bookmark> bookmarks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<!-- Exported by GhostFox -->");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");
        foreach (var b in bookmarks)
        {
            sb.Append("    <DT><A HREF=\"")
              .Append(System.Net.WebUtility.HtmlEncode(b.Url)).Append("\">")
              .Append(System.Net.WebUtility.HtmlEncode(b.Title)).AppendLine("</A>");
        }
        sb.AppendLine("</DL><p>");
        return sb.ToString();
    }
}
