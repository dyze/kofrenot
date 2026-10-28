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
/// text, number, date or address in a box (<see cref="NodeDetailsPanel"/>'s FieldEdit), a checkbox,
/// a choice in a list. A tab per type, then "Its own", when the node has fields of both, or of several
/// types: the tab chosen is kept by its name, from node to node. A value set on two devices offers each
/// to keep, its tab marked; two fields of one name offer to merge them. Add field… adds one of the
/// node's own, with its first value.
/// </summary>
public partial class NodeDetailsPanel
{
    private const string ClearChoice = "—";
    private const string OwnTab = "Its own";

    // The tab of fields shown, by its name: kept from node to node.
    private string? _fieldTab;

    private void ShowFields(NodeDetails details)
    {
        var typing = TypingKept(details.Node);
        _boxes.Clear();
        FieldGrid.Children.Clear();
        FieldGrid.RowDefinitions.Clear();
        var canEdit = WriteChanges is not null;
        var groups = details.Fields.GroupBy(TabOf).ToList();
        var shown = groups.FirstOrDefault(group => group.Key == _fieldTab) ?? groups.FirstOrDefault();
        ShowFieldTabs(groups, shown?.Key);
        foreach (var field in shown ?? Enumerable.Empty<NodeField>())
        {
            AddFieldRow(field, details.Fields, canEdit);
        }

        TypeBack(typing);

        AddFieldButton.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
        if (OwnFieldKind.ItemsSource is null)
        {
            OwnFieldKind.ItemsSource = FieldPlanner.OwnKinds;
            OwnFieldKind.SelectedIndex = 0;
        }
    }

    private static string TabOf(NodeField field) => field.Type?.Name ?? OwnTab;

    // A tab per group of fields when there are several: the one shown underlined, one in conflict marked.
    private void ShowFieldTabs(IReadOnlyList<IGrouping<string, NodeField>> groups, string? shown)
    {
        FieldTabs.Children.Clear();
        FieldTabs.Visibility = groups.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (groups.Count < 2)
        {
            return;
        }

        foreach (var group in groups)
        {
            var isShown = group.Key == shown;
            var tab = new Button
            {
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(8, 2, 8, 2),
                Focusable = false,
                Style = (Style)FindResource("FlatButton"),
                Content = new TextBlock
                {
                    Text = group.Any(field => field.Conflict.Count > 0) ? $"{group.Key} •" : group.Key,
                    FontWeight = isShown ? FontWeights.SemiBold : FontWeights.Normal,
                    TextDecorations = isShown ? TextDecorations.Underline : null,
                },
                ToolTip = group.Any(field => field.Conflict.Count > 0) ? "A value set on two devices: choose one" : null,
            };
            tab.Click += (_, _) => ShowFieldTab(group.Key);
            FieldTabs.Children.Add(tab);
        }
    }

    private void ShowFieldTab(string tab)
    {
        if (tab != _fieldTab && LeaveFields())
        {
            _fieldTab = tab;
            Show();
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
        if (field.Kind == FieldKind.Image)
        {
            return ImageEditorOf(field, margin, canEdit);
        }

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
                return BoxOf(field, margin);
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
            _fieldTab = OwnTab;
            CloseOwnField();
            Show();
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
