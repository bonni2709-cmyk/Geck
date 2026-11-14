using GhostFox.Core.Models;

namespace GhostFox.Core.Engines;

/// <summary>
/// Заглушка движка: используется, если нативный Gecko-рантайм не найден
/// (например, первый запуск без папки Firefox64 рядом с exe, или CI smoke-тест UI).
/// UI при этом полностью работает — адресная строка, вкладки, история UI —
/// просто вместо страницы показывается объяснение через событие Navigated.
/// </summary>
public sealed class PlaceholderEngine : IEngine
{
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public string EngineName => "Placeholder (Gecko runtime not found)";
    public bool IsNativeEngineAvailable => false;
    public string? CurrentUrl => _historyIndex >= 0 ? _history[_historyIndex] : null;
    public string? Title => CurrentUrl is null ? "GhostFox" : $"[no engine] {CurrentUrl}";
    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    public bool IsLoading => false;

    public event EventHandler<NavigatingEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;
    public event EventHandler<ProgressEventArgs>? LoadProgressChanged;
    public event EventHandler? TitleChanged;
    public event EventHandler? HistoryStateChanged;
    public event EventHandler<DownloadRequestedEventArgs>? DownloadRequested;
    public event EventHandler<NavigatingEventArgs>? NewTabRequested;

    public void Navigate(string url)
    {
        var args = new NavigatingEventArgs { Url = url };
        Navigating?.Invoke(this, args);
        if (args.Cancel) return;

        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(url);
        _historyIndex++;

        LoadProgressChanged?.Invoke(this, new ProgressEventArgs { Value = 1 });
        Navigated?.Invoke(this, new NavigatedEventArgs { Url = url });
        TitleChanged?.Invoke(this, EventArgs.Empty);
        HistoryStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GoBack() { if (CanGoBack) { _historyIndex--; RaiseMoved(); } }
    public void GoForward() { if (CanGoForward) { _historyIndex++; RaiseMoved(); } }
    public void Reload() => RaiseMoved();
    public void Stop() { }

    private void RaiseMoved()
    {
        if (CurrentUrl is { } url)
            Navigated?.Invoke(this, new NavigatedEventArgs { Url = url });
        TitleChanged?.Invoke(this, EventArgs.Empty);
        HistoryStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<string?> EvaluateScriptAsync(string script, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task InjectUserCssAsync(string css, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void ApplyPrivacySettings(BrowserSettings settings) { }
    public Task ClearPrivateDataAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
}
