using System.Security.Cryptography;
using System.Text.Json;

namespace GhostFox.Core.Services;

/// <summary>Генератор паролей на криптографическом RNG (Модуль 2).</summary>
public static class PasswordGenerator
{
    private const string Alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+";

    public static string Generate(int length = 20)
    {
        if (length < 8) length = 8;
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}

/// <summary>Автозаполнение форм входа из сейфа (инжект через IEngine.EvaluateScriptAsync).</summary>
public static class AutofillScript
{
    public static string BuildFillScript(string username, string password)
    {
        var user = JsonSerializer.Serialize(username);
        var pass = JsonSerializer.Serialize(password);
        // Ставим значения через нативный setter, чтобы React/Vue-сайты увидели изменение.
        return $$"""
            (() => {
              const setVal = (el, v) => {
                const proto = el.constructor === HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
                const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
                setter.call(el, v);
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
              };
              const pwd = document.querySelector('input[type="password"]');
              if (!pwd) return 'no-password-field';
              const form = pwd.form;
              const scope = form || document;
              const user = scope.querySelector('input[type="email"], input[type="text"], input[name*="user" i], input[name*="login" i], input[autocomplete="username"]');
              if (user) setVal(user, {{user}});
              setVal(pwd, {{pass}});
              return 'filled';
            })();
            """;
    }
}
