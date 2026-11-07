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
    // The boxes of the fields shown, by holder (the node, or a link unfolded), owner and field, and the line under each saying what is wrong with it.
    private readonly Dictionary<(Guid Holder, string Owner, string Field), TextBox> _boxes = [];
    private readonly Dictionary<(Guid Holder, string Owner, string Field), TextBlock> _errors = [];

    // The box typed in, the node or link and field it writes.
    private Typing? _typing;

    /// <summary>
    /// Writes the value changed in a field's box, before the details are left. One that does not
    /// fit its field is discarded when <paramref name="ask"/> and the user agrees, else kept in its
    /// box: false then, the details are not left. A field of its own half added is asked about too.
    /// </summary>
    public bool LeaveFields(bool ask = true)
    {
        if (!LeaveOwnField(ask))
        {
            return false;
        }

        if (_typing is not { IsChanged: true } typing || Browser.Graph is not { } graph)
        {
            return true;
        }

        var plan = new FieldPlanner(graph).Set(typing.Holder, typing.Field, typing.Box.Text);
        if (plan.Problem != FieldProblem.Invalid)
        {
            // Written, or the same value typed otherwise ("1920-10-8"): shown as written either way.
            _typing = null;
            return !plan.CanApply || WriteFields(_ => plan, "Field set", holder: typing.Holder);
        }

        ShowStatus(plan.Status, typing.Field, typing.Holder);
        if (!ask)
        {
            return false;
        }

        var owner = Window.GetWindow(this);
        if (MessageDialog.Confirm(owner, $"{typing.Field.Label}: {plan.Status}\n\nDiscard “{typing.Box.Text.Trim()}”?", "Discard", "Keep editing"))
        {
            typing.Box.Text = typing.Field.Value ?? string.Empty;
            HideStatus();
            _typing = null;
            return true;
        }

        typing.Box.Focus();
        return false;
    }

    // Add field… with a name or a value typed: discarded when the user agrees; kept while the window is only hidden.
    private bool LeaveOwnField(bool ask)
    {
        if (OwnFieldForm.Visibility != Visibility.Visible || !ask)
        {
            return true;
        }

        var name = OwnFieldName.Text.Trim();
        if ((name.Length > 0 || OwnFieldValue.Text.Trim().Length > 0)
            && !MessageDialog.Confirm(Window.GetWindow(this), $"Discard the field {(name.Length > 0 ? $"“{name}” " : string.Empty)}being added?", "Discard", "Keep editing"))
        {
            OwnFieldName.Focus();
            return false;
        }

        CloseOwnField();
        return true;
    }

    /// <summary>What is wrong, under the field it is about (of <paramref name="holder"/>, the node by default) when it has a box; else under the fields.</summary>
    private void ShowStatus(string status, NodeField? about = null, FieldHolder? holder = null)
    {
        HideStatus();
        var holderId = holder?.Id ?? Browser.Details?.Node.Id;
        var line = about is not null && holderId is { } id && _errors.TryGetValue((id, about.Owner, about.Field), out var error) ? error : FieldStatus;
        line.Text = status;
        line.Visibility = status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HideStatus()
    {
        FieldStatus.Visibility = Visibility.Collapsed;
        foreach (var error in _errors.Values)
        {
            error.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Leaves the details, writing what changed: the name being typed, the fields, the note. When <paramref name="ask"/>
    /// (another tab, node or level shown), a name or a value that cannot be written is discarded if the user agrees,
    /// else kept, and false: the details are not left. Without asking (the window hidden, a vault switched, quit),
    /// what can be written is, the rest kept in its box.
    /// </summary>
    public bool Leave(bool ask)
    {
        if (ask)
        {
            if (!LeaveName(ask: true) || !LeaveFields(ask: true))
            {
                return false;
            }
        }
        else
        {
            LeaveName(ask: false);
            LeaveFields(ask: false);
        }

        LeaveNote();
        return true;
    }

    // The box, and under it what is wrong with what is typed (ShowStatus).
    private StackPanel BoxOf(NodeField field, Thickness margin, FieldHolder holder)
    {
        var box = new TextBox { Text = field.Value ?? string.Empty, ToolTip = HintOf(field.Kind) };
        var error = new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = (Brush)FindResource("SystemFillColorCautionBrush"),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        box.GotKeyboardFocus += (_, _) =>
        {
            _typing = new Typing(box, holder, field);
            // Reached with the keyboard: at the end of its value, as every box; a click puts the caret where it is.
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                box.CaretIndex = box.Text.Length;
            }
        };
        box.TextChanged += (_, _) => Mark(box, field);
        box.LostKeyboardFocus += (_, _) => WriteBox(box);
        _boxes[(holder.Id, field.Owner, field.Field)] = box;
        _errors[(holder.Id, field.Owner, field.Field)] = error;
        return new StackPanel { Margin = margin, Children = { box, error } };
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
        FieldKind.Date => "A date, like 2026-10-27; or only its month (2026-10) or its year (2026)",
        FieldKind.Url => "An address, like https://example.com",
        FieldKind.Email => "An email address, like ann@example.com",
        FieldKind.Address => "A postal address, like 12 rue de la Paix, Paris",
        FieldKind.Phone => "A phone number, like +33 6 12 34 56 78",
        _ => "Its text",
    };

    // What is typed in a box of this node or of one of its links, kept while its fields are shown again (a change from elsewhere).
    private (FieldHolder Holder, string Owner, string Field, string Text, int Caret, bool HasFocus)? TypingKept(NodeDetails details)
    {
        var kept = _typing is { IsChanged: true } typing && (typing.Holder.Id == details.Node.Id || details.Links.Any(row => row.Link.Id == typing.Holder.Id))
            ? (typing.Holder, typing.Field.Owner, typing.Field.Field, typing.Box.Text, typing.Box.CaretIndex, typing.Box.IsKeyboardFocusWithin)
            : ((FieldHolder, string, string, string, int, bool)?)null;
        _typing = null;
        return kept;
    }

    private void TypeBack((FieldHolder Holder, string Owner, string Field, string Text, int Caret, bool HasFocus)? kept)
    {
        if (kept is not { } typed || !_boxes.TryGetValue((typed.Holder.Id, typed.Owner, typed.Field), out var box) || Browser.Graph is not { } graph
            || NodeFields.Of(graph, typed.Holder).FirstOrDefault(field => (field.Owner, field.Field) == (typed.Owner, typed.Field)) is not { } shown)
        {
            return;
        }

        box.Text = typed.Text;
        box.CaretIndex = Math.Min(typed.Caret, typed.Text.Length);
        _typing = new Typing(box, typed.Holder, shown);
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
            case Key.Enter when _typing is { Box.IsKeyboardFocusWithin: true } typing:
                WriteBox(typing.Box);
                return true;
            case Key.Enter when OwnFieldForm.IsKeyboardFocusWithin:
                AddOwnField();
                return true;
            case Key.Escape when OwnFieldForm.IsKeyboardFocusWithin:
                CloseOwnField();
                return true;
            case Key.Escape when _typing is { Box.IsKeyboardFocusWithin: true } typing:
                typing.Box.Text = typing.Field.Value ?? string.Empty;
                _typing = null;
                HideStatus();
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
            if (!WriteFields(planner => planner.Set(typing.Holder, typing.Field, box.Text), "Field set", typing.Field, typing.Holder))
            {
                _typing = typing;
            }
        }
    }

    private sealed record Typing(TextBox Box, FieldHolder Holder, NodeField Field)
    {
        public bool IsChanged => Box.Text != (Field.Value ?? string.Empty);
    }
}
