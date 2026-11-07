using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Kofrenot.UI.Views;

/// <summary>
/// A caution, such as a value set on two devices before they synced: the alert icon, then its
/// <see cref="Text"/> wrapping beside it, both in the caution colour.
/// </summary>
public sealed class CautionText : DockPanel
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CautionText), new PropertyMetadata(string.Empty, (sender, e) => ((CautionText)sender)._text.Text = (string)e.NewValue));

    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    public CautionText()
    {
        SetResourceReference(TextElement.ForegroundProperty, "SystemFillColorCautionBrush");
        var icon = new IconView { Kind = "alert", Margin = new Thickness(0, 2, 6, 0), VerticalAlignment = VerticalAlignment.Top };
        SetDock(icon, Dock.Left);
        Children.Add(icon);
        Children.Add(_text);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
