using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// ⋯ on a node's page: rename, colour, pin or unpin, delete (asked first, naming what goes); a long
/// press on a link removes it (asked first). Each is a change written as on Windows, synced at the
/// next sync. Hidden in a vault shared with you to read.
/// </summary>
public sealed partial class NodePage
{
    private void ShowActions(NodeDetails details)
    {
        ActionsButton.IsVisible = !_shell.Services.Session.IsReadOnly;
        var colors = new MenuItem { Header = "Colour" };
        colors.ItemsSource = Enum.GetValues<NodeColor>().Select(color => ColorItem(color, details.Color)).ToList();
        ActionsButton.Flyout = new MenuFlyout
        {
            ItemsSource = new List<MenuItem>
            {
                Item("Rename…", () => _shell.Run(RenameAsync)),
                colors,
                Item(details.IsPinned ? "Unpin" : "Pin", TogglePin),
                Item("Delete…", () => _shell.Run(DeleteAsync)),
            },
        };
    }

    private static MenuItem Item(string header, Action click)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private MenuItem ColorItem(NodeColor color, NodeColor current)
    {
        var item = Item(color == NodeColor.None ? "None" : color.ToString(), () => GiveColor(color));
        item.Icon = new Ellipse { Width = 14, Height = 14, Fill = NodeLook.Brush(color), Stroke = NodeLook.Brush(NodeColor.Grey), StrokeThickness = color == NodeColor.None ? 1 : 0 };
        item.FontWeight = color == current ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal;
        return item;
    }

    private async Task RenameAsync()
    {
        if (Vault is not { } vault || _browser.Details?.Node is not { } node)
        {
            return;
        }

        var planner = new RenamePlanner(vault.Graph);
        var name = await _shell.AskText("Rename", _browser.Details.Name, "Rename", text => planner.Plan(node, text).Remark, text => planner.Plan(node, text).CanRename);
        if (name is not null && planner.Plan(node, name).Change is { } rename)
        {
            Write(answers => _shell.Services.Session.Rename(answers, rename));
        }
    }

    private void GiveColor(NodeColor color)
    {
        if (Vault is { } vault && _browser.Details?.Node is { } node && new ColorPlanner(vault.Graph).Plan(node, color) is { } change)
        {
            Write(answers => _shell.Services.Session.Write(answers, change, "Node colour changed"));
        }
    }

    private void TogglePin()
    {
        if (Vault is { } vault && _browser.Details is { } details)
        {
            Write(answers => _shell.Services.Session.Write(answers, new PinPlanner(vault.Graph).Toggle(details.Node), details.IsPinned ? "Node unpinned" : "Node pinned"));
        }
    }

    private async Task DeleteAsync()
    {
        if (Vault is not { } vault || _browser.Details?.Node is not { } node || AsksBeforeLeaving(() => _shell.Run(DeleteAsync)))
        {
            return;
        }

        var deletion = new DeletionPlanner(vault.Graph).Node(node);
        if (!await _shell.Confirm(deletion.Question, "Delete"))
        {
            _shell.Services.Log.Info("Action", "Node deletion cancelled");
            return;
        }

        if (Write(answers => _shell.Services.Session.Write(answers, deletion.Change, "Node deleted")) && !GoBack())
        {
            _shell.Back();
        }
    }

    private void RemoveLink_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is NodeRow { Link: { } link } row)
        {
            _shell.Run(() => RemoveLinkAsync(link, row));
        }
    }

    private async Task RemoveLinkAsync(Link link, NodeRow row)
    {
        if (Vault is not { } vault || _browser.Details is not { } details)
        {
            return;
        }

        if (!await _shell.Confirm($"Remove the link “{details.Name} {row.Detail} {row.Name}”?\n\nThe nodes stay.", "Remove"))
        {
            _shell.Services.Log.Info("Action", "Link removal cancelled");
            return;
        }

        Write(answers => _shell.Services.Session.Write(answers, new DeletionPlanner(vault.Graph).Link(link), "Link removed"));
    }

    // Written, what went wrong told, the page shown again.
    private bool Write(Func<PreparedAnswers, bool> write)
    {
        var answers = new PreparedAnswers();
        var written = write(answers);
        _shell.Tell(answers.Told);
        Refresh();
        return written;
    }
}
