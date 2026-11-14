namespace GhostFox.Core.Services;

/// <summary>
/// Модуль 2: блокировщик уровня EasyList.
///
/// Поддерживаемый синтаксис (честный поднабор Adblock Plus + hosts):
///   ||example.com^                 — домен и все поддомены, любой путь
///   ||example.com/ads/*            — домен + префикс пути
///   ||example.com^$script          — опции после $ разбираются, third-party не ослабляет блокировку
///   @@||example.com^               — исключение (белый список), проверяется первым
///   ##.ad-banner                   — косметический фильтр (глобальный)
///   example.com##.popup            — косметический фильтр для домена
///   0.0.0.0 tracker.com / tracker.com — hosts-формат
/// Строки вида /regex/ и чистые path-правила (/banner/*) пропускаются — это задокументированное ограничение.
/// </summary>
public sealed class AdBlockEngine
{
    private sealed record NetworkRule(string Domain, string? PathPrefix, bool IsException);
    private sealed record CosmeticRule(string? Domain, string Selector);

    private List<NetworkRule> _network = new();
    private List<CosmeticRule> _cosmetic = new();
    private readonly object _gate = new();

    public int NetworkRuleCount { get { lock (_gate) return _network.Count; } }
    public int CosmeticRuleCount { get { lock (_gate) return _cosmetic.Count; } }

    public AdBlockEngine(bool seedBuiltInTrackers = true)
    {
        if (seedBuiltInTrackers)
            LoadRules(string.Join('\n', PrivacyBlocklist.DefaultDomains.Select(d => "||" + d + "^")));
    }

    /// <summary>Разобрать текст списка и ДОБАВИТЬ правила. Возвращает, сколько правил распознано.</summary>
    public int LoadRules(string listText)
    {
        var network = new List<NetworkRule>();
        var cosmetic = new List<CosmeticRule>();

        foreach (var raw in listText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '!' || line[0] == '[') continue;
            if (line[0] == '#' && !line.StartsWith("##", StringComparison.Ordinal)) continue; // комментарий, но не косметическое правило ##

            // --- косметика ---
            var cosmeticMarker = line.IndexOf("##", StringComparison.Ordinal);
            if (cosmeticMarker >= 0 && !line.Contains("#@#"))
            {
                var domainPart = line[..cosmeticMarker].Trim();
                var selector = line[(cosmeticMarker + 2)..].Trim();
                if (selector.Length == 0) continue;
                if (domainPart.Length == 0 || domainPart == "*")
                    cosmetic.Add(new CosmeticRule(null, selector));
                else
                    foreach (var d in domainPart.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        cosmetic.Add(new CosmeticRule(d.ToLowerInvariant(), selector));
                continue;
            }
            if (line.Contains("#@#") || line.Contains("#?#")) continue; // исключения косметики — Модуль 3

            var isException = false;
            if (line.StartsWith("@@", StringComparison.Ordinal)) { isException = true; line = line[2..]; }

            // --- сетевые правила ||domain... ---
            if (line.StartsWith("||", StringComparison.Ordinal))
            {
                var rest = line[2..];
                var cut = rest.IndexOfAny(new[] { '^', '$', '|' });
                var token = cut >= 0 ? rest[..cut] : rest;
                token = token.TrimEnd('*');
                if (token.Length == 0) continue;
                string domain; string? path = null;
                var slash = token.IndexOf('/');
                if (slash >= 0) { domain = token[..slash]; path = token[slash..]; }
                else domain = token;
                domain = domain.TrimStart('*', '.').ToLowerInvariant();
                if (domain.Length == 0 || domain.Contains(' ')) continue;
                network.Add(new NetworkRule(domain, path, isException));
                continue;
            }

            // --- hosts-формат ---
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string? host = null;
            if (parts.Length >= 2 && (parts[0] == "0.0.0.0" || parts[0] == "127.0.0.1")) host = parts[1];
            else if (parts.Length == 1 && parts[0].Contains('.') && !parts[0].Contains('/') && !parts[0].StartsWith("http"))
                host = parts[0];
            if (host != null)
                network.Add(new NetworkRule(host.ToLowerInvariant(), null, isException));
        }

        lock (_gate) { _network.AddRange(network); _cosmetic.AddRange(cosmetic); }
        return network.Count + cosmetic.Count;
    }

    public void Reset()
    {
        lock (_gate) { _network = new(); _cosmetic = new(); }
    }

    public bool ShouldBlock(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        if (host.Length == 0) return false;
        var path = uri.AbsolutePath;

        List<NetworkRule> snapshot;
        lock (_gate) snapshot = _network;

        var blocked = false;
        foreach (var rule in snapshot)
        {
            if (!HostMatches(host, rule.Domain)) continue;
            if (rule.PathPrefix != null && !path.StartsWith(rule.PathPrefix.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)) continue;
            if (rule.IsException) return false; // белый список побеждает немедленно
            blocked = true;
        }
        return blocked;
    }

    /// <summary>CSS для косметических фильтров страницы: селекторы -> display:none.</summary>
    public string BuildCosmeticCss(string pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri)) return string.Empty;
        var host = uri.Host.ToLowerInvariant();
        List<CosmeticRule> snapshot;
        lock (_gate) snapshot = _cosmetic;

        var selectors = snapshot
            .Where(r => r.Domain == null || HostMatches(host, r.Domain))
            .Select(r => r.Selector)
            .Where(s => s.Length < 300) // защита от мусорных правил
            .Distinct()
            .Take(2000)
            .ToList();
        if (selectors.Count == 0) return string.Empty;
        return string.Join(",\n", selectors) + " { display: none !important; }";
    }

    internal static bool HostMatches(string host, string ruleDomain) =>
        host == ruleDomain || host.EndsWith("." + ruleDomain, StringComparison.Ordinal);
}
