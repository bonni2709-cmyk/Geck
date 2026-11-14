using Gecko;

namespace GhostFox.Platforms.Windows;

/// <summary>
/// Поиск и однократная инициализация рантайма Gecko (Firefox 60 ESR x64 для GeckoFX 60).
/// Порядок поиска: GHOSTFOX_GECKO_DIR -> .\Firefox64 -> .\Firefox рядом с exe.
/// </summary>
internal static class GeckoRuntime
{
    private static bool _initialized;
    private static bool _failed;

    public static string? FindRuntimeDirectory()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("GHOSTFOX_GECKO_DIR"),
            Path.Combine(AppContext.BaseDirectory, "Firefox64"),
            Path.Combine(AppContext.BaseDirectory, "Firefox"),
        };
        foreach (var dir in candidates)
        {
            if (!string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, "xul.dll")))
                return dir;
        }
        return null;
    }

    public static bool EnsureInitialized()
    {
        if (_initialized) return true;
        if (_failed) return false;
        try
        {
            var dir = FindRuntimeDirectory();
            if (dir == null) { _failed = true; return false; }
            Xpcom.Initialize(dir);
            _initialized = true;
            return true;
        }
        catch
        {
            _failed = true; // битый/несовместимый рантайм — UI уйдёт в PlaceholderEngine, а не упадёт
            return false;
        }
    }
}
