using System.Text.Json;

namespace GhostFox.Core.Services;

/// <summary>Поиск по странице (Модуль 3): обёртка над window.find с подсветкой через CSS Highlight API, где доступна.</summary>
public static class FindInPage
{
    public static string BuildFindScript(string term, bool forward = true)
    {
        var literal = JsonSerializer.Serialize(term);
        var backwards = forward ? "false" : "true";
        return $$"""
            (() => {
              const term = {{literal}};
              if (!term) return 'empty';
              const found = window.find(term, false, {{backwards}}, true, false, true, false);
              return found ? 'found' : 'not-found';
            })();
            """;
    }
}
