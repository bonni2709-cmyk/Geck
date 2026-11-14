namespace GhostFox.Core.Services;

/// <summary>
/// Подписки на фильтры (EasyList/EasyPrivacy и свои): скачивание, кэш в профиле,
/// загрузка кэша при старте. Сеть не блокирует старт браузера: при отсутствии кэша
/// работает встроенный список трекеров из AdBlockEngine.
/// </summary>
public sealed class FilterSubscriptionService
{
    public sealed record Status(int TotalRules, DateTimeOffset? UpdatedAt, string? Error);

    public static readonly string[] DefaultSubscriptions =
    {
        "https://easylist.to/easylist/easylist.txt",
        "https://easylist.to/easylist/easyprivacy.txt",
    };

    private readonly string _cacheDirectory;
    private readonly AdBlockEngine _engine;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public FilterSubscriptionService(string profileDirectory, AdBlockEngine engine)
    {
        _cacheDirectory = Path.Combine(profileDirectory, "filters");
        _engine = engine;
        Directory.CreateDirectory(_cacheDirectory);
    }

    private string MetaPath => Path.Combine(_cacheDirectory, "updated.txt");

    /// <summary>Поднять кэш с диска (если есть). Возвращает число правил.</summary>
    public int LoadFromCache()
    {
        var total = 0;
        try
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, "list_*.txt"))
                total += _engine.LoadRules(File.ReadAllText(file));
        }
        catch { /* битый кэш не должен ронять браузер */ }
        return total;
    }

    public async Task<Status> UpdateAsync(IReadOnlyList<string> subscriptionUrls, CancellationToken ct = default)
    {
        var total = 0;
        string? error = null;
        for (var i = 0; i < subscriptionUrls.Count; i++)
        {
            try
            {
                var text = await _http.GetStringAsync(subscriptionUrls[i], ct).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(_cacheDirectory, $"list_{i}.txt"), text, ct).ConfigureAwait(false);
                total += _engine.LoadRules(text);
            }
            catch (Exception ex) { error = ex.Message; }
        }
        if (total > 0)
            await File.WriteAllTextAsync(MetaPath, DateTimeOffset.Now.ToString("o"), ct).ConfigureAwait(false);
        return new Status(_engine.NetworkRuleCount + _engine.CosmeticRuleCount, ReadUpdatedAt(), error);
    }

    public DateTimeOffset? ReadUpdatedAt()
    {
        try
        {
            return File.Exists(MetaPath) && DateTimeOffset.TryParse(File.ReadAllText(MetaPath), out var t) ? t : null;
        }
        catch { return null; }
    }

    public Status CurrentStatus() =>
        new(_engine.NetworkRuleCount + _engine.CosmeticRuleCount, ReadUpdatedAt(), null);
}
