namespace GhostFox.Core.Services;

/// <summary>Тема оформления (Модуль 2). Акцент можно переопределить вручную.</summary>
public sealed record ThemePalette(string Name, string Background, string Surface, string SurfaceAlt,
                                  string Text, string TextDim, string Accent)
{
    public ThemePalette WithAccent(string accentHex) =>
        string.IsNullOrWhiteSpace(accentHex) ? this : this with { Accent = accentHex };
}

public static class ThemeCatalog
{
    public static readonly ThemePalette Dark =
        new("Dark", "#121216", "#1D1D24", "#26262F", "#F2F2F5", "#9A9AA5", "#7C4DFF");
    public static readonly ThemePalette Light =
        new("Light", "#F4F4F8", "#FFFFFF", "#E6E6EE", "#17171C", "#5B5B66", "#5E35B1");
    public static readonly ThemePalette Midnight =
        new("Midnight", "#05070D", "#0B101C", "#131A2A", "#DCE6F2", "#7E8DA3", "#00BFA5");

    public static IReadOnlyList<ThemePalette> All => new[] { Dark, Light, Midnight };

    public static ThemePalette Get(string? name, string? accentOverride = null) =>
        (All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? Dark)
        .WithAccent(accentOverride ?? string.Empty);
}

/// <summary>Пользовательское правило CSS для сайтов: "*://*.example.com/*" в простом wildcard-виде.</summary>
public sealed class SiteCssRule
{
    /// <summary>Шаблон хоста с '*' (например "*.wikipedia.org" или "*").</summary>
    public required string HostPattern { get; set; }
    public required string Css { get; set; }
    public bool Enabled { get; set; } = true;
}

public static class UserCssService
{
    /// <summary>Собирает CSS для страницы: глобальный + все подошедшие per-site правила.</summary>
    public static string BuildCssFor(string pageUrl, string globalCss, IReadOnlyList<SiteCssRule> rules)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(globalCss)) parts.Add(globalCss);
        if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            foreach (var rule in rules)
            {
                if (rule.Enabled && HostMatches(host, rule.HostPattern))
                    parts.Add(rule.Css);
            }
        }
        return string.Join("\n", parts);
    }

    internal static bool HostMatches(string host, string pattern)
    {
        pattern = pattern.Trim().ToLowerInvariant();
        host = host.ToLowerInvariant();
        if (pattern is "*" or "*.*") return true;
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = pattern[1..]; // ".example.com"
            return host == pattern[2..] || host.EndsWith(suffix, StringComparison.Ordinal);
        }
        if (pattern.Contains('*'))
        {
            var re = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(host, re);
        }
        return host == pattern;
    }
}

/// <summary>
/// Anti-fingerprint (Модуль 2): лёгкий шум Canvas. Переопределяет toDataURL/getImageData,
/// добавляя детерминированный псевдо-шум — отпечаток меняется между сайтами, но стабилен в рамках страницы.
/// Полноценная защита уровня Tor Browser недостижима инжектом; prefs resistFingerprinting — основной слой.
/// </summary>
public static class FingerprintNoise
{
    public const string NoiseScript = """
        (() => {
          if (window.__ghostfoxNoise) return 'already';
          window.__ghostfoxNoise = true;
          let seed = 22222;
          const rand = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
          const origToDataURL = HTMLCanvasElement.prototype.toDataURL;
          HTMLCanvasElement.prototype.toDataURL = function (...args) {
            try {
              const ctx = this.getContext('2d');
              if (ctx && this.width > 0 && this.height > 0) {
                const img = ctx.getImageData(0, 0, Math.min(this.width, 8), Math.min(this.height, 8));
                for (let i = 0; i < img.data.length; i += 4) img.data[i] = img.data[i] ^ (rand() * 2 | 0);
                ctx.putImageData(img, 0, 0);
              }
            } catch (e) {}
            return origToDataURL.apply(this, args);
          };
          return 'noise-on';
        })();
        """;
}
