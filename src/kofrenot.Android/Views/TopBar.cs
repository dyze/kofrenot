using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The bar at the top of a screen (its look in App.axaml): ← when there is a screen before (it does
/// what Android's Back does, <see cref="Shell"/>), then its <see cref="Title"/> or what else it shows
/// (its content), then its <see cref="Actions"/> (⋮).
/// </summary>
public sealed class TopBar : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<TopBar, string?>(nameof(Title));

    public static readonly StyledProperty<bool> ShowsBackProperty = AvaloniaProperty.Register<TopBar, bool>(nameof(ShowsBack));

    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<TopBar, object?>(nameof(Actions));

    public static readonly RoutedEvent<RoutedEventArgs> BackEvent = RoutedEvent.Register<TopBar, RoutedEventArgs>(nameof(Back), RoutingStrategies.Bubble);

    private Button? _back;

    /// <summary>← tapped: the same as Android's Back.</summary>
    public event EventHandler<RoutedEventArgs>? Back
    {
        add => AddHandler(BackEvent, value);
        remove => RemoveHandler(BackEvent, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Whether ← shows: set each time the screen is shown, so not on the first screen.</summary>
    public bool ShowsBack
    {
        get => GetValue(ShowsBackProperty);
        private set => SetValue(ShowsBackProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ShowsBack = this.FindAncestorOfType<Shell>()?.CanGoBack ?? false;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_back is not null)
        {
            _back.Click -= Back_Click;
        }

        _back = e.NameScope.Find<Button>("PART_Back");
        if (_back is not null)
        {
            _back.Click += Back_Click;
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BackEvent));
}
