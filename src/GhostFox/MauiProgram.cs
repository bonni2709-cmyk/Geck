using GhostFox.Core.Engines;
using GhostFox.Core.Services;
using Microsoft.Extensions.Logging;

namespace GhostFox;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Профиль: настройки/история/сессия/пароли лежат в AppData приложения.
        var profileDir = Path.Combine(FileSystem.AppDataDirectory, "profile");
        var data = new BrowserDataService(new JsonStore(profileDir));
        builder.Services.AddSingleton(data);
        builder.Services.AddSingleton(new PrivacyBlocklist());
        builder.Services.AddSingleton<TabManager>();
        builder.Services.AddSingleton(new DownloadService());
        builder.Services.AddSingleton(new PasswordVault(Path.Combine(profileDir, "vault.json")));

#if WINDOWS
        builder.Services.AddSingleton<IBrowserEngineHost, Platforms.Windows.GeckoEngineHost>();
#elif ANDROID
        builder.Services.AddSingleton<IBrowserEngineHost, Platforms.Android.GeckoViewEngineHost>();
#else
        builder.Services.AddSingleton<IBrowserEngineHost, PlaceholderEngineHost>();
#endif

        builder.Services.AddTransient<MainPage>();
        return builder.Build();
    }

    /// <summary>Фолбэк для платформ без нативного движка: UI живёт, страницы — заглушка.</summary>
    private sealed class PlaceholderEngineHost : IBrowserEngineHost
    {
        public IEngine CreateAndAttach(object hostContainer, Core.Models.BrowserSettings settings) => new PlaceholderEngine();
    }
}
