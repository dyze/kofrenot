using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kofrenot.UI.Views;

/// <summary>
/// kofrenot's logo, <see cref="FrameworkElement.Width"/> pixels square, drawn from the frame of
/// <c>kofrenot.ico</c> that fits its size on this screen: an <see cref="Image"/> given the .ico itself
/// shows its first frame (16 px), stretched and blurred.
/// </summary>
public sealed class AppLogo : Image
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/kofrenot.ico");
    private static readonly Lazy<IReadOnlyList<BitmapFrame>> Frames = new(LoadFrames);

    public AppLogo()
    {
        Stretch = Stretch.Uniform;
        Loaded += (_, _) => ShowFrame(VisualTreeHelper.GetDpi(this).DpiScaleX);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ShowFrame(newDpi.DpiScaleX);
    }

    private void ShowFrame(double dpiScale) => Source = FrameFor(Width * dpiScale);

    /// <summary>The smallest frame at least <paramref name="pixels"/> wide, or the largest one.</summary>
    private static BitmapFrame FrameFor(double pixels) =>
        Frames.Value.FirstOrDefault(frame => frame.PixelWidth >= pixels) ?? Frames.Value[^1];

    private static IReadOnlyList<BitmapFrame> LoadFrames()
    {
        var resource = Application.GetResourceStream(IconUri)
            ?? throw new InvalidOperationException("kofrenot.ico is missing from the assembly resources.");
        using var stream = resource.Stream;
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return [.. decoder.Frames.OrderBy(frame => frame.PixelWidth)];
    }
}
