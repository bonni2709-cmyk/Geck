# Android: GeckoView (Модуль 3)

## План подключения (без изменения UI и Core)

1. В `GhostFox.csproj` раскомментировать TFM `net8.0-android` (строка уже заготовлена).
2. Подключить биндинг GeckoView. Варианты:
   - готовый NuGet-биндинг AAR `org.mozilla.geckoview:geckoview` (через `AndroidLibrary`/`Bind`), или
   - сгенерировать биндинг из AAR штатным Xamarin Binding Project.
3. Реализовать `Platforms/Android/GeckoViewEngineHost.cs` (файл-заготовка уже лежит в проекте):
   - `GeckoRuntime` создаётся один раз (аналогично `GeckoRuntime.cs` на Windows);
   - на вкладку — свой `GeckoSession`, в контейнер кладётся один `GeckoView`, у которого меняется сессия;
   - `NavigationDelegate.onLoadRequest` → событие `IEngine.Navigating` (сюда встанет тот же `PrivacyBlocklist`);
   - `ProgressDelegate` → `LoadProgressChanged`; загрузка файлов → `DownloadRequested` → общий `DownloadService`.
4. В `MauiProgram` ветка `#elif ANDROID` уже регистрирует `GeckoViewEngineHost` — больше ничего менять не нужно.

## Соответствие контрактов

| IEngine | GeckoFX (Windows) | GeckoView (Android) |
|---|---|---|
| Navigate | `GeckoWebBrowser.Navigate` | `GeckoSession.LoadUri` |
| GoBack/Forward | `GoBack/GoForward` | `GeckoSession.GoBack/GoForward` |
| EvaluateScript | `AutoJSContext.EvaluateScript` | `GeckoSession` + WebExtension `messaging` / `evaluateJS` (через порт) |
| ApplyPrivacySettings | prefs (`GeckoPreferences`) | `GeckoRuntimeSettings` + prefs через `setPref` |
| Изоляция вкладок | отдельный контрол на вкладку | отдельный `GeckoSession` на вкладку |

## Риски

- Размер APK/AAB: GeckoView добавляет ~100+ МБ на ABI — релиз только как AAB со сплитами по ABI.
- Биндинг GeckoView — самый трудоёмкий пункт: API большой, часть делегатов придётся биндить вручную.
