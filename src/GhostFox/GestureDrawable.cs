using Microsoft.Maui.Graphics;

namespace GhostFox;

/// <summary>След жеста на оверлее (Модуль 2): рисует ломаную поверх страницы.</summary>
public sealed class GestureDrawable : IDrawable
{
    public List<PointF> Points { get; } = new();
    public Color Stroke { get; set; } = Color.FromArgb("#7C4DFF");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Points.Count < 2) return;
        canvas.StrokeColor = Stroke;
        canvas.StrokeSize = 4;
        var path = new PathF();
        path.MoveTo(Points[0]);
        for (var i = 1; i < Points.Count; i++) path.LineTo(Points[i]);
        canvas.DrawPath(path);
    }
}
