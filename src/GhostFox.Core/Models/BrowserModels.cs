using GhostFox.Core.Services;

namespace GhostFox.Core.Models;

/// <summary>Пользовательская поисковая система (Модуль 2: можно добавлять свои).</summary>
public sealed class SearchEngine
{
    public required string Name { get; set; }
    /// <summary>Шаблон с плейсхолдером {query}, например "https://duckduckgo.com/?q={query}".</summary>
    public required string UrlTemplate { get; set; }
    public bool IsDefault { get; set; }

    public string BuildUrl(string query) => UrlTemplate.Replace("{query}", Uri.EscapeDataString(query));

    public static List<SearchEngine> BuiltIn() => new()
    {
        new() { Name = "DuckDuckGo", UrlTemplate = "https://duckduckgo.com/?q={query}", IsDefault = true },
        new() { Name = "Startpage",  UrlTemplate = "https://www.startpage.com/sp/search?query={query}" },
        new() { Name = "Brave",      UrlTemplate = "https://search.brave.com/search?q={query}" },
        new() { Name = "Google",     UrlTemplate = "https://www.google.com/search?q={query}" },
    };
}

/// <summary>Все настройки браузера. Сериализуется в JSON в AppData (см. SettingsService).</summary>
public sealed class BrowserSettings
{
    public string HomePage { get; set; } = "https://duckduckgo.com/";
    public string DefaultSearchEngineName { get; set; } = "DuckDuckGo";
    public List<SearchEngine> SearchEngines { get; set; } = SearchEngine.BuiltIn();

    // --- Приватность (Модуль 2) ---
    public bool BlockTrackersAndAds { get; set; } = true;
    public bool EnableJavaScript { get; set; } = true;
    public bool AcceptCookies { get; set; } = true;
    public bool BlockThirdPartyCookies { get; set; } = true;
    /// <summary>WebRTC = частая утечка реального IP мимо VPN.</summary>
    public bool DisableWebRtc { get; set; } = true;
    /// <summary>Шум для Canvas/WebGL fingerprint (применяется инжектом на уровне движка, где доступно).</summary>
    public bool ResistFingerprinting { get; set; } = true;
    public bool Incognito { get; set; }
    public bool ClearOnExit { get; set; } = true;
    public string UserAgent { get; set; } = UserAgents.FirefoxWindows;

    // --- Кастомизация ---
    public bool DarkTheme { get; set; } = true;
    /// <summary>Имя темы из ThemeCatalog: Dark / Light / Midnight (Модуль 2).</summary>
    public string ThemeName { get; set; } = "Dark";
    public string AccentColorHex { get; set; } = "#7C4DFF";
    public bool VerticalTabs { get; set; } = true;
    /// <summary>Пользовательский CSS, инжектится в каждую страницу (Модуль 2).</summary>
    public string CustomCss { get; set; } = string.Empty;
    /// <summary>Per-site CSS правила (Модуль 2).</summary>
    public List<SiteCssRule> SiteCssRules { get; set; } = new();
    /// <summary>Жест (например "DR") -> действие (например "closetab") (Модуль 2).</summary>
    public Dictionary<string, string> GestureMap { get; set; } = Services.GestureRecognizer.DefaultMap();
    /// <summary>Подписки фильтров блокировщика (Модуль 2).</summary>
    public List<string> FilterSubscriptions { get; set; } = new(Services.FilterSubscriptionService.DefaultSubscriptions);
    /// <summary>Косметические фильтры (##селекторы из списков) в дополнение к сетевым.</summary>
    public bool BlockCosmeticFilters { get; set; } = true;

    public SearchEngine GetDefaultSearchEngine() =>
        SearchEngines.FirstOrDefault(e => e.Name == DefaultSearchEngineName)
        ?? SearchEngines.FirstOrDefault()
        ?? new SearchEngine { Name = "DuckDuckGo", UrlTemplate = "https://duckduckgo.com/?q={query}" };
}

public static class UserAgents
{
    public const string FirefoxWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:60.0) Gecko/20100101 Firefox/60.0";
    public const string FirefoxAndroid = "Mozilla/5.0 (Android 13; Mobile; rv:120.0) Gecko/120.0 Firefox/120.0";
    public const string ChromeWindows  = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";
}

/// <summary>Одна вкладка. Нативный IEngine живёт в UI-слое и привязывается по Id.</summary>
public sealed class BrowserTab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New Tab";
    public string Url { get; set; } = "about:blank";
    public bool IsLoading { get; set; }
    public bool IsPinned { get; set; }
}

public sealed class HistoryEntry
{
    public required string Url { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset VisitedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class Bookmark
{
    public required string Url { get; set; }
    public required string Title { get; set; }
    public string Folder { get; set; } = "Bookmarks";
}

/// <summary>Снимок сессии для восстановления после вылета (Модуль 3).</summary>
public sealed class SessionState
{
    public List<BrowserTab> Tabs { get; set; } = new();
    public Guid ActiveTabId { get; set; }
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;
}

public enum DownloadStatus { Queued, Downloading, Paused, Completed, Failed, Canceled }

public sealed class DownloadItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Url { get; set; }
    public required string FilePath { get; set; }
    public long BytesReceived { get; set; }
    public long? TotalBytes { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public string? Error { get; set; }
    public double Progress => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : 0;
}
