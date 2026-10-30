using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node's fields and types on the phone: a tap on a field fills it (a text typed, a checkbox
/// ticked, an option chosen from a sheet); ⋮ adds a field of its own, adds a type or takes one out (asked first).
/// A name, colour or value set on two devices offers each to keep. An image is set on Windows.
/// </summary>
public sealed partial class NodePage
{
    private void Field_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FieldLine { Field: { } field } && !_shell.Services.Session.IsReadOnly)
        {
            _shell.Run(() => FillAsync(field));
        }
    }

    private async Task FillAsync(NodeField field)
    {
        if (Vault is not { } vault || _browser.Details?.Node is not { } node)
        {
            return;
        }

        var planner = new FieldPlanner(vault.Graph);
        switch (field.Kind)
        {
            case FieldKind.Image:
                _shell.Tell("An image is set in kofrenot on Windows.");
                break;
            case FieldKind.Checkbox:
                WriteField(planner.Check(node, field, !FieldValue.IsChecked(field.Value)), "Field value set");
                break;
            case FieldKind.Choice:
                _shell.ShowSheet(field.Label, field.Choices
                    .Select(choice => new SheetChoice(choice, () => WriteField(planner.Set(node, field, choice), "Field value set"), IsCurrent: choice == field.Value))
                    .Append(new SheetChoice("None", () => WriteField(planner.Clear(node, field), "Field value cleared"), IsCurrent: !field.HasValue)));
                break;
            default:
                var typed = await _shell.AskText(field.Label, field.Value ?? string.Empty, "Save", text => planner.Set(node, field, text).Error, text => planner.Set(node, field, text).Problem != FieldProblem.Invalid);
                if (typed is not null)
                {
                    WriteField(planner.Set(node, field, typed), typed.Trim().Length == 0 ? "Field value cleared" : "Field value set");
                }

                break;
        }
    }

    private async Task AddFieldAsync()
    {
        if (Vault is not { } vault || _browser.Details?.Node is not { } node)
        {
            return;
        }

        var planner = new FieldPlanner(vault.Graph);
        var name = await _shell.AskText("A field of its own: its name", string.Empty, "Next", text => NameRemark(planner.AddOwn(node, text, FieldKind.Text, "-")), text => NameRemark(planner.AddOwn(node, text, FieldKind.Text, "-")) is null);
        if (name is null)
        {
            return;
        }

        var value = await _shell.AskText($"{name.Trim()}: its value", string.Empty, "Add", text => planner.AddOwn(node, name, FieldKind.Text, text).Error, text => planner.AddOwn(node, name, FieldKind.Text, text).CanApply);
        if (value is not null)
        {
            WriteField(planner.AddOwn(node, name, FieldKind.Text, value), "Field of its own added");
        }
    }

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
        return new SheetChoice("Add type…", () => _shell.ShowSheet("Add type", types), IsEnabled: types.Count > 0);
    }

    // ⋮ > Take out type: asked first, saying what it hides.
    private SheetChoice RemoveTypeChoice(KnowledgeGraph graph, NodeDetails details)
    {
        var types = details.Types
            .Where(type => type != TypeCatalog.Generic)
            .Select(type => new SheetChoice(type.Name, () => _shell.Run(() => RemoveTypeAsync(new TypeSetPlanner(graph).Remove(details.Node, type)))))
            .ToList();
        return new SheetChoice("Take out type…", () => _shell.ShowSheet("Take out type", types), IsEnabled: types.Count > 0);
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
        Conflicts.Children.Add(new TextBlock { Text = text, Classes = { "error" } });
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
}
