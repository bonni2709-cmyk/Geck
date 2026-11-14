using GhostFox.Core.Models;
using GhostFox.Core.Services;

namespace GhostFox;

/// <summary>Применяет тему из настроек к ресурсам приложения (Модуль 2, DynamicResource во всём XAML).</summary>
public static class ThemeApplier
{
    public static void Apply(BrowserSettings settings)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app == null) return;
        var palette = ThemeCatalog.Get(settings.ThemeName, settings.AccentColorHex);
        var res = app.Resources;
        res["GfBackground"] = Color.FromArgb(palette.Background);
        res["GfSurface"] = Color.FromArgb(palette.Surface);
        res["GfSurfaceAlt"] = Color.FromArgb(palette.SurfaceAlt);
        res["GfText"] = Color.FromArgb(palette.Text);
        res["GfTextDim"] = Color.FromArgb(palette.TextDim);
        res["GfAccent"] = Color.FromArgb(palette.Accent);
        app.RequestedTheme = palette.Name == "Light" ? AppTheme.Light : AppTheme.Dark;
        settings.DarkTheme = palette.Name != "Light";
    }
}
