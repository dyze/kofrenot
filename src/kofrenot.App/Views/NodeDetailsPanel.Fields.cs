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
/// a choice in a list, those of the tab shown (NodeDetailsPanel's Tabs), in as many columns as the
/// width holds (<see cref="FieldColumnsPanel"/>). A value set on two devices
/// offers each to keep, its tab marked; two fields of one name offer to merge them. Add field… adds
/// one of the node's own, with its first value.
/// </summary>
public partial class NodeDetailsPanel
{
    private const string ClearChoice = "—";

    // The node Add field… was started on, while its form is shown.
    private Guid? _ownFieldNode;

    private void ShowFields(NodeDetails details)
    {
        var typing = TypingKept(details);
        _boxes.Clear();
        _errors.Clear();
        // Add field… belongs to the node it was started on (leaving it asked about it: LeaveFields).
        if (OwnFieldForm.Visibility == Visibility.Visible && _ownFieldNode != details.Node.Id)
        {
            ResetOwnField();
        }

        FieldColumns.Children.Clear();
        var canEdit = WriteChanges is not null;
        foreach (var field in ShowTabs(details))
        {
            FieldColumns.Children.Add(FieldBlockOf(field, details.Fields, canEdit, details.Node));
        }

        TypeBack(typing);

        AddFieldButton.Visibility = canEdit && !(_shownTab?.IsContent ?? false) ? Visibility.Visible : Visibility.Collapsed;
        if (OwnFieldKind.ItemsSource is null)
        {
            OwnFieldKind.ItemsSource = FieldPlanner.OwnKinds;
            OwnFieldKind.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// A field's block: its label, its editor and its buttons, and under them, when it was set on two
    /// devices, the values to keep. Its buttons' column is one width in every block, so editors line up.
    /// A link's qualifier (<paramref name="holder"/> a link) with a value has × to take it out, asked first.
    /// </summary>
    private Grid FieldBlockOf(NodeField field, IReadOnlyList<NodeField> fields, bool canEdit, FieldHolder holder)
    {
        var block = new Grid();
        block.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        block.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 420 });
        block.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "FieldButtons" });
        block.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        block.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = new TextBlock
        {
            Margin = new Thickness(0, 4, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7,
            Text = field.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = field.TypeName is { } type ? $"A {(holder.IsLink ? "qualifier" : "field")} of {type}" : holder.IsLink ? "A qualifier of this link's own" : "A field of this node's own",
        };
        Place(block, label, 0, 0);
        Place(block, EditorOf(field, canEdit, holder), 0, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 0, 0) };
        if (field.Value is { } value && FieldValue.LinkOf(field.Kind, value) is { } link)
        {
            actions.Children.Add(FieldButton("open", OpenTipOf(field.Kind), () => OpenLink(link)));
        }

        if (canEdit && field.Kind == FieldKind.Date)
        {
            actions.Children.Add(FieldButton("calendar", "Choose a date", () => ShowCalendar(field, holder), focusable: true));
        }

        if (canEdit && fields.FirstOrDefault(other => other != field && other.IsOwn != field.IsOwn && NameKey.Of(other.Name) == NameKey.Of(field.Name)) is { } twin && !field.IsOwn)
        {
            actions.Children.Add(FieldButton("merge", $"Merge {twin.Label} into {field.Label}", () => Merge(field, twin, holder)));
        }

        if (canEdit && holder.IsLink && field.HasValue && field.Kind != FieldKind.Image)
        {
            actions.Children.Add(FieldButton("remove", $"Take {field.Label} out of this link", () => RemoveQualifier(field, holder)));
        }

        Place(block, actions, 0, 2);
        if (field.Values is { IsConflict: true } values && canEdit)
        {
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
                button.Click += (_, _) => WriteFields(planner => planner.Keep(holder, field, version), "Field conflict resolved", holder: holder);
                keep.Children.Add(button);
            }

            Place(block, keep, 1, 1);
            Grid.SetColumnSpan(keep, 2);
        }

        return block;
    }

    private Node Node => Browser.Details!.Node;

    private static void Place(Grid block, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        block.Children.Add(element);
    }

    private FrameworkElement EditorOf(NodeField field, bool canEdit, FieldHolder holder)
    {
        var margin = new Thickness(0, 4, 0, 0);
        if (field.Kind == FieldKind.Image)
        {
            return ImageEditorOf(field, margin, canEdit, holder);
        }

        if (!canEdit)
        {
            return new TextBlock { Margin = margin, Text = field.Display, TextWrapping = TextWrapping.Wrap };
        }

        switch (field.Kind)
        {
            case FieldKind.Checkbox:
                var check = new CheckBox { Margin = margin, MinWidth = 0, IsChecked = FieldValue.IsChecked(field.Value) };
                check.Click += (_, _) => WriteFields(planner => planner.Check(holder, field, check.IsChecked == true), "Field set", holder: holder);
                return check;
            case FieldKind.Choice:
                var list = new ComboBox { Margin = margin, MinWidth = 140, HorizontalAlignment = HorizontalAlignment.Left };
                list.ItemsSource = field.Choices.Prepend(ClearChoice).Concat(field.Value is { } kept && !field.Choices.Contains(kept) ? [kept] : []).ToList();
                list.SelectedItem = field.Value ?? ClearChoice;
                list.SelectionChanged += (_, _) =>
                {
                    if (list.SelectedItem is string chosen && chosen != (field.Value ?? ClearChoice))
                    {
                        WriteFields(planner => chosen == ClearChoice ? planner.Clear(holder, field) : planner.Set(holder, field, chosen), "Field set", holder: holder);
                    }
                };
                return list;
            default:
                return BoxOf(field, margin, holder);
        }
    }

    private static string OpenTipOf(FieldKind kind) => kind switch
    {
        FieldKind.Email => "Write a mail to this address",
        FieldKind.Address => "Show this address on a map",
        _ => "Open this address",
    };

    // A web address, a mail to write or a map: the link is the user's, it is not logged.
    private void OpenLink(Uri link)
    {
        try
        {
            Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true });
            Log.Info("Action", "Field link opened");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Log.Warn("Action", $"Cannot open a field's address: {ex.Message}");
            MessageDialog.Inform(Window.GetWindow(this), $"Cannot open this address: {ex.Message}");
        }
    }

    // An icon's button beside a field, as large as the details' own (pin, history); reached with Tab only
    // when it is the field's own way to set it (an image removed, a date chosen).
    private Button FieldButton(string icon, string tip, Action click, bool focusable = false)
    {
        var button = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = focusable,
            Style = (Style)FindResource("FlatButton"),
            ToolTip = tip,
            Content = new IconView { Kind = icon, Size = (double)FindResource("ActionIconSize") },
        };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>
    /// Writes a plan of the node's fields or a link's qualifiers (<paramref name="holder"/>, the node by default), or says
    /// why it cannot (under <paramref name="about"/> when given); false when nothing was written.
    /// </summary>
    private bool WriteFields(Func<FieldPlanner, FieldPlan> plan, string action, NodeField? about = null, FieldHolder? holder = null)
    {
        if (Browser is not { Graph: { } graph, Details: not null } || WriteChanges is null)
        {
            return false;
        }

        var planned = plan(new FieldPlanner(graph));
        ShowStatus(planned.Status, about, holder);
        if (!planned.CanApply || !WriteChanges(planned.Changes, action))
        {
            return false;
        }

        Show();
        return true;
    }

    private void Merge(NodeField kept, NodeField dropped, FieldHolder holder)
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
            WriteFields(planner => planner.Merge(holder, kept, dropped, value), "Fields merged", holder: holder);
        }
    }

    private void AddField_Click(object sender, RoutedEventArgs e)
    {
        _ownFieldNode = Node.Id;
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
            ShowOwnTab();
            CloseOwnField();
            Show();
        }
    }

    private void CloseOwnField()
    {
        ResetOwnField();
        Focus();
    }

    private void ResetOwnField()
    {
        _ownFieldNode = null;
        OwnFieldName.Clear();
        OwnFieldValue.Clear();
        OwnFieldForm.Visibility = Visibility.Collapsed;
        AddFieldButton.Visibility = WriteChanges is null ? Visibility.Collapsed : Visibility.Visible;
        HideStatus();
    }
}
