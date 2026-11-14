using GhostFox.Core.Models;

namespace GhostFox.Core.Services;

/// <summary>
/// Состояние вкладок (платформо-независимо). UI-слой держит по одному IEngine
/// на каждую вкладку и показывает движок активной. Изоляция процессов вкладок —
/// на совести движка (Gecko: fission/e10s, где доступен; GeckoView: отдельные GeckoSession).
/// </summary>
public sealed class TabManager
{
    private readonly List<BrowserTab> _tabs = new();
    public IReadOnlyList<BrowserTab> Tabs => _tabs;
    public BrowserTab? ActiveTab { get; private set; }

    public event EventHandler? Changed;

    public BrowserTab OpenTab(string url = "about:blank", bool activate = true)
    {
        var tab = new BrowserTab { Url = url, Title = url == "about:blank" ? "New Tab" : url };
        _tabs.Add(tab);
        if (activate || ActiveTab == null) ActiveTab = tab;
        Changed?.Invoke(this, EventArgs.Empty);
        return tab;
    }

    public void CloseTab(Guid id)
    {
        var index = _tabs.FindIndex(t => t.Id == id);
        if (index < 0) return;
        var wasActive = ActiveTab?.Id == id;
        _tabs.RemoveAt(index);
        if (wasActive)
            ActiveTab = _tabs.Count == 0 ? null : _tabs[Math.Min(index, _tabs.Count - 1)];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Activate(Guid id)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == id);
        if (tab == null) return;
        ActiveTab = tab;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public BrowserTab? Find(Guid id) => _tabs.FirstOrDefault(t => t.Id == id);

    public SessionState ToSession() => new()
    {
        Tabs = _tabs.Select(t => new BrowserTab { Id = t.Id, Title = t.Title, Url = t.Url, IsPinned = t.IsPinned }).ToList(),
        ActiveTabId = ActiveTab?.Id ?? Guid.Empty,
        SavedAt = DateTimeOffset.Now
    };

    public void Restore(SessionState session)
    {
        _tabs.Clear();
        _tabs.AddRange(session.Tabs);
        ActiveTab = _tabs.FirstOrDefault(t => t.Id == session.ActiveTabId) ?? _tabs.FirstOrDefault();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
