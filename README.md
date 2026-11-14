# 🦊 GhostFox

Приватный кроссплатформенный браузер на движке **Gecko**: Windows (сейчас) → Android (следом).

## Архитектура (зафиксирована, не меняется между модулями)

```
        ┌─────────────────────────────────────────────┐
        │  GhostFox ( .NET MAUI ) — общий UI           │
        │  MainPage: вкладки, адресная строка, тулбар  │
        └──────────────────┬──────────────────────────┘
                           │ только через интерфейс
        ┌──────────────────▼──────────────────────────┐
        │  GhostFox.Core (net8.0) — вся логика         │
        │  IEngine, TabManager, PrivacyBlocklist,      │
        │  PasswordVault (AES-GCM), DownloadService,   │
        │  сессии, настройки, Reader Mode              │
        └─────────┬───────────────────────┬───────────┘
                  │ Windows               │ Android (Модуль 3)
        ┌─────────▼──────────┐  ┌─────────▼───────────┐
        │ GeckoFxEngine      │  │ GeckoViewEngine     │
        │ GeckoFX 60.0.56 +  │  │ GeckoView (Mozilla) │
        │ Firefox 60 ESR x64 │  │ GeckoSession        │
        └────────────────────┘  └─────────────────────┘
```

- **Почему не «чистый Gecko»**: это движок, а не библиотека — сборка требует MozillaBuild и ~32 ГБ ОЗУ. Используем готовые обёртки.
- **Windows**: `Geckofx60.64` (NuGet, v60.0.56). Его `GeckoWebBrowser` — WinForms-контрол; в WinUI3 (MAUI Windows) он хостится через HWND: `SetParent` + синхронизация позиции с MAUI-контейнером (`Platforms/Windows/GeckoEngineHost.cs`).
- **Android**: GeckoView — заготовка `Platforms/Android/GeckoViewEngineHost.cs` + план в [docs/ANDROID_GECKOVIEW.md](docs/ANDROID_GECKOVIEW.md). UI и Core при замене движка не переписываются — меняется только реализация `IEngine`.
- **Fallback**: если рантайм Gecko не найден рядом с exe, UI стартует на `PlaceholderEngine` (вкладки/адресная строка/история работают), а не падает.

## Что уже есть (Модуль 1 + каркас 2/3)

| Фича | Статус |
|---|---|
| Навигация, назад/вперёд, reload, прогресс-бар | ✅ Модуль 1 |
| Вертикальные вкладки (создание/переключение/закрытие, движок на вкладку) | ✅ |
| Менеджер сессий (автосейв + восстановление после вылета) | ✅ каркас |
| Блокировщик трекеров/рекламы на уровне запроса | ✅ стартовый список, EasyList — Модуль 2 |
| Настройки: JS, куки, WebRTC, anti-fingerprint, User-Agent, инкогнито | ✅ экран + prefs Gecko |
| Кастомный CSS-инжектор | ✅ |
| Менеджер паролей (AES-256-GCM + PBKDF2) | ✅ Core |
| Загрузки с докачкой (HTTP Range) | ✅ |
| Режим чтения | ✅ эвристика, Readability.js — Модуль 3 |
| Закладки, история (JSON в профиле) | ✅ |
| PiP, переводчик, жесты, импорт из других браузеров | 🔜 Модуль 3 |

## Сборка

Локально в песочнице сборка **не выполняется** (нет SDK, лимит 128 МБ) — собирается GitHub Actions:

- `.github/workflows/build.yml`: тесты Core (ubuntu) → публикация Windows x64 (windows-latest) → докладывание рантайма Firefox 60 ESR → артефакт **`GhostFox-win-x64`**.
- Локально на Windows-машине: `dotnet workload install maui-windows`, положить Firefox 60 ESR x64 в `Firefox64/` рядом с проектом, `dotnet run --project src/GhostFox -f net8.0-windows10.0.19041.0`.

Подробнее: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/ANDROID_GECKOVIEW.md](docs/ANDROID_GECKOVIEW.md)
