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
          document.querySelectorAll('nav, aside, footer, header, form, iframe, [role=navigation], .ad, .ads, .sidebar, .menu, .share').forEach(e => e.remove());
          const candidates = [...document.querySelectorAll('article, main, [role=main], .post, .article, .content, .entry')];
          let best = null, bestScore = 0;
          const score = (el) => {
            let s = 0;
            el.querySelectorAll('p').forEach(p => { const len = (p.innerText || '').trim().length; if (len > 80) s += len; });
            s -= el.querySelectorAll('a').length * 25;
            return s;
          };
          for (const el of candidates) { const s = score(el); if (s > bestScore) { bestScore = s; best = el; } }
          if (!best || bestScore < 120) best = document.body;
          const title = document.title || 'Reader';
          document.head.innerHTML = '<meta charset="utf-8"><title>' + title + '</title>' +
            '<style>body{font-family:Georgia,serif;max-width:720px;margin:40px auto;padding:0 20px;line-height:1.75;font-size:19px;background:#111;color:#eee}img{max-width:100%}a{color:#8ab4ff}pre,code{white-space:pre-wrap}</style>';
          document.body.innerHTML = '<h1>' + title + '</h1>' + best.innerHTML;
          window.scrollTo(0, 0);
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
