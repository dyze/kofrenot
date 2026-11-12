using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kofrenot.Phone.Views;

/// <summary>A sideways shift and a fade eased to where they go, frame by frame, fast first then slowing down.</summary>
public static class Slide
{
    /// <summary>
    /// <paramref name="shift"/> of <paramref name="on"/> moved to <paramref name="x"/>, <paramref name="on"/> faded to
    /// <paramref name="opacity"/>, in <paramref name="duration"/>; at once when <paramref name="on"/> is not shown.
    /// </summary>
    public static Task ToAsync(Visual on, TranslateTransform shift, double x, double opacity, TimeSpan duration)
    {
        if (TopLevel.GetTopLevel(on) is not { } top)
        {
            shift.X = x;
            on.Opacity = opacity;
            return Task.CompletedTask;
        }

        var (fromX, fromOpacity) = (shift.X, on.Opacity);

        // What follows runs after the frame, not inside it.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TimeSpan? start = null;
        void Frame(TimeSpan now)
        {
            start ??= now;
            var progress = Math.Min(1, (now - start.Value) / duration);
            var eased = EaseOut(progress);
            shift.X = fromX + (x - fromX) * eased;
            on.Opacity = fromOpacity + (opacity - fromOpacity) * eased;
            if (progress < 1)
            {
                top.RequestAnimationFrame(Frame);
            }
            else
            {
                done.SetResult();
            }
        }

        top.RequestAnimationFrame(Frame);
        return done.Task;
    }

    private static double EaseOut(double progress) => 1 - Math.Pow(1 - progress, 3);
}
