using GhostFox.Core.Engines;
using GhostFox.Core.Models;
using GhostFox.Core.Services;

namespace GhostFox;

/// <summary>
/// Оболочка браузера (Модуль 1): вертикальные вкладки, адресная строка,
/// прогресс, навигация, загрузки. Вся логика — через Core-сервисы и IEngine,
/// поэтому этот файл одинаков для Windows (GeckoFX) и Android (GeckoView).
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IBrowserEngineHost _engineHost;
    private readonly BrowserDataService _data;
    private readonly TabManager _tabs;
    private readonly AdBlockEngine _adblock;
    private readonly FilterSubscriptionService _filters;
    private readonly PasswordVault _vault;
    private readonly DownloadService _downloads;
    private readonly GestureDrawable _gestureDrawable = new();
    private readonly List<(double X, double Y)> _gesturePoints = new();

    private readonly Dictionary<Guid, IEngine> _engines = new();
    private BrowserSettings _settings = new();
    private int _blockedCount;
    private bool _initialized;

    public MainPage(IBrowserEngineHost engineHost, BrowserDataService data, TabManager tabs,
                    AdBlockEngine adblock, FilterSubscriptionService filters,
                    PasswordVault vault, DownloadService downloads)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        GesturePad.Drawable = _gestureDrawable;
        _engineHost = engineHost;
        _data = data;
        _tabs = tabs;
        _adblock = adblock;
        _filters = filters;
        _vault = vault;
        _downloads = downloads;
        _tabs.Changed += (_, _) => RefreshTabList();
        _downloads.Changed += (_, item) =>
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"⬇ {Path.GetFileName(item.FilePath)} — {item.Status} {item.Progress:P0}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        _settings = await _data.LoadSettingsAsync();
        ThemeApplier.Apply(_settings);

        // Модуль 2: фильтры блокировщика — из кэша сразу, обновление в фоне если кэш старше недели.
        _filters.LoadFromCache();
        var updatedAt = _filters.ReadUpdatedAt();
        if (updatedAt == null || DateTimeOffset.Now - updatedAt > TimeSpan.FromDays(7))
            _ = UpdateFiltersInBackgroundAsync();

        // Модуль 3: восстановление сессии после вылета.
        var session = await _data.LoadSessionAsync();
        if (session.Tabs.Count > 0 && !_settings.Incognito)
            _tabs.Restore(session);
        if (_tabs.Tabs.Count == 0)
            _tabs.OpenTab(_settings.HomePage);

        ActivateTab(_tabs.ActiveTab!, navigateIfNew: true);
    }

    // ---------- Вкладки ----------

    private IEngine EngineFor(BrowserTab tab)
    {
        if (_engines.TryGetValue(tab.Id, out var existing)) return existing;
        var engine = _engineHost.CreateAndAttach(EngineHost, _settings);
        WireEngine(engine, tab);
        _engines[tab.Id] = engine;
        EngineLabel.Text = $"engine: {engine.EngineName}";
        PlaceholderLabel.IsVisible = !engine.IsNativeEngineAvailable;
        return engine;
    }

    private void ActivateTab(BrowserTab tab, bool navigateIfNew = false)
    {
        _tabs.Activate(tab.Id);
        var engine = EngineFor(tab);
        ShowEngine(tab.Id);
        AddressBar.Text = tab.Url == "about:blank" ? string.Empty : tab.Url;
        UpdateNavButtons(engine);
        if (navigateIfNew && engine.CurrentUrl == null && tab.Url != "about:blank")
            engine.Navigate(tab.Url);
        _ = PersistSessionAsync();
    }

    /// <summary>Платформенный хост сам решает, как показать движок вкладки (Windows: показать нужный HWND, скрыть остальные).</summary>
    private void ShowEngine(Guid tabId)
    {
        if (_engineHost is IEngineSwitcher switcher)
            switcher.ShowEngine(_engines.TryGetValue(tabId, out var e) ? e : null);
    }

    private void RefreshTabList()
    {
        TabsView.ItemsSource = _tabs.Tabs
            .Select(t => new TabRow(t.Id, (t.IsLoading ? "⏳ " : "") + t.Title)).ToList();
    }

    private async Task PersistSessionAsync()
    {
        if (_settings.Incognito) return;
        try { await _data.SaveSessionAsync(_tabs.ToSession()); } catch { /* сессия — не критичный путь */ }
    }

    // ---------- События движка ----------

    private void WireEngine(IEngine engine, BrowserTab tab)
    {
        engine.Navigating += (_, e) =>
        {
            // Модуль 2: блокировщик трекеров/рекламы на уровне запроса главной страницы и подресурсов.
            if (_settings.BlockTrackersAndAds && _adblock.ShouldBlock(e.Url))
            {
                e.Cancel = true;
                e.Decision = NavigationDecision.BlockedByPrivacyFilter;
                MainThread.BeginInvokeOnMainThread(() =>
                    BlockedLabel.Text = $"🛡 blocked: {++_blockedCount}");
                return;
            }
            // Файлы отдаём загрузчику с докачкой, а не движку.
            if (e.IsMainFrame && UrlNormalizer.LooksLikeDownload(e.Url))
            {
                e.Cancel = true;
                e.Decision = NavigationDecision.HandedToDownloader;
                _ = StartDownloadAsync(e.Url);
            }
        };
        engine.NewTabRequested += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
        {
            var newTab = _tabs.OpenTab(e.Url);
            ActivateTab(newTab, navigateIfNew: true);
        });
        engine.DownloadRequested += (_, e) => _ = StartDownloadAsync(e.Url);
        engine.LoadProgressChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadProgress.IsVisible = e.Value >= 0 && e.Value < 1;
            LoadProgress.Progress = Math.Clamp(e.Value, 0, 1);
        });
        engine.Navigated += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
        {
            tab.Url = e.Url;
            if (_tabs.ActiveTab?.Id == tab.Id) AddressBar.Text = e.Url == "about:blank" ? "" : e.Url;
            StatusLabel.Text = e.Success ? e.Url : $"⚠ failed: {e.Url}";
            UpdateNavButtons(engine);
            RefreshTabList();
            _ = PersistSessionAsync();
            _ = ApplyPerPageEnhancementsAsync(engine);
        });
        engine.TitleChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            tab.Title = engine.Title ?? tab.Url;
            RefreshTabList();
        });
        engine.HistoryStateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => UpdateNavButtons(engine));
    }

    /// <summary>Модуль 2/3: пользовательский CSS + (в будущем) переводчик/PiP-хуки после каждой загрузки.</summary>
    private async Task ApplyPerPageEnhancementsAsync(IEngine engine)
    {
        try
        {
            var url = engine.CurrentUrl ?? string.Empty;
            var css = UserCssService.BuildCssFor(url, _settings.CustomCss, _settings.SiteCssRules);
            if (_settings.BlockCosmeticFilters)
            {
                var cosmetic = _adblock.BuildCosmeticCss(url);
                if (cosmetic.Length > 0) css = css + "\n" + cosmetic;
            }
            if (!string.IsNullOrWhiteSpace(css))
                await engine.InjectUserCssAsync(css);

            if (_settings.ResistFingerprinting)
                await engine.EvaluateScriptAsync(FingerprintNoise.NoiseScript);

            await TryAutofillAsync(engine, url);
        }
        catch { /* страница могла уже смениться */ }
    }

    /// <summary>Автозаполнение из сейфа, если он разблокирован и есть запись для этого хоста.</summary>
    private async Task TryAutofillAsync(IEngine engine, string url)
    {
        if (!_vault.IsUnlocked) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        var entry = _vault.Entries.FirstOrDefault(en =>
            Uri.TryCreate(en.Site, UriKind.Absolute, out var s) &&
            string.Equals(s.Host, uri.Host, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
            await engine.EvaluateScriptAsync(AutofillScript.BuildFillScript(entry.Username, entry.Password));
    }

    private async Task UpdateFiltersInBackgroundAsync()
    {
        try
        {
            var status = await _filters.UpdateAsync(_settings.FilterSubscriptions);
            MainThread.BeginInvokeOnMainThread(() =>
                StatusLabel.Text = $"🛡 filters: {status.TotalRules} rules" + (status.Error != null ? $" (update error: {status.Error})" : ""));
        }
        catch { /* сеть недоступна — остаёмся на кэше/встроенном списке */ }
    }

    private string FilterStatusText()
    {
        var s = _filters.CurrentStatus();
        return $"Rules: {s.TotalRules} (network {_adblock.NetworkRuleCount}, cosmetic {_adblock.CosmeticRuleCount})" +
               (s.UpdatedAt != null ? $"\nUpdated: {s.UpdatedAt:yyyy-MM-dd HH:mm}" : "\nNever updated (built-in list active)");
    }

    private async Task StartDownloadAsync(string url)
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "downloads");
        var item = await _downloads.DownloadAsync(url, dir);
        await DisplayAlert("Download", $"{Path.GetFileName(item.FilePath)}: {item.Status}", "OK");
    }

    private void UpdateNavButtons(IEngine engine)
    {
        BackButton.IsEnabled = engine.CanGoBack;
        ForwardButton.IsEnabled = engine.CanGoForward;
    }

    private IEngine? ActiveEngine =>
        _tabs.ActiveTab != null && _engines.TryGetValue(_tabs.ActiveTab.Id, out var e) ? e : null;

    // ---------- UI-обработчики ----------

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TabRow row && _tabs.Find(row.Id) is { } tab)
            ActivateTab(tab);
    }

    private void OnCloseTabClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Guid id }) return;
        if (_engines.Remove(id, out var engine)) engine.Dispose();
        _tabs.CloseTab(id);
        if (_tabs.ActiveTab is { } next) ActivateTab(next);
        else
        {
            var fresh = _tabs.OpenTab(_settings.HomePage);
            ActivateTab(fresh, navigateIfNew: true);
        }
        _ = PersistSessionAsync();
    }

    private void OnNewTabClicked(object sender, EventArgs e)
    {
        var tab = _tabs.OpenTab(_settings.HomePage);
        ActivateTab(tab, navigateIfNew: true);
        AddressBar.Focus();
    }

    private void OnAddressCompleted(object sender, EventArgs e) => Go();
    private void OnGoClicked(object sender, EventArgs e) => Go();

    private void Go()
    {
        if (_tabs.ActiveTab is not { } tab) return;
        var url = UrlNormalizer.Normalize(AddressBar.Text, _settings.GetDefaultSearchEngine());
        EngineFor(tab).Navigate(url);
    }

    private void OnBackClicked(object sender, EventArgs e) => ActiveEngine?.GoBack();
    private void OnForwardClicked(object sender, EventArgs e) => ActiveEngine?.GoForward();
    private void OnReloadClicked(object sender, EventArgs e) => ActiveEngine?.Reload();
    private void OnHomeClicked(object sender, EventArgs e) => ActiveEngine?.Navigate(_settings.HomePage);

    private async void OnBookmarkClicked(object sender, EventArgs e)
    {
        if (_tabs.ActiveTab is not { } tab || tab.Url == "about:blank") return;
        var bookmarks = await _data.LoadBookmarksAsync();
        bookmarks.Add(new Bookmark { Url = tab.Url, Title = tab.Title });
        await _data.SaveBookmarksAsync(bookmarks);
        StatusLabel.Text = "★ bookmark saved";
    }

    private async void OnMenuClicked(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet("GhostFox", "Cancel", null,
            "Reader mode", "Passwords", "Draw gesture", "Import bookmarks (.html)",
            "Export data", "Settings", "Toggle incognito", "Clear private data");
        switch (action)
        {
            case "Reader mode" when ActiveEngine is { } engine:
                await engine.EvaluateScriptAsync(ReaderMode.ExtractArticleScript);
                break;
            case "Settings":
                await Navigation.PushModalAsync(new SettingsPage(_settings, async s =>
                {
                    _settings = s;
                    await _data.SaveSettingsAsync(s);
                    ThemeApplier.Apply(s);
                    _gestureDrawable.Stroke = Color.FromArgb(ThemeCatalog.Get(s.ThemeName, s.AccentColorHex).Accent);
                    foreach (var eng in _engines.Values) eng.ApplyPrivacySettings(s);
                },
                async () =>
                {
                    var status = await _filters.UpdateAsync(_settings.FilterSubscriptions);
                    return FilterStatusText() + (status.Error != null ? $"\nLast error: {status.Error}" : "");
                },
                FilterStatusText()));
                break;
            case "Passwords":
                await Navigation.PushModalAsync(new VaultPage(_vault));
                break;
            case "Draw gesture":
                _gesturePoints.Clear();
                _gestureDrawable.Points.Clear();
                GesturePad.IsVisible = true;
                StatusLabel.Text = "✏ draw a gesture: ← back, → forward, ↑ reload, ↓ new tab, ↓→ close tab";
                break;
            case "Import bookmarks (.html)":
                await ImportBookmarksAsync();
                break;
            case "Export data":
                await ExportDataAsync();
                break;
            case "Toggle incognito":
                _settings.Incognito = !_settings.Incognito;
                StatusLabel.Text = _settings.Incognito ? "🕶 incognito ON" : "incognito OFF";
                break;
            case "Clear private data":
                _data.WipePrivateData();
                foreach (var eng in _engines.Values) await eng.ClearPrivateDataAsync();
                StatusLabel.Text = "private data wiped (history, session, cookies)";
                break;
        }
    }

    // ---------- Жесты (Модуль 2) ----------

    private void OnGesturePan(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _gesturePoints.Clear();
                _gestureDrawable.Points.Clear();
                _gesturePoints.Add((0, 0));
                _gestureDrawable.Points.Add(new PointF(200, 200));
                break;
            case GestureStatus.Running:
                _gesturePoints.Add((e.TotalX, e.TotalY));
                _gestureDrawable.Points.Add(new PointF((float)e.TotalX + 200, (float)e.TotalY + 200));
                GesturePad.Invalidate();
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                GesturePad.IsVisible = false;
                var gesture = Core.Services.GestureRecognizer.Recognize(_gesturePoints);
                var action = Core.Services.GestureRecognizer.ResolveAction(gesture, _settings.GestureMap);
                StatusLabel.Text = action != null ? $"gesture '{gesture}' -> {action}" : $"gesture '{gesture}' not recognized";
                if (action != null) ExecuteGestureAction(action);
                break;
        }
    }

    private void ExecuteGestureAction(string action)
    {
        switch (action)
        {
            case "back": ActiveEngine?.GoBack(); break;
            case "forward": ActiveEngine?.GoForward(); break;
            case "reload": ActiveEngine?.Reload(); break;
            case "home": ActiveEngine?.Navigate(_settings.HomePage); break;
            case "newtab": OnNewTabClicked(this, EventArgs.Empty); break;
            case "closetab":
                if (_tabs.ActiveTab is { } tab)
                {
                    if (_engines.Remove(tab.Id, out var eng)) eng.Dispose();
                    _tabs.CloseTab(tab.Id);
                    if (_tabs.ActiveTab is { } next) ActivateTab(next);
                }
                break;
            case "reader": _ = ActiveEngine?.EvaluateScriptAsync(ReaderMode.ExtractArticleScript); break;
            case "scrolltop": _ = ActiveEngine?.EvaluateScriptAsync("window.scrollTo(0,0); 'ok'"); break;
        }
    }

    // ---------- Импорт/экспорт (Модуль 2) ----------

    private async Task ImportBookmarksAsync()
    {
        try
        {
            var file = await Microsoft.Maui.Storage.FilePicker.Default.PickAsync(new Microsoft.Maui.Storage.PickOptions { PickerTitle = "Bookmarks HTML (Firefox/Chrome export)" });
            if (file == null) return;
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var parsed = new DataTransferService().ParseBookmarksHtml(await reader.ReadToEndAsync());
            var existing = await _data.LoadBookmarksAsync();
            var known = new HashSet<string>(existing.Select(b => b.Url));
            existing.AddRange(parsed.Where(b => known.Add(b.Url)));
            await _data.SaveBookmarksAsync(existing);
            StatusLabel.Text = $"imported {parsed.Count} bookmarks ({existing.Count} total)";
        }
        catch (Exception ex) { await DisplayAlert("Import", ex.Message, "OK"); }
    }

    private async Task ExportDataAsync()
    {
        try
        {
            var transfer = new DataTransferService();
            var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(dir);
            var bookmarks = await _data.LoadBookmarksAsync();
            var history = await _data.LoadHistoryAsync();
            var jsonPath = Path.Combine(dir, "ghostfox-export.json");
            var htmlPath = Path.Combine(dir, "bookmarks.html");
            await File.WriteAllTextAsync(jsonPath, transfer.ExportProfileJson(bookmarks, history, _settings));
            await File.WriteAllTextAsync(htmlPath, transfer.ExportBookmarksHtml(bookmarks));
            await DisplayAlert("Export", $"Profile JSON:\n{jsonPath}\n\nBookmarks HTML:\n{htmlPath}", "OK");
        }
        catch (Exception ex) { await DisplayAlert("Export", ex.Message, "OK"); }
    }
}

/// <summary>Необязательное расширение хоста: умеет показывать движок конкретной вкладки.</summary>
public interface IEngineSwitcher
{
    void ShowEngine(IEngine? engine);
}

/// <summary>Отображаемая строка вкладки (заголовок + индикатор загрузки).</summary>
public sealed record TabRow(Guid Id, string DisplayTitle);
