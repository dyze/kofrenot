using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kofrenot.Phone.Views;

/// <summary>A choice of a bottom sheet: its label, before it a dot of colour or an icon (a key of App.axaml's icons), and what it does.</summary>
public sealed record SheetChoice(string Label, Action Chosen, IBrush? Dot = null, bool IsEnabled = true, bool IsCurrent = false, string? Icon = null);

/// <summary>
/// Bottom sheets, as Material on a phone, instead of menus: a screen's actions (⋮), what ＋ adds, the
/// vault. A choice closes the sheet, then runs; a tap outside, or Back, closes it.
/// </summary>
public sealed partial class Shell
{
    public void ShowSheet(string? title, IEnumerable<SheetChoice> choices)
    {
        SheetTitle.Text = title;
        SheetTitle.IsVisible = title is not null;
        SheetChoices.Children.Clear();
        foreach (var choice in choices)
        {
            SheetChoices.Children.Add(ButtonOf(choice));
        }

        SheetScrim.IsVisible = true;
    }

    /// <summary>A sheet showing <paramref name="content"/> (a calendar), then <paramref name="choices"/>; the content closes it itself (<see cref="CloseSheet"/>).</summary>
    public void ShowSheet(string? title, Control content, IEnumerable<SheetChoice> choices)
    {
        ShowSheet(title, choices);
        SheetChoices.Children.Insert(0, content);
    }

    private bool IsSheetShown => SheetScrim.IsVisible;

    public void CloseSheet() => SheetScrim.IsVisible = false;

    private Button ButtonOf(SheetChoice choice)
    {
        var label = new TextBlock
        {
            Text = choice.Label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = choice.IsCurrent ? FontWeight.SemiBold : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        if (choice.Dot is { } dot)
        {
            content.Children.Add(new Ellipse { Width = 14, Height = 14, Margin = new Avalonia.Thickness(3), Fill = dot, VerticalAlignment = VerticalAlignment.Center });
        }
        else if (choice.Icon is { } icon)
        {
            content.Children.Add(new PathIcon { Data = PhoneTheme.Icon(icon), Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center });
        }

        content.Children.Add(label);
        var button = new Button { Content = content, Classes = { "choice" }, IsEnabled = choice.IsEnabled };
        button.Click += (_, _) =>
        {
            CloseSheet();
            choice.Chosen();
        };
        return button;
    }

    // Only a tap on the scrim itself, not on the sheet over it.
    private void SheetScrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == SheetScrim)
        {
            CloseSheet();
        }
    }
}
