#if ANDROID
using GhostFox.Core.Engines;
using GhostFox.Core.Models;

namespace GhostFox.Platforms.Android;

/// <summary>
/// МОДУЛЬ 3 — задел на Android. Реальная реализация появится после подключения
/// Xamarin-биндинга GeckoView (org.mozilla.geckoview:geckoview) — см. docs/ANDROID_GECKOVIEW.md.
/// Контракт уже зафиксирован IEngine, поэтому MainPage/Core менять не придётся:
/// здесь GeckoSession/GeckoView будут обёрнуты так же, как GeckoWebBrowser на Windows.
/// </summary>
public sealed class GeckoViewEngineHost : IBrowserEngineHost
{
    public IEngine CreateAndAttach(object hostContainer, BrowserSettings settings, bool isOverlay = false)
        => throw new NotImplementedException(
            "GeckoView is not wired yet (Module 3). See docs/ANDROID_GECKOVIEW.md.");
}
#endif
