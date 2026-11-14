using GhostFox.Core.Models;
using GhostFox.Core.Services;

namespace GhostFox;

/// <summary>
/// Экран настроек (Модуль 2): приватность, подписки фильтров, темы,
/// поисковые системы, глобальный и per-site CSS.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly BrowserSettings _settings;
    private readonly Func<BrowserSettings, Task> _onSave;
    private readonly Func<Task<string>> _updateFilters;
    private static readonly string[] ThemeNames = ThemeCatalog.All.Select(t => t.Name).ToArray();

    public SettingsPage(BrowserSettings settings, Func<BrowserSettings, Task> onSave,
                        Func<Task<string>> updateFilters, string filterStatus)
    {
        InitializeComponent();
        _settings = settings;
        _onSave = onSave;
        _updateFilters = updateFilters;

        BlockTrackersSwitch.IsToggled = settings.BlockTrackersAndAds;
        CosmeticSwitch.IsToggled = settings.BlockCosmeticFilters;
        JavaScriptSwitch.IsToggled = settings.EnableJavaScript;
        CookiesSwitch.IsToggled = settings.AcceptCookies;
        WebRtcSwitch.IsToggled = settings.DisableWebRtc;
        FingerprintSwitch.IsToggled = settings.ResistFingerprinting;
        ClearOnExitSwitch.IsToggled = settings.ClearOnExit;
        HomePageEntry.Text = settings.HomePage;
        UserAgentEntry.Text = settings.UserAgent;
        CustomCssEditor.Text = settings.CustomCss;
        FilterStatusLabel.Text = filterStatus;
        AccentEntry.Text = settings.AccentColorHex;

        ThemePicker.ItemsSource = ThemeNames;
        ThemePicker.SelectedItem = ThemeNames.Contains(settings.ThemeName) ? settings.ThemeName : ThemeNames[0];
        RefreshSearchPicker();
        RefreshSiteCssLabel();
    }

    private void RefreshSearchPicker()
    {
        SearchPicker.ItemsSource = _settings.SearchEngines.Select(e => e.Name).ToList();
        SearchPicker.SelectedItem = _settings.GetDefaultSearchEngine().Name;
    }

    private void RefreshSiteCssLabel() =>
        SiteCssLabel.Text = _settings.SiteCssRules.Count == 0
            ? "No per-site rules"
            : string.Join(", ", _settings.SiteCssRules.Select(r => r.HostPattern));

    private async void OnUpdateFiltersClicked(object sender, EventArgs e)
    {
        UpdateFiltersButton.IsEnabled = false;
        FilterStatusLabel.Text = "Updating…";
        try { FilterStatusLabel.Text = await _updateFilters(); }
        catch (Exception ex) { FilterStatusLabel.Text = "Update failed: " + ex.Message; }
        UpdateFiltersButton.IsEnabled = true;
    }

    private async void OnAddSearchEngineClicked(object sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Search engine", "Name (e.g. Yandex)");
        if (string.IsNullOrWhiteSpace(name)) return;
        var template = await DisplayPromptAsync("Search engine", "URL template with {query} (e.g. https://yandex.ru/search/?text={query})");
        if (string.IsNullOrWhiteSpace(template) || !template.Contains("{query}"))
        {
            await DisplayAlert("Search engine", "Template must contain {query}", "OK");
            return;
        }
        _settings.SearchEngines.Add(new SearchEngine { Name = name.Trim(), UrlTemplate = template.Trim() });
        RefreshSearchPicker();
    }

    private void OnRemoveSearchEngineClicked(object sender, EventArgs e)
    {
        if (SearchPicker.SelectedItem is string name && _settings.SearchEngines.Count > 1)
        {
            _settings.SearchEngines.RemoveAll(x => x.Name == name);
            RefreshSearchPicker();
        }
    }

    private async void OnAddSiteCssClicked(object sender, EventArgs e)
    {
        var host = await DisplayPromptAsync("Per-site CSS", "Host pattern: * or *.wikipedia.org or example.com");
        if (string.IsNullOrWhiteSpace(host)) return;
        var css = await DisplayPromptAsync("Per-site CSS", "CSS, e.g. body{filter:invert(1)}");
        if (string.IsNullOrWhiteSpace(css)) return;
        _settings.SiteCssRules.Add(new SiteCssRule { HostPattern = host.Trim(), Css = css });
        RefreshSiteCssLabel();
    }

    private void OnClearSiteCssClicked(object sender, EventArgs e)
    {
        _settings.SiteCssRules.Clear();
        RefreshSiteCssLabel();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _settings.BlockTrackersAndAds = BlockTrackersSwitch.IsToggled;
        _settings.BlockCosmeticFilters = CosmeticSwitch.IsToggled;
        _settings.EnableJavaScript = JavaScriptSwitch.IsToggled;
        _settings.AcceptCookies = CookiesSwitch.IsToggled;
        _settings.DisableWebRtc = WebRtcSwitch.IsToggled;
        _settings.ResistFingerprinting = FingerprintSwitch.IsToggled;
        _settings.ClearOnExit = ClearOnExitSwitch.IsToggled;
        _settings.HomePage = string.IsNullOrWhiteSpace(HomePageEntry.Text) ? _settings.HomePage : HomePageEntry.Text.Trim();
        _settings.UserAgent = string.IsNullOrWhiteSpace(UserAgentEntry.Text) ? _settings.UserAgent : UserAgentEntry.Text.Trim();
        _settings.CustomCss = CustomCssEditor.Text ?? string.Empty;
        if (ThemePicker.SelectedItem is string theme) _settings.ThemeName = theme;
        if (!string.IsNullOrWhiteSpace(AccentEntry.Text)) _settings.AccentColorHex = AccentEntry.Text.Trim();
        if (SearchPicker.SelectedItem is string search) _settings.DefaultSearchEngineName = search;
        await _onSave(_settings);
        await Navigation.PopModalAsync();
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}
