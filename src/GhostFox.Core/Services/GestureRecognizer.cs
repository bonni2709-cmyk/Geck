namespace GhostFox.Core.Services;

/// <summary>
/// Распознавание нарисованных жестов (Модуль 2).
/// Трек точек упрощается до последовательности направлений U/D/L/R:
/// накапливаем движение, каждые ~24 единицы фиксируем доминирующую ось,
/// соседние дубли сжимаем. "DR" = вниз-вправо (по умолчанию — закрыть вкладку).
/// </summary>
public sealed class GestureRecognizer
{
    public const double MinSegmentLength = 24;

    /// <summary>Карта жест -> действие по умолчанию. Действия: back, forward, reload, home, newtab, closetab, reader, scrolltop.</summary>
    public static Dictionary<string, string> DefaultMap() => new()
    {
        ["L"] = "back",
        ["R"] = "forward",
        ["U"] = "reload",
        ["DR"] = "closetab",
        ["D"] = "newtab",
        ["UD"] = "scrolltop",
    };

    public static string Recognize(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 2) return string.Empty;
        var directions = new List<char>();
        var anchor = points[0];
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - anchor.X;
            var dy = points[i].Y - anchor.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < MinSegmentLength) continue;
            var dir = Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? 'R' : 'L') : (dy >= 0 ? 'D' : 'U');
            if (directions.Count == 0 || directions[^1] != dir)
                directions.Add(dir);
            anchor = points[i];
        }
        return new string(directions.ToArray());
    }

    public static string? ResolveAction(string gesture, IReadOnlyDictionary<string, string> map) =>
        gesture.Length > 0 && map.TryGetValue(gesture, out var action) ? action : null;
}
