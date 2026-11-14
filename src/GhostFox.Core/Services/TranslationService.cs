using System.Text.Json;
using System.Text.RegularExpressions;

namespace GhostFox.Core.Services;

/// <summary>
/// Встроенный переводчик (Модуль 3). Бэкенд — MyMemory (бесплатный, без ключа,
/// лимит ~1000 слов/день на IP; для продакшна подменяется BaseUrl на свой LibreTranslate).
/// Язык определяется эвристикой по письменности, перевод режется на чанки по предложениям.
/// </summary>
public sealed class TranslationService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _cache = new();

    public TranslationService(HttpClient? http = null) =>
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public string BaseUrl { get; set; } = "https://api.mymemory.translated.net";

    public static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "en";
        int cyrillic = 0, hangul = 0, kana = 0, han = 0, arabic = 0, latin = 0;
        foreach (var ch in text.Take(400))
        {
            if (ch is >= '\u0400' and <= '\u04FF') cyrillic++;
            else if (ch is >= '\uAC00' and <= '\uD7AF') hangul++;
            else if (ch is >= '\u3040' and <= '\u30FF') kana++;
            else if (ch is >= '\u4E00' and <= '\u9FFF') han++;
            else if (ch is >= '\u0600' and <= '\u06FF') arabic++;
            else if (char.IsLetter(ch)) latin++;
        }
        var best = new[] { ("ru", cyrillic), ("ko", hangul), ("ja", kana), ("zh-CN", han), ("ar", arabic), ("en", latin) }
            .MaxBy(x => x.Item2);
        return best.Item2 == 0 ? "en" : best.Item1;
    }

    /// <summary>Разбивка на чанки <=450 символов по границам предложений (лимит q у MyMemory).</summary>
    public static List<string> Chunk(string text, int maxLength = 450)
    {
        var chunks = new List<string>();
        var sentences = Regex.Split(text, @"(?<=[.!?…])\s+");
        var current = "";
        foreach (var s in sentences)
        {
            if (s.Length > maxLength)
            {
                if (current.Length > 0) { chunks.Add(current); current = ""; }
                for (var i = 0; i < s.Length; i += maxLength) chunks.Add(s.Substring(i, Math.Min(maxLength, s.Length - i)));
                continue;
            }
            if (current.Length + s.Length + 1 > maxLength) { chunks.Add(current); current = s; }
            else current = current.Length == 0 ? s : current + " " + s;
        }
        if (current.Length > 0) chunks.Add(current);
        return chunks;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var source = sourceLanguage ?? DetectLanguage(text);
        if (source == targetLanguage) return text;
        var cacheKey = source + "|" + targetLanguage + "|" + text;
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var parts = new List<string>();
        foreach (var chunk in Chunk(text))
            parts.Add(await TranslateChunkAsync(chunk, source, targetLanguage, ct).ConfigureAwait(false));
        var result = string.Join(" ", parts);
        _cache[cacheKey] = result;
        return result;
    }

    private async Task<string> TranslateChunkAsync(string chunk, string source, string target, CancellationToken ct)
    {
        var url = $"{BaseUrl}/get?q={Uri.EscapeDataString(chunk)}&langpair={source}|{target}";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString() ?? chunk;
    }
}

/// <summary>Скрипты перевода страницы: вытащить текстовые узлы и подставить перевод обратно.</summary>
public static class PageTranslationScripts
{
    public const string ExtractTextScript = """
        (() => {
          const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode: (n) => {
              if (!n.parentElement) return NodeFilter.FILTER_REJECT;
              const tag = n.parentElement.tagName;
              if (['SCRIPT','STYLE','CODE','PRE','NOSCRIPT','TEXTAREA'].includes(tag)) return NodeFilter.FILTER_REJECT;
              return n.textContent.trim().length >= 3 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
            }});
          const texts = new Set();
          let node;
          while (node = walker.nextNode()) texts.add(node.textContent.trim());
          return JSON.stringify([...texts].slice(0, 120));
        })();
        """;

    /// <param name="translationsJson">JSON-объект {"оригинал":"перевод", ...}.</param>
    public static string BuildApplyScript(string translationsJson)
    {
        var literal = JsonSerializer.Serialize(translationsJson);
        return $$"""
            (() => {
              const map = JSON.parse({{literal}});
              const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null);
              let node, count = 0;
              while (node = walker.nextNode()) {
                const key = node.textContent.trim();
                if (map[key]) { node.textContent = node.textContent.replace(key, map[key]); count++; }
              }
              return 'replaced:' + count;
            })();
            """;
    }
}
