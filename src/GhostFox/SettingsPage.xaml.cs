using GhostFox.Core.Models;

namespace GhostFox;

/// <summary>Экран настроек (Модуль 2). Значения пишутся в BrowserSettings и сразу применяются к живым движкам.</summary>
public partial class SettingsPage : ContentPage
{
    private readonly BrowserSettings _settings;
    private readonly Func<BrowserSettings, Task> _onSave;

    public SettingsPage(BrowserSettings settings, Func<BrowserSettings, Task> onSave)
    {
        InitializeComponent();
        _settings = settings;
        _onSave = onSave;

        BlockTrackersSwitch.IsToggled = settings.BlockTrackersAndAds;
        JavaScriptSwitch.IsToggled = settings.EnableJavaScript;
        CookiesSwitch.IsToggled = settings.AcceptCookies;
        WebRtcSwitch.IsToggled = settings.DisableWebRtc;
        FingerprintSwitch.IsToggled = settings.ResistFingerprinting;
        ClearOnExitSwitch.IsToggled = settings.ClearOnExit;
        HomePageEntry.Text = settings.HomePage;
        UserAgentEntry.Text = settings.UserAgent;
        CustomCssEditor.Text = settings.CustomCss;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _settings.BlockTrackersAndAds = BlockTrackersSwitch.IsToggled;
        _settings.EnableJavaScript = JavaScriptSwitch.IsToggled;
        _settings.AcceptCookies = CookiesSwitch.IsToggled;
        _settings.DisableWebRtc = WebRtcSwitch.IsToggled;
        _settings.ResistFingerprinting = FingerprintSwitch.IsToggled;
        _settings.ClearOnExit = ClearOnExitSwitch.IsToggled;
        _settings.HomePage = string.IsNullOrWhiteSpace(HomePageEntry.Text) ? _settings.HomePage : HomePageEntry.Text.Trim();
        _settings.UserAgent = string.IsNullOrWhiteSpace(UserAgentEntry.Text) ? _settings.UserAgent : UserAgentEntry.Text.Trim();
        _settings.CustomCss = CustomCssEditor.Text ?? string.Empty;
        await _onSave(_settings);
        await Navigation.PopModalAsync();
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}
