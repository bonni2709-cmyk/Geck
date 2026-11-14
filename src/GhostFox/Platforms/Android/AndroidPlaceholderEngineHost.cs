#if ANDROID
using GhostFox.Core.Engines;
using GhostFox.Core.Models;

namespace GhostFox.Platforms.Android;

/// <summary>
/// Временный хост Android (Модуль 3): UI и вся логика уже работают на Android,
/// страницы отдаёт PlaceholderEngine. Следующий шаг — GeckoViewEngineHost
/// (биндинг org.mozilla.geckoview), контракт IEngine при этом не изменится.
/// </summary>
public sealed class AndroidPlaceholderEngineHost : IBrowserEngineHost
{
    public IEngine CreateAndAttach(object hostContainer, BrowserSettings settings, bool isOverlay = false) =>
        new PlaceholderEngine();
}
#endif
