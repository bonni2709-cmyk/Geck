# GhostFox — архитектура

## Слои

1. **GhostFox.Core** (`net8.0`, ноль платформенных зависимостей)
   Контракт `IEngine`, модели, сервисы: `TabManager`, `PrivacyBlocklist`, `PasswordVault`,
   `DownloadService` (докачка по Range), `BrowserDataService` (JSON-профиль, атомарная запись),
   `UrlNormalizer`, `ReaderMode`. Покрыт xUnit-тестами, собирается на Linux в CI за секунды.

2. **GhostFox** (.NET MAUI, сейчас только `net8.0-windows10.0.19041.0`)
   UI: вертикальные вкладки, тулбар, адресная строка, прогресс, экран настроек.
   Движок получается из DI как `IBrowserEngineHost` — UI не знает про GeckoFX.

3. **Платформенные реализации `IEngine`**
   - Windows: `GeckoFxEngine` (GeckoFX 60.0.56 поверх Firefox 60 ESR x64). Хостинг WinForms-контрола
     в WinUI3 — HWND/`SetParent`, позиция синхронизируется с MAUI Grid по таймеру 200 мс.
   - Android: `GeckoViewEngine` (Модуль 3, см. ANDROID_GECKOVIEW.md).
   - Fallback: `PlaceholderEngine` из Core — если рантайм не найден.

## Ключевые решения и риски

- **GeckoFX 60 — .NET Framework-обёртка.** Потребление из net8.0-windows разрешено через
  `AssetTargetFallback` (net461–net47). Это осознанный технический долг Модуля 1:
  если рантайм .NET 8 откажется грузить сборки GeckoFX, план Б — вынести Gecko-хост
  в отдельный .NET Framework 4.8 процесс и общаться с MAUI-шеллом по именованному каналу
  (контракт `IEngine` при этом не меняется, меняется только транспорт).
- **Каждая вкладка — отдельный `GeckoWebBrowser`** (виден только активный HWND).
  Полноценная изоляция процессов зависит от e10s в Gecko 60; в GeckoView это отдельные `GeckoSession`.
- **Приватность по умолчанию**: трекеры режутся до навигации (`Navigating` + blocklist),
  WebRTC и fingerprinting настраиваются prefs Gecko, инкогнито отключает запись сессии/истории.
- **Сессии пишутся атомарно** (tmp + rename): вылет в середине записи не бьёт файл восстановления.

## Протокол сборки в песочнице

Код пишется локально, собирается только в GitHub Actions (`windows-latest` + `ubuntu-latest` для тестов).
Кэши NuGet/SDK в репозиторий не попадают (`.gitignore`), рантайм Firefox (~100 МБ) докладывается шагом CI.
