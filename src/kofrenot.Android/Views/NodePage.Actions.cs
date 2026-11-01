using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// ⋮ on a node's page, a sheet: rename, colour, pin or unpin, add a field or a type, take a type out,
/// delete (asked first, naming what goes); a long press on a link removes it (asked first). Each is a
/// change written as on Windows, synced at the next sync. ⋮ > History… shows its versions on a screen
/// of their own (HistoryPage); in a vault shared with you to read, that is all ⋮ has.
/// </summary>
public sealed partial class NodePage
{
    private bool IsReadOnly => _shell.Services.Session.IsReadOnly;

    // A reader has the history only.
    private void Actions_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is not { } vault || _browser.Details is not { } details)
        {
            return;
        }

        // What a reader has too: the history, and keep offline (this phone only, never written to the vault).
        SheetChoice[] readers =
        [
            new("History…", ShowHistory, Icon: "history"),
            .. OfflineChoice(details.Node) is { } offline ? [offline] : Array.Empty<SheetChoice>(),
        ];
        if (IsReadOnly)
        {
            _shell.ShowSheet(details.Name, readers);
            return;
        }

        _shell.ShowSheet(details.Name,
        [
            .. readers,
            new("Rename…", () => _shell.Run(RenameAsync), Icon: "rename"),
            new("Colour…", () => ShowColors(details.Color), Icon: "colour"),
            new(details.IsPinned ? "Unpin" : "Pin", TogglePin, Icon: "pin"),
            new("Add field…", () => _shell.Run(AddFieldAsync), Icon: "add"),
            AddTypeChoice(vault.Graph, details),
            RemoveTypeChoice(vault.Graph, details),
            new("Delete…", () => _shell.Run(DeleteAsync), Icon: "delete"),
        ]);
    }

    // A tap on the name renames it, as on Windows; a reader only reads it.
    private void NodeName_Tapped(object? sender, TappedEventArgs e)
    {
        if (!IsReadOnly)
        {
            _shell.Run(RenameAsync);
        }
    }

    // A note changed and not saved: asked first, as a restore may change it.
    // The globe: its name searched by the phone's search app, else with the engine of the settings in the browser; a reader too.
    private void WebSearch_Click(object? sender, RoutedEventArgs e)
    {
        if (_browser.Details?.Node is { } node && WebSearch.UrlOf(_shell.Services.Settings.WebSearch, node.Name) is { } fallback)
        {
            _shell.Services.Log.Info("Action", "Name searched on the web");
            _shell.Services.Chooser.SearchWeb(node.Name, fallback);
        }
    }

    private void ShowHistory()
    {
        if (_browser.Details?.Node is { } node && !AsksBeforeLeaving(ShowHistory))
        {
            _shell.Push(new HistoryPage(_shell, node));
        }
    }

    // A dot of each colour; none, a clear one so the names line up.
    private void ShowColors(NodeColor current) =>
        _shell.ShowSheet("Colour", Enum.GetValues<NodeColor>().Select(color => new SheetChoice(
            color == NodeColor.None ? "None" : color.ToString(),
            () => GiveColor(color),
            NodeLook.Brush(color) ?? Brushes.Transparent,
            IsCurrent: color == current)));

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

        // Gone: the page shows the screen before (Refresh).
        Write(answers => _shell.Services.Session.Write(answers, deletion.Change, "Node deleted"));
    }

    private void Link_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (!IsReadOnly && (sender as Control)?.Tag is NodeRow { Link: { } link } row)
        {
            e.Handled = true;
            _shell.ShowSheet(row.Name, [new("Remove link…", () => _shell.Run(() => RemoveLinkAsync(link)), Icon: "unlink")]);
        }
    }

    private async Task RemoveLinkAsync(Link link)
    {
        if (Vault is not { } vault || _browser.Details is not { } details)
        {
            return;
        }

        var removal = new DeletionPlanner(vault.Graph).Link(link, details.Node.Id);
        if (!await _shell.Confirm(removal.Question, "Remove"))
        {
            _shell.Services.Log.Info("Action", "Link removal cancelled");
            return;
        }

        Write(answers => _shell.Services.Session.Write(answers, removal.Change, "Link removed"));
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
