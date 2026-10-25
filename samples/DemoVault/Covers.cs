using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kofrenot.Samples;

/// <summary>Made-up pictures, drawn by WPF: book covers and a sketch, saved as JPEG.</summary>
internal static class Covers
{
    private static readonly Typeface Title = new(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface Author = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    /// <summary>A book cover: a gradient, the title over a circle in the middle (what a card shows), the author below.</summary>
    public static void Book(string path, string title, string author, Color top, Color bottom)
    {
        Save(path, 600, 900, context =>
        {
            context.DrawRectangle(new LinearGradientBrush(top, bottom, 90), null, new Rect(0, 0, 600, 900));
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), null, new Point(300, 450), 230, 230);
            Text(context, title, Title, 72, 300, 340, 540);
            Text(context, author, Author, 30, 300, 780, 520);
        });
    }

    /// <summary>A kitchen sketch: a floor plan of rectangles.</summary>
    public static void Sketch(string path)
    {
        Save(path, 1200, 800, context =>
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 241, 232)), null, new Rect(0, 0, 1200, 800));
            var line = new Pen(new SolidColorBrush(Color.FromRgb(60, 70, 90)), 6);
            context.DrawRectangle(null, line, new Rect(100, 100, 1000, 600));
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(180, 205, 225)), line, new Rect(100, 100, 1000, 120));
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(225, 200, 160)), line, new Rect(450, 380, 300, 160));
            Text(context, "Kitchen", Author, 48, 600, 600, 600);
        });
    }

    private static void Text(DrawingContext context, string text, Typeface typeface, double size, double centreX, double top, double width)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Brushes.White, 1)
        {
            MaxTextWidth = width,
            TextAlignment = TextAlignment.Center,
        };
        context.DrawText(formatted, new Point(centreX - width / 2, top));
    }

    private static void Save(string path, int width, int height, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            draw(context);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
