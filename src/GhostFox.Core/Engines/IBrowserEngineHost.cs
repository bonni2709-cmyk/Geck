namespace GhostFox.Core.Engines;

/// <summary>
/// Платформенный "хост": создаёт IEngine и встраивает его нативный контрол
/// в указанный MAUI-контейнер (на Windows это Grid-плейсхолдер в MainPage).
/// Регистрация — в MauiProgram через условную компиляцию платформы.
/// </summary>
public interface IBrowserEngineHost
{
    /// <param name="hostContainer">Платформенный контейнер (на Windows — Microsoft.Maui.Controls.Grid).</param>
    /// <param name="settings">Текущие настройки, применяемые к движку сразу после создания.</param>
    /// <param name="isOverlay">true для PiP-движка: он живёт поверх страницы и не участвует в переключении вкладок.</param>
    IEngine CreateAndAttach(object hostContainer, Models.BrowserSettings settings, bool isOverlay = false);
}
