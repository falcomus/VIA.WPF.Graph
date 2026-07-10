using SkiaSharp;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Owns Skia text font and paint objects for modern text rendering and measuring.
/// </summary>
internal sealed class SkiaTextStyle : IDisposable
{
    public SkiaTextStyle(SKTypeface typeface, float size, SKColor color, bool isBold)
    {
        ArgumentNullException.ThrowIfNull(typeface);

        Font = new SKFont(typeface, size)
        {
            Embolden = isBold,
            Subpixel = true
        };

        Paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Fill
        };
    }

    public SKFont Font { get; }

    public SKPaint Paint { get; }

    public float MeasureText(string text)
    {
        return string.IsNullOrEmpty(text)
            ? 0f
            : Font.MeasureText(text, Paint);
    }

    public void DrawText(SKCanvas canvas, string text, float x, float baselineY)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        canvas.DrawText(text, x, baselineY, SKTextAlign.Left, Font, Paint);
    }

    public void Dispose()
    {
        Font.Dispose();
        Paint.Dispose();
    }
}
