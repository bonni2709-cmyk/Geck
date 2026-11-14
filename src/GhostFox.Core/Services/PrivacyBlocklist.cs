namespace GhostFox.Core.Services;

/// <summary>
/// Модуль 2 (база уже в Модуле 1): решение "блокировать ли запрос".
/// Сопоставление по домену и всем его поддоменам. Список — стартовый набор
/// крупнейших трекеров/рекламных сетей; в Модуле 2 сюда подключаются
/// подписки формата EasyList/hosts.
/// </summary>
public sealed class PrivacyBlocklist
{
    private readonly HashSet<string> _domains;

    public PrivacyBlocklist(IEnumerable<string>? extraDomains = null)
    {
        _domains = new HashSet<string>(DefaultDomains, StringComparer.OrdinalIgnoreCase);
        if (extraDomains != null)
            foreach (var d in extraDomains) _domains.Add(d.Trim().ToLowerInvariant());
    }

    public int Count => _domains.Count;

    public bool ShouldBlock(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host;
        if (string.IsNullOrEmpty(host)) return false;

        // точное совпадение и любой родительский домен: ads.tracker.example.com -> tracker.example.com -> example.com
        var labels = host.Split('.');
        for (var i = 0; i < labels.Length - 1; i++)
        {
            var candidate = string.Join('.', labels, i, labels.Length - i);
            if (_domains.Contains(candidate)) return true;
        }
        return false;
    }

    public static readonly string[] DefaultDomains =
    {
        "doubleclick.net", "googlesyndication.com", "google-analytics.com", "googletagmanager.com",
        "googleadservices.com", "adservice.google.com", "connect.facebook.net", "graph.facebook.com",
        "analytics.twitter.com", "ads.twitter.com", "mc.yandex.ru", "an.yandex.ru", "yandex.ru/clck",
        "hotjar.com", "static.hotjar.com", "mixpanel.com", "api.mixpanel.com", "segment.io", "segment.com",
        "amplitude.com", "api.amplitude.com", "fullstory.com", "mouseflow.com", "clarity.ms",
        "scorecardresearch.com", "quantserve.com", "adnxs.com", "adsrvr.org", "rubiconproject.com",
        "pubmatic.com", "openx.net", "criteo.com", "criteo.net", "outbrain.com", "taboola.com",
        "amazon-adsystem.com", "advertising.com", "adform.net", "smartadserver.com", "zedo.com",
        "tracking.com", "clickfuse.com", "mathtag.com", "bluekai.com", "demdex.net", "krxd.net",
        "exelator.com", "turn.com", "bidswitch.net", "casalemedia.com", "contextweb.com", "lijit.com",
        "sharethrough.com", "teads.tv", "imrworldwide.com", "newrelic.com", "nr-data.net", "bugsnag.com",
    };
}
