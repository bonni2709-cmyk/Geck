namespace GhostFox.Core.Engines;

/// <summary>Причина, по которой движок отклонил/изменил навигацию.</summary>
public enum NavigationDecision
{
    Allow,
    BlockedByPrivacyFilter,
    HandedToDownloader
}

public sealed class NavigatingEventArgs : EventArgs
{
    public required string Url { get; init; }
    public bool IsMainFrame { get; init; } = true;
    /// <summary>Выставляется обработчиком (например, блокировщиком), чтобы отменить переход.</summary>
    public bool Cancel { get; set; }
    public NavigationDecision Decision { get; set; } = NavigationDecision.Allow;
}

public sealed class NavigatedEventArgs : EventArgs
{
    public required string Url { get; init; }
    public bool Success { get; init; } = true;
}

public sealed class ProgressEventArgs : EventArgs
{
    /// <summary>0..1, либо -1 если движок не сообщает прогресс.</summary>
    public required double Value { get; init; }
}

public sealed class DownloadRequestedEventArgs : EventArgs
{
    public required string Url { get; init; }
    public string? SuggestedFileName { get; init; }
}

/// <summary>
/// ЕДИНЫЙ контракт браузерного движка для всего GhostFox.
///
/// Реализации:
///   * Windows : GhostFox.Platforms.Windows.GeckoFxEngine   (GeckoFX + XULRunner, Firefox 60 ESR)
///   * Android : GhostFox.Platforms.Android.GeckoViewEngine (GeckoView, Модуль 3 — см. docs/ANDROID_GECKOVIEW.md)
///   * Любая ОС: GhostFox.Core.Engines.PlaceholderEngine    (заглушка, если нативный рантайм не найден)
///
/// UI и вся бизнес-логика зависят ТОЛЬКО от этого интерфейса, поэтому замена
/// GeckoFX -> GeckoView не требует переписывания логики.
/// </summary>
public interface IEngine : IDisposable
{
    /// <summary>Человекочитаемое имя реализации ("GeckoFX (Firefox 60 ESR)", "GeckoView", ...).</summary>
    string EngineName { get; }

    /// <summary>true, если нативный рантайм реально поднялся (а не placeholder).</summary>
    bool IsNativeEngineAvailable { get; }

    string? CurrentUrl { get; }
    string? Title { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    bool IsLoading { get; }

    event EventHandler<NavigatingEventArgs>? Navigating;
    event EventHandler<NavigatedEventArgs>? Navigated;
    event EventHandler<ProgressEventArgs>? LoadProgressChanged;
    event EventHandler? TitleChanged;
    event EventHandler? HistoryStateChanged;
    event EventHandler<DownloadRequestedEventArgs>? DownloadRequested;
    /// <summary>Сайт запросил новое окно/вкладку (target=_blank, window.open).</summary>
    event EventHandler<NavigatingEventArgs>? NewTabRequested;

    void Navigate(string url);
    void GoBack();
    void GoForward();
    void Reload();
    void Stop();

    /// <summary>Выполнить JS в контексте страницы (режим чтения, инжект CSS и т.д.).</summary>
    Task<string?> EvaluateScriptAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>Вставить пользовательский CSS в текущую страницу (Модуль 2).</summary>
    Task InjectUserCssAsync(string css, CancellationToken cancellationToken = default);

    /// <summary>Применить настройки приватности к живому профилю движка.</summary>
    void ApplyPrivacySettings(Models.BrowserSettings settings);
}
