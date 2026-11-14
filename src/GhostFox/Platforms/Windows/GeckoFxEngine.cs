using Gecko;
using GhostFox.Core.Engines;
using GhostFox.Core.Models;
using GhostFox.Core.Services;

namespace GhostFox.Platforms.Windows;

/// <summary>
/// IEngine поверх GeckoFX (GeckoWebBrowser, WinForms-контрол).
/// Хостинг в WinUI3 — через HWND (см. GeckoEngineHost), потому что у WinUI нет WindowsFormsHost.
/// </summary>
internal sealed class GeckoFxEngine : IEngine
{
    private readonly GeckoWebBrowser _browser;

    public GeckoFxEngine(BrowserSettings settings)
    {
        _browser = new GeckoWebBrowser { Dock = System.Windows.Forms.DockStyle.Fill };
        ApplyPrivacySettings(settings);
        WireEvents();
    }

    internal System.Windows.Forms.Control NativeControl => _browser;

    public string EngineName => "GeckoFX (Firefox 60 ESR)";
    public bool IsNativeEngineAvailable => true;
    public string? CurrentUrl => _browser.Url?.ToString();
    public string? Title => _browser.DocumentTitle;
    public bool CanGoBack => _browser.CanGoBack;
    public bool CanGoForward => _browser.CanGoForward;
    public bool IsLoading => _browser.IsBusy;

    public event EventHandler<NavigatingEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;
    public event EventHandler<ProgressEventArgs>? LoadProgressChanged;
    public event EventHandler? TitleChanged;
    public event EventHandler? HistoryStateChanged;
    public event EventHandler<DownloadRequestedEventArgs>? DownloadRequested;
    public event EventHandler<NavigatingEventArgs>? NewTabRequested;

    private void WireEvents()
    {
        _browser.Navigating += (_, e) =>
        {
            var args = new NavigatingEventArgs { Url = e.Uri?.ToString() ?? "about:blank" };
            Navigating?.Invoke(this, args);
            if (args.Cancel) e.Cancel = true;
        };
        _browser.Navigated += (_, e) =>
        {
            Navigated?.Invoke(this, new NavigatedEventArgs { Url = e.Uri?.ToString() ?? CurrentUrl ?? "about:blank" });
            HistoryStateChanged?.Invoke(this, EventArgs.Empty);
        };
        _browser.DocumentTitleChanged += (_, _) => TitleChanged?.Invoke(this, EventArgs.Empty);
        _browser.ProgressChanged += (_, e) =>
        {
            var value = e.MaximumProgress > 0 ? (double)e.CurrentProgress / e.MaximumProgress : -1;
            LoadProgressChanged?.Invoke(this, new ProgressEventArgs { Value = value });
        };
    }

    public void Navigate(string url) => _browser.Navigate(url);
    public void GoBack() => _browser.GoBack();
    public void GoForward() => _browser.GoForward();
    public void Reload() => _browser.Reload();
    public void Stop() => _browser.Stop();

    public Task<string?> EvaluateScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = new AutoJSContext(_browser.Window); // GeckoFX 60: AutoJSContext принимает GeckoWindow
            context.EvaluateScript(script, out string result);
            return Task.FromResult<string?>(result);
        }
        catch { return Task.FromResult<string?>(null); }
    }

    public Task InjectUserCssAsync(string css, CancellationToken cancellationToken = default) =>
        EvaluateScriptAsync(ReaderMode.BuildCssInjectionScript(css), cancellationToken);

    /// <summary>Маппинг настроек приватности в prefs Gecko (about:config).</summary>
    public void ApplyPrivacySettings(BrowserSettings s)
    {
        try
        {
            var prefs = GeckoPreferences.Default;
            prefs["javascript.enabled"] = s.EnableJavaScript;
            prefs["network.cookie.cookieBehavior"] = !s.AcceptCookies ? 2 : s.BlockThirdPartyCookies ? 1 : 0;
            prefs["media.peerconnection.enabled"] = !s.DisableWebRtc;
            prefs["privacy.resistFingerprinting"] = s.ResistFingerprinting;
            prefs["privacy.trackingprotection.enabled"] = s.BlockTrackersAndAds;
            prefs["general.useragent.override"] = s.UserAgent;
            prefs["browser.privatebrowsing.autostart"] = s.Incognito;
        }
        catch { /* prefs применяются best-effort: движок мог ещё не подняться */ }
    }

    public Task ClearPrivateDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // В GeckoFX 60 нет DeleteCookies на контроле — идём напрямую в nsICookieManager.
            var cookies = Xpcom.GetService<nsICookieManager>("@mozilla.org/cookiemanager;1");
            cookies = Xpcom.QueryInterface<nsICookieManager>(cookies);
            cookies.RemoveAll();
        }
        catch { /* движок мог ещё не подняться */ }
        return Task.CompletedTask;
    }

    public void Dispose() => _browser.Dispose();
}
