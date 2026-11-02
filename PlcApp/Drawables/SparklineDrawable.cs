using Microsoft.Maui.Graphics;

namespace PlcApp.Drawables;

/// <summary>
/// Grafic mic (sparkline) cu evolutia recenta a unei valori.
/// Se redeseneaza prin crearea unei instante noi la fiecare citire.
/// </summary>
public sealed class SparklineDrawable : IDrawable
{
    private readonly double[] _values;
    private readonly Color _color;

    public SparklineDrawable(IEnumerable<double> values, Color color)
    {
        _values = values.ToArray();
        _color = color;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var width = dirtyRect.Width;
        var height = dirtyRect.Height;

        if (width <= 1 || height <= 1)
            return;

        // Linie de referinta cat timp nu avem destule esantioane.
        if (_values.Length < 2)
        {
            canvas.StrokeColor = _color.WithAlpha(0.25f);
            canvas.StrokeSize = 1.5f;
            canvas.StrokeDashPattern = new float[] { 3, 3 };
            canvas.DrawLine(0, height / 2f, width, height / 2f);
            canvas.StrokeDashPattern = null;
            return;
        }

        var min = _values.Min();
        var max = _values.Max();
        var range = max - min;
        if (range <= 0)
            range = 1;

        var padding = 3f;
        var usable = height - (padding * 2);
        var stepX = width / (_values.Length - 1);

        var line = new PathF();
        var area = new PathF();

        for (var i = 0; i < _values.Length; i++)
        {
            var x = (float)(i * stepX);
            var normalized = (float)((_values[i] - min) / range);
            var y = padding + (1f - normalized) * usable;

            if (i == 0)
            {
                line.MoveTo(x, y);
                area.MoveTo(x, height);
                area.LineTo(x, y);
            }
            else
            {
                line.LineTo(x, y);
                area.LineTo(x, y);
            }
        }

        area.LineTo(width, height);
        area.Close();

        canvas.FillColor = _color.WithAlpha(0.14f);
        canvas.FillPath(area);

        canvas.StrokeColor = _color;
        canvas.StrokeSize = 2f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(line);

        // Punct pe ultima valoare.
        var lastX = width;
        var lastNormalized = (float)((_values[^1] - min) / range);
        var lastY = padding + (1f - lastNormalized) * usable;

        canvas.FillColor = _color;
        canvas.FillCircle(lastX - 2.5f, lastY, 2.5f);
    }
}
