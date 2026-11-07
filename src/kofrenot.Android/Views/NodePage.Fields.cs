using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node's fields and types on the phone: a tap on a field fills it (a text typed, a checkbox
/// ticked, an option chosen from a sheet, an image chosen: NodePage.FieldImage); ⋮ adds a field of its own, adds a type or takes one out (asked first).
/// A name, colour or value set on two devices offers each to keep.
/// </summary>
public sealed partial class NodePage
{
    private void Field_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FieldLine { Field: { } field } && !_shell.Services.Session.IsReadOnly)
        {
            _shell.Run(() => FillAsync(field, _browser.Details!.Node));
        }
    }

    /// <summary>Fills a field of the node, or a qualifier of one of its links (<paramref name="node"/> the link).</summary>
    private async Task FillAsync(NodeField field, FieldHolder node)
    {
        if (Vault is not { } vault)
        {
            return;
        }

        var planner = new FieldPlanner(vault.Graph);
        switch (field.Kind)
        {
            case FieldKind.Image:
                ShowImageChoices(planner, node, field);
                break;
            case FieldKind.Checkbox:
                WriteField(planner.Check(node, field, !FieldValue.IsChecked(field.Value)), "Field value set");
                break;
            case FieldKind.Choice:
                _shell.ShowSheet(field.Label, field.Choices
                    .Select(choice => new SheetChoice(choice, () => WriteField(planner.Set(node, field, choice), "Field value set"), IsCurrent: choice == field.Value))
                    .Append(new SheetChoice("None", () => WriteField(planner.Clear(node, field), "Field value cleared"), IsCurrent: !field.HasValue)));
                break;
            case FieldKind.Date:
                ShowCalendar(planner, node, field);
                break;
            default:
                await TypeValueAsync(planner, node, field);
                break;
        }
    }

    // A value typed; empty clears it.
    private async Task TypeValueAsync(FieldPlanner planner, FieldHolder node, NodeField field)
    {
        var typed = await _shell.AskText(field.Label, field.Value ?? string.Empty, "Save", text => planner.Set(node, field, text).Error, text => planner.Set(node, field, text).Problem != FieldProblem.Invalid);
        if (typed is not null)
        {
            WriteField(planner.Set(node, field, typed), typed.Trim().Length == 0 ? "Field value cleared" : "Field value set");
        }
    }

    // A calendar instead of a date typed, as on Windows; None clears it. Written as typed ones are: 2026-10-27.
    private void ShowCalendar(FieldPlanner planner, FieldHolder node, NodeField field)
    {
        var shown = FieldValue.FirstDayOf(field.Value)?.ToDateTime(TimeOnly.MinValue);
        var calendar = new Avalonia.Controls.Calendar { SelectedDate = shown, DisplayDate = shown ?? DateTime.Today, HorizontalAlignment = HorizontalAlignment.Center };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            _shell.CloseSheet();
            if (calendar.SelectedDate is { } chosen)
            {
                WriteField(planner.Set(node, field, chosen.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "Field value set");
            }
        };
        _shell.ShowSheet(field.Label, calendar,
        [
            new SheetChoice("Type it: a month or a year…", () => _shell.Run(() => TypeValueAsync(planner, node, field))),
            new SheetChoice("None", () => WriteField(planner.Clear(node, field), "Field value cleared"), IsCurrent: !field.HasValue),
        ]);
    }

    /// <summary>A field of the node's own, or a qualifier of a link's own (<paramref name="holder"/> the link).</summary>
    private async Task AddFieldAsync(FieldHolder? holder = null)
    {
        var target = holder ?? (_browser.Details?.Node is { } shown ? FieldHolder.OfNode(shown.Id) : (FieldHolder?)null);
        if (Vault is not { } vault || target is not { } node)
        {
            return;
        }

        var planner = new FieldPlanner(vault.Graph);
        var name = await _shell.AskText("A field of its own: its name", string.Empty, "Next", text => NameRemark(planner.AddOwn(node, text, FieldKind.Text, "-")), text => NameRemark(planner.AddOwn(node, text, FieldKind.Text, "-")) is null);
        if (name is null)
        {
            return;
        }

        // Its kind next, as on Windows; then its value, ticked or not for a checkbox.
        _shell.ShowSheet($"{name.Trim()}: its kind", FieldPlanner.OwnKinds.Select(kind => new SheetChoice(KindLabel(kind), kind == FieldKind.Checkbox
            ? () => AskChecked(planner, node, name)
            : () => _shell.Run(() => AddFieldValueAsync(planner, node, name, kind)))));
    }

    private async Task AddFieldValueAsync(FieldPlanner planner, FieldHolder node, string name, FieldKind kind)
    {
        var value = await _shell.AskText($"{name.Trim()}: its value", string.Empty, "Add", text => planner.AddOwn(node, name, kind, text).Error, text => planner.AddOwn(node, name, kind, text).CanApply);
        if (value is not null)
        {
            WriteField(planner.AddOwn(node, name, kind, value), "Field of its own added");
        }
    }

    private void AskChecked(FieldPlanner planner, FieldHolder node, string name) =>
        _shell.ShowSheet($"{name.Trim()}: its value", new[] { true, false }.Select(isChecked => new SheetChoice(isChecked ? "Checked" : "Not checked",
            () => WriteField(planner.AddOwn(node, name, FieldKind.Checkbox, FieldValue.Of(isChecked)), "Field of its own added"))));

    private static string KindLabel(FieldKind kind) => kind switch
    {
        FieldKind.Url => "Web address",
        FieldKind.Email => "Email address",
        FieldKind.Address => "Postal address",
        FieldKind.Phone => "Phone number",
        _ => kind.ToString(),
    };

    // What is wrong with a field's name; its value is asked next.
    private static string? NameRemark(FieldPlan plan) =>
        plan.Problem is FieldProblem.NameEmpty or FieldProblem.NameTaken ? plan.Status : null;

    private void WriteField(FieldPlan plan, string action)
    {
        if (plan.CanApply)
        {
            Write(answers => _shell.Services.Session.Write(answers, plan.Changes, action));
        }
        else if (plan.Problem == FieldProblem.Invalid)
        {
            _shell.Tell(plan.Status);
        }
    }

    // ⋮ > Add type: the node types it has not, a view's excepted (what it shows is chosen on Windows).
    private SheetChoice AddTypeChoice(KnowledgeGraph graph, NodeDetails details)
    {
        var types = graph.Types.NodeTypes
            .Where(type => type != TypeCatalog.Generic && type != TypeCatalog.View && !details.Types.Contains(type))
            .Select(type => new SheetChoice(type.Name, () => ChangeTypes(new TypeSetPlanner(graph).Add(details.Node, type))))
            .ToList();
        return new SheetChoice("Add type…", () => _shell.ShowSheet("Add type", types), IsEnabled: types.Count > 0, Icon: "add");
    }

    // ⋮ > Take out type: asked first, saying what it hides.
    private SheetChoice RemoveTypeChoice(KnowledgeGraph graph, NodeDetails details)
    {
        var types = details.Types
            .Where(type => type != TypeCatalog.Generic)
            .Select(type => new SheetChoice(type.Name, () => _shell.Run(() => RemoveTypeAsync(new TypeSetPlanner(graph).Remove(details.Node, type)))))
            .ToList();
        return new SheetChoice("Take out type…", () => _shell.ShowSheet("Take out type", types), IsEnabled: types.Count > 0, Icon: "remove");
    }

    private async Task RemoveTypeAsync(TypeSetPlan plan)
    {
        var hidden = plan.Hidden is null ? string.Empty : $"\n\n{plan.Hidden}";
        if (await _shell.Confirm($"Take “{plan.Type.Name}” out of “{plan.Node.Name}”?\n\nThe values of its fields stay, as the node's own.{hidden}", "Take out"))
        {
            ChangeTypes(plan);
        }
        else
        {
            _shell.Services.Log.Info("Action", "Node type change cancelled");
        }
    }

    private void ChangeTypes(TypeSetPlan plan) =>
        Write(answers => _shell.Services.Session.ChangeTypes(answers, plan));

    // A name, a colour or a field's value set on two devices before they synced: each offered to keep.
    private void ShowConflicts(NodeDetails details)
    {
        Conflicts.Children.Clear();
        if (Vault?.Graph is not { } graph || _shell.Services.Session.IsReadOnly)
        {
            Conflicts.IsVisible = false;
            return;
        }

        var node = details.Node;
        if (details.NameConflict.Count > 1)
        {
            AddConflict("Named differently on two devices. Keep:", details.NameConflict.Select(name => (name, (Action)(() => Keep(new RenamePlanner(graph).Plan(node, name).Change, "Node renamed (a conflict resolved)")))));
        }

        if (details.ColorConflict.Count > 1)
        {
            AddConflict("Coloured differently on two devices. Keep:", details.ColorConflict.Select(color => (color.ToString(), (Action)(() => Keep(new ColorPlanner(graph).Plan(node, color), "Node colour changed (a conflict resolved)")))));
        }

        foreach (var field in details.Fields.Where(field => field.Values is { IsConflict: true }))
        {
            var versions = field.Values!.Current.Zip(field.Conflict);
            AddConflict($"{field.Label} set differently on two devices. Keep:", versions.Select(version => (version.Second, (Action)(() => WriteField(new FieldPlanner(graph).Keep(node, field, version.First), "Field value kept (a conflict resolved)")))));
        }

        Conflicts.IsVisible = Conflicts.Children.Count > 0;
    }

    private void AddConflict(string text, IEnumerable<(string Label, Action Keep)> choices)
    {
        var caution = new DockPanel();
        var alert = new PathIcon
        {
            Data = PhoneTheme.Icon("alert"),
            Foreground = PhoneTheme.Brush("Error"),
            Width = 16,
            Height = 16,
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        DockPanel.SetDock(alert, Dock.Left);
        caution.Children.Add(alert);
        caution.Children.Add(new TextBlock { Text = text, Classes = { "error" } });
        Conflicts.Children.Add(caution);
        var buttons = new WrapPanel();
        foreach (var (label, keep) in choices)
        {
            var button = new Button { Content = label, Margin = new Avalonia.Thickness(0, 4, 8, 0) };
            button.Click += (_, _) => keep();
            buttons.Children.Add(button);
        }

        Conflicts.Children.Add(buttons);
    }

    private void Keep(Change? change, string action)
    {
        if (change is not null)
        {
            Write(answers => _shell.Services.Session.Write(answers, change, action));
        }
    }

    // Handed to the app Android picks for it (a browser, a mail app, a map); the link is the user's, it is not logged.
    private void OpenLink_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FieldLine { Link: { } link })
        {
            _shell.Services.Log.Info("Action", "Field link opened");
            _shell.Services.Chooser.OpenLink(link);
        }
    }
}
