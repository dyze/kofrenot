using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// A node's fields (<see cref="NodeFields"/>), each edited in place (<see cref="FieldPlanner"/>): a
/// text, number, date or address in a box (Enter or leaving it writes it, Esc puts it back, empty
/// clears it), a checkbox, a choice in a list. A value set on two devices offers each to keep; two
/// fields of one name offer to merge them. Add field… adds one of the node's own, with its first value.
/// </summary>
public partial class NodeDetailsPanel
{
    private const string ClearChoice = "—";

    // The box being typed in, and the field it writes: its value is written when Enter is pressed or it is left.
    private (TextBox Box, NodeField Field)? _fieldBox;

    private void ShowFields(NodeDetails details)
    {
        _fieldBox = null;
        FieldGrid.Children.Clear();
        FieldGrid.RowDefinitions.Clear();
        var canEdit = WriteChanges is not null;
        foreach (var field in details.Fields)
        {
            AddFieldRow(field, details.Fields, canEdit);
        }

        AddFieldButton.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
        if (OwnFieldKind.ItemsSource is null)
        {
            OwnFieldKind.ItemsSource = FieldPlanner.OwnKinds;
            OwnFieldKind.SelectedIndex = 0;
        }
    }

    private void AddFieldRow(NodeField field, IReadOnlyList<NodeField> fields, bool canEdit)
    {
        var row = FieldGrid.RowDefinitions.Count;
        FieldGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = new TextBlock
        {
            Margin = new Thickness(0, 4, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7,
            Text = field.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = field.Type is { } type ? $"A field of {type.Name}" : "A field of this node's own",
        };
        Place(label, row, 0);
        Place(EditorOf(field, canEdit), row, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 0, 0) };
        if (field.Kind == FieldKind.Url && field.Value is { } url)
        {
            actions.Children.Add(SmallButton("", "Open this address", () => OpenAddress(url)));
        }

        if (canEdit && fields.FirstOrDefault(other => other != field && other.IsOwn != field.IsOwn && NameKey.Of(other.Name) == NameKey.Of(field.Name)) is { } twin && !field.IsOwn)
        {
            actions.Children.Add(SmallButton("", $"Merge {twin.Label} into {field.Label}", () => Merge(field, twin)));
        }

        Place(actions, row, 2);
        if (field.Values is { IsConflict: true } values && canEdit)
        {
            FieldGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var keep = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            keep.Children.Add(new TextBlock
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("SystemFillColorCautionBrush"),
                Text = "Set on two devices. Keep:",
            });
            foreach (var (version, shown) in values.Current.Zip(field.Conflict))
            {
                var button = new Button { Margin = new Thickness(0, 2, 6, 0), Padding = new Thickness(8, 1, 8, 1), Focusable = false, Content = shown };
                button.Click += (_, _) => WriteFields(planner => planner.Keep(Node, field, version), "Field conflict resolved");
                keep.Children.Add(button);
            }

            Place(keep, row + 1, 1);
            Grid.SetColumnSpan(keep, 2);
        }
    }

    private Node Node => Browser.Details!.Node;

    private void Place(UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        FieldGrid.Children.Add(element);
    }

    private FrameworkElement EditorOf(NodeField field, bool canEdit)
    {
        var margin = new Thickness(0, 4, 0, 0);
        if (!canEdit)
        {
            return new TextBlock { Margin = margin, Text = field.Display, TextWrapping = TextWrapping.Wrap };
        }

        switch (field.Kind)
        {
            case FieldKind.Checkbox:
                var check = new CheckBox { Margin = margin, MinWidth = 0, IsChecked = FieldValue.IsChecked(field.Value), Focusable = false };
                check.Click += (_, _) => WriteFields(planner => planner.Check(Node, field, check.IsChecked == true), "Field set");
                return check;
            case FieldKind.Choice:
                var list = new ComboBox { Margin = margin, MinWidth = 140, HorizontalAlignment = HorizontalAlignment.Left, Focusable = false };
                list.ItemsSource = field.Choices.Prepend(ClearChoice).Concat(field.Value is { } kept && !field.Choices.Contains(kept) ? [kept] : []).ToList();
                list.SelectedItem = field.Value ?? ClearChoice;
                list.SelectionChanged += (_, _) =>
                {
                    if (list.SelectedItem is string chosen && chosen != (field.Value ?? ClearChoice))
                    {
                        WriteFields(planner => chosen == ClearChoice ? planner.Clear(Node, field) : planner.Set(Node, field, chosen), "Field set");
                    }
                };
                return list;
            default:
                var box = new TextBox { Margin = margin, Text = field.Value ?? string.Empty, ToolTip = HintOf(field.Kind) };
                box.GotKeyboardFocus += (_, _) => _fieldBox = (box, field);
                box.LostKeyboardFocus += (_, _) => WriteBox(box, field);
                return box;
        }
    }

    // The address is the user's: it is not logged.
    private void OpenAddress(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Log.Info("Action", "Field address opened");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Log.Warn("Action", $"Cannot open a field's address: {ex.Message}");
            MessageDialog.Inform(Window.GetWindow(this), $"Cannot open this address: {ex.Message}");
        }
    }

    private static string HintOf(FieldKind kind) => kind switch
    {
        FieldKind.Number => "A number, like 1234.5",
        FieldKind.Date => "A date, like 2026-10-27",
        FieldKind.Url => "An address, like https://example.com",
        _ => "Its text",
    };

    private Button SmallButton(string glyph, string tip, Action click)
    {
        var button = new Button
        {
            Padding = new Thickness(6, 2, 6, 2),
            Focusable = false,
            Style = (Style)FindResource("FlatButton"),
            ToolTip = tip,
            Content = new TextBlock { FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Text = glyph },
        };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Enter writes the field typed in, Esc puts its value back; the other keys type in it.</summary>
    private bool HandleFieldKey(Key key)
    {
        switch (key)
        {
            case Key.Enter when _fieldBox is { } typed:
                WriteBox(typed.Box, typed.Field);
                return true;
            case Key.Enter when OwnFieldForm.IsKeyboardFocusWithin:
                AddOwnField();
                return true;
            case Key.Escape when OwnFieldForm.IsKeyboardFocusWithin:
                CloseOwnField();
                return true;
            case Key.Escape when _fieldBox is { } typed:
                typed.Box.Text = typed.Field.Value ?? string.Empty;
                _fieldBox = null;
                FieldStatus.Visibility = Visibility.Collapsed;
                Focus();
                return true;
            default:
                return false;
        }
    }

    private void WriteBox(TextBox box, NodeField field)
    {
        if (_fieldBox?.Box != box || box.Text == (field.Value ?? string.Empty))
        {
            return;
        }

        _fieldBox = null;
        if (!WriteFields(planner => planner.Set(Node, field, box.Text), "Field set"))
        {
            _fieldBox = (box, field);
        }
    }

    /// <summary>Writes a plan of the node's fields, or says why it cannot; false when nothing was written.</summary>
    private bool WriteFields(Func<FieldPlanner, FieldPlan> plan, string action)
    {
        if (Browser is not { Graph: { } graph, Details: not null } || WriteChanges is null)
        {
            return false;
        }

        var planned = plan(new FieldPlanner(graph));
        FieldStatus.Text = planned.Status;
        FieldStatus.Visibility = planned.Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!planned.CanApply || !WriteChanges(planned.Changes, action))
        {
            return false;
        }

        Show();
        return true;
    }

    private void Merge(NodeField kept, NodeField dropped)
    {
        var owner = Window.GetWindow(this);
        var (mine, theirs) = (kept.Display, dropped.Display);
        var value = kept.Value is null || dropped.Value is null || kept.Value == dropped.Value
            ? kept.Value ?? dropped.Value
            : MessageDialog.Confirm(owner, $"Merge {dropped.Label} into {kept.Label}: keep which value?", $"“{mine}”", $"“{theirs}”") ? kept.Value : dropped.Value;
        if (value is null)
        {
            return;
        }

        if (MessageDialog.Confirm(owner, $"Merge {dropped.Label} into {kept.Label}, keeping “{FieldValue.Display(kept.Kind, value)}”?\n\n{dropped.Label} goes.", "Merge"))
        {
            WriteFields(planner => planner.Merge(Node, kept, dropped, value), "Fields merged");
        }
    }

    private void AddField_Click(object sender, RoutedEventArgs e)
    {
        OwnFieldForm.Visibility = Visibility.Visible;
        AddFieldButton.Visibility = Visibility.Collapsed;
        OwnFieldName.Focus();
    }

    private void AddOwnField_Click(object sender, RoutedEventArgs e) => AddOwnField();

    private void CancelOwnField_Click(object sender, RoutedEventArgs e) => CloseOwnField();

    private void AddOwnField()
    {
        if (OwnFieldKind.SelectedItem is FieldKind kind
            && WriteFields(planner => planner.AddOwn(Node, OwnFieldName.Text, kind, OwnFieldValue.Text), "Field of its own added"))
        {
            CloseOwnField();
        }
    }

    private void CloseOwnField()
    {
        OwnFieldName.Clear();
        OwnFieldValue.Clear();
        OwnFieldForm.Visibility = Visibility.Collapsed;
        AddFieldButton.Visibility = WriteChanges is null ? Visibility.Collapsed : Visibility.Visible;
        FieldStatus.Visibility = Visibility.Collapsed;
        Focus();
    }
}
