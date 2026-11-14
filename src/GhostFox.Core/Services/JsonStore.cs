using System.Text.Json;
using GhostFox.Core.Models;

namespace GhostFox.Core.Services;

/// <summary>Крошечное JSON-хранилище в папке профиля. Никаких внешних зависимостей.</summary>
public sealed class JsonStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _profileDirectory;

    public JsonStore(string profileDirectory)
    {
        _profileDirectory = profileDirectory;
        Directory.CreateDirectory(profileDirectory);
    }

    public string ProfileDirectory => _profileDirectory;
    public string PathFor(string fileName) => Path.Combine(_profileDirectory, fileName);

    public async Task<T> LoadAsync<T>(string fileName, Func<T> fallback, CancellationToken ct = default) where T : class
    {
        try
        {
            var path = PathFor(fileName);
            if (!File.Exists(path)) return fallback();
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
        }
        catch { return fallback(); } // битый файл настроек не должен ронять браузер
    }

    public async Task SaveAsync<T>(string fileName, T value, CancellationToken ct = default)
    {
        var path = PathFor(fileName);
        var tmp = path + ".tmp"; // атомарная замена: защита от вылета в середине записи (менеджер сессий!)
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, Options), ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>Настройки + история + закладки + сессия поверх JsonStore.</summary>
public sealed class BrowserDataService
{
    private readonly JsonStore _store;
    public BrowserDataService(JsonStore store) => _store = store;

    public Task<BrowserSettings> LoadSettingsAsync() => _store.LoadAsync("settings.json", () => new BrowserSettings());
    public Task SaveSettingsAsync(BrowserSettings s) => _store.SaveAsync("settings.json", s);

    public Task<SessionState> LoadSessionAsync() => _store.LoadAsync("session.json", () => new SessionState());
    public Task SaveSessionAsync(SessionState s) => _store.SaveAsync("session.json", s);

    public Task<List<HistoryEntry>> LoadHistoryAsync() => _store.LoadAsync("history.json", () => new List<HistoryEntry>());
    public Task SaveHistoryAsync(List<HistoryEntry> h) => _store.SaveAsync("history.json", h);

    public Task<List<Bookmark>> LoadBookmarksAsync() => _store.LoadAsync("bookmarks.json", () => new List<Bookmark>());
    public Task SaveBookmarksAsync(List<Bookmark> b) => _store.SaveAsync("bookmarks.json", b);

    public string ProfileDirectory => _store.ProfileDirectory;

    // --- Именованные сессии (Модуль 3) ---
    private static string SafeName(string name) =>
        string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

    public async Task<List<string>> ListNamedSessionsAsync() =>
        await _store.LoadAsync("sessions-index.json", () => new List<string>());

    public async Task SaveNamedSessionAsync(string name, SessionState state)
    {
        await _store.SaveAsync($"session-{SafeName(name)}.json", state);
        var index = await ListNamedSessionsAsync();
        if (!index.Contains(name)) { index.Add(name); await _store.SaveAsync("sessions-index.json", index); }
    }

    public Task<SessionState> LoadNamedSessionAsync(string name) =>
        _store.LoadAsync($"session-{SafeName(name)}.json", () => new SessionState());

    public async Task DeleteNamedSessionAsync(string name)
    {
        try { var path = _store.PathFor($"session-{SafeName(name)}.json"); if (File.Exists(path)) File.Delete(path); } catch { }
        var index = await ListNamedSessionsAsync();
        index.Remove(name);
        await _store.SaveAsync("sessions-index.json", index);
    }

    /// <summary>Инкогнито/очистка при выходе: кэш и куки чистит движок, здесь — наши JSON.</summary>
    public void WipePrivateData()
    {
        foreach (var f in new[] { "history.json", "session.json" })
        {
            try { var p = _store.PathFor(f); if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }
}
