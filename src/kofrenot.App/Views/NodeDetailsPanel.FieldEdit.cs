using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A field typed in a box: marked once changed, written by Enter or when the box is left, put back
/// by Esc; empty clears it. A value changed is never lost: it is written when the details are left
/// (<see cref="LeaveFields"/>), and kept in its box when they are shown again meanwhile.
/// </summary>
public partial class NodeDetailsPanel
{
    // The boxes of the fields shown, by owner and field.
    private readonly Dictionary<(string Owner, string Field), TextBox> _boxes = [];

    // The box typed in, the node and field it writes.
    private Typing? _typing;

    /// <summary>
    /// Writes the value changed in a field's box, before the details are left. One that does not
    /// fit its field is discarded when <paramref name="ask"/> and the user agrees, else kept in its
    /// box: false then, the details are not left.
    /// </summary>
    public bool LeaveFields(bool ask = true)
    {
        if (_typing is not { IsChanged: true } typing || Browser.Graph is not { } graph)
        {
            return true;
        }

        var plan = new FieldPlanner(graph).Set(typing.Node, typing.Field, typing.Box.Text);
        if (plan.Problem != FieldProblem.Invalid)
        {
            // Written, or the same value typed otherwise ("1920-10-8"): shown as written either way.
            _typing = null;
            return !plan.CanApply || WriteFields(_ => plan, "Field set");
        }

        FieldStatus.Text = plan.Status;
        FieldStatus.Visibility = Visibility.Visible;
        if (!ask)
        {
            return false;
        }

        var owner = Window.GetWindow(this);
        if (MessageDialog.Confirm(owner, $"{typing.Field.Label}: {FieldStatus.Text}\n\nDiscard “{typing.Box.Text.Trim()}”?", "Discard", "Keep editing"))
        {
            typing.Box.Text = typing.Field.Value ?? string.Empty;
            FieldStatus.Visibility = Visibility.Collapsed;
            _typing = null;
            return true;
        }

        typing.Box.Focus();
        return false;
    }

    /// <summary>Leaves the details' note and fields, writing what changed, before the window is hidden or something else is shown.</summary>
    public void Leave()
    {
        LeaveFields(ask: false);
        LeaveNote();
    }

    private TextBox BoxOf(NodeField field, Thickness margin)
    {
        var box = new TextBox { Margin = margin, Text = field.Value ?? string.Empty, ToolTip = HintOf(field.Kind) };
        var node = Node;
        box.GotKeyboardFocus += (_, _) => _typing = new Typing(box, node, field);
        box.TextChanged += (_, _) => Mark(box, field);
        box.LostKeyboardFocus += (_, _) => WriteBox(box);
        _boxes[(field.Owner, field.Field)] = box;
        return box;
    }

    // A box changed from its field's value has the accent's border, and says how to keep it.
    private void Mark(TextBox box, NodeField field)
    {
        if (box.Text != (field.Value ?? string.Empty))
        {
            box.BorderBrush = (Brush)FindResource("AccentFillColorDefaultBrush");
            box.ToolTip = "Changed: Enter saves it, Esc puts it back";
        }
        else
        {
            box.ClearValue(Control.BorderBrushProperty);
            box.ToolTip = HintOf(field.Kind);
        }
    }

    private static string HintOf(FieldKind kind) => kind switch
    {
        FieldKind.Number => "A number, like 1234.5",
        FieldKind.Date => "A date, like 2026-10-27",
        FieldKind.Url => "An address, like https://example.com",
        FieldKind.Email => "An email address, like ann@example.com",
        FieldKind.Address => "A postal address, like 12 rue de la Paix, Paris",
        _ => "Its text",
    };

    // What is typed in a box of this node, kept while its fields are shown again (a change from elsewhere).
    private (string Owner, string Field, string Text, int Caret, bool HasFocus)? TypingKept(Node node)
    {
        var kept = _typing is { IsChanged: true } typing && typing.Node.Id == node.Id
            ? (typing.Field.Owner, typing.Field.Field, typing.Box.Text, typing.Box.CaretIndex, typing.Box.IsKeyboardFocusWithin)
            : ((string, string, string, int, bool)?)null;
        _typing = null;
        return kept;
    }

    private void TypeBack((string Owner, string Field, string Text, int Caret, bool HasFocus)? kept)
    {
        if (kept is not { } typed || !_boxes.TryGetValue((typed.Owner, typed.Field), out var box) || Browser.Details is not { } details)
        {
            return;
        }

        box.Text = typed.Text;
        box.CaretIndex = Math.Min(typed.Caret, typed.Text.Length);
        _typing = new Typing(box, details.Node, details.Fields.First(field => (field.Owner, field.Field) == (typed.Owner, typed.Field)));
        if (typed.HasFocus)
        {
            box.Focus();
        }
    }

    /// <summary>Enter writes the field typed in, Esc puts its value back; the other keys type in it.</summary>
    private bool HandleFieldKey(Key key)
    {
        switch (key)
        {
            case Key.Enter when _typing is { } typing:
                WriteBox(typing.Box);
                return true;
            case Key.Enter when OwnFieldForm.IsKeyboardFocusWithin:
                AddOwnField();
                return true;
            case Key.Escape when OwnFieldForm.IsKeyboardFocusWithin:
                CloseOwnField();
                return true;
            case Key.Escape when _typing is { } typing:
                typing.Box.Text = typing.Field.Value ?? string.Empty;
                _typing = null;
                FieldStatus.Visibility = Visibility.Collapsed;
                Focus();
                return true;
            default:
                return false;
        }
    }

    // Writes the box's value when it changed; one that does not fit stays typed, its status shown.
    private void WriteBox(TextBox box)
    {
        if (_typing is { IsChanged: true } typing && typing.Box == box)
        {
            _typing = null;
            if (!WriteFields(planner => planner.Set(typing.Node, typing.Field, box.Text), "Field set"))
            {
                _typing = typing;
            }
        }
    }

    private sealed record Typing(TextBox Box, Node Node, NodeField Field)
    {
        public bool IsChanged => Box.Text != (Field.Value ?? string.Empty);
    }
}
