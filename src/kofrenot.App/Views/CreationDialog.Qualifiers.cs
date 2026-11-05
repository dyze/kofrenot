using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// The link step's qualifiers: a box for each of the link type's (<see cref="CreationForm.Qualifiers"/>), filled from
/// the input box, all optional. On a link updated, each says its value now; an empty box leaves it as it is.
/// </summary>
public partial class CreationDialog
{
    // Built when the step or the link type changes, not at each keystroke: the box typed in keeps the focus.
    private void ShowQualifiers()
    {
        QualifierPanel.Children.Clear();
        if (StepSide is not null)
        {
            return;
        }

        var lines = _form.Qualifiers;
        if (lines.Count > 0)
        {
            QualifierPanel.Children.Add(new TextBlock { Margin = new Thickness(0, 0, 0, 4), Opacity = 0.7, Text = "Qualifiers (optional)" });
        }

        foreach (var line in lines)
        {
            var row = new Grid { Margin = new Thickness(16, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = line.Field.Name, TextTrimming = TextTrimming.CharacterEllipsis };
            var box = new TextBox { Text = line.Text, ToolTip = line.Current is { } current ? $"Now: {current}. Empty leaves it as it is." : "Empty: none" };
            box.TextChanged += (_, _) =>
            {
                _form.SetQualifier(line.Field, box.Text);
                ShowForm();
            };
            Grid.SetColumn(box, 1);
            row.Children.Add(label);
            row.Children.Add(box);
            QualifierPanel.Children.Add(row);
        }
    }
}
