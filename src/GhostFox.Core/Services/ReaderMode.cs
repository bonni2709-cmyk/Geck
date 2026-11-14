namespace GhostFox.Core.Services;

/// <summary>
/// Модуль 3 (каркас уже сейчас): JS для режима чтения — вырезает статью из DOM
/// и заменяет тело документа очищённой версией. Выполняется через IEngine.EvaluateScriptAsync.
/// Это упрощённая эвристика; полноценный Readability.js подключается в Модуле 3.
/// </summary>
public static class ReaderMode
{
    public const string ExtractArticleScript = """
        (() => {
          const candidates = [...document.querySelectorAll('article, main, [role=main], .post, .article, .content')];
          let best = document.body, bestScore = 0;
          for (const el of candidates) {
            const text = (el.innerText || '').trim();
            const score = text.length - (el.querySelectorAll('a').length * 40);
            if (score > bestScore) { bestScore = score; best = el; }
          }
          const title = document.title || 'Reader';
          const html = best ? best.innerHTML : document.body.innerHTML;
          document.head.innerHTML = '<meta charset="utf-8"><title>' + title + '</title>' +
            '<style>body{font-family:Georgia,serif;max-width:720px;margin:40px auto;padding:0 20px;line-height:1.7;font-size:19px;background:#111;color:#eee}img{max-width:100%}a{color:#8ab4ff}</style>';
          document.body.innerHTML = '<h1>' + title + '</h1>' + html;
          return title;
        })();
        """;

    /// <summary>Обёртка пользовательского CSS в JS-инжект (универсальна для GeckoFX и GeckoView).</summary>
    public static string BuildCssInjectionScript(string css)
    {
        var escaped = css.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");
        return $"(() => {{ const s=document.createElement('style'); s.id='ghostfox-user-css'; s.textContent=`{escaped}`; (document.head||document.documentElement).appendChild(s); return 'ok'; }})();";
    }
}
