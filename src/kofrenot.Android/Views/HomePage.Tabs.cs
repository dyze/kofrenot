using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The tabs: the search, then the views opened on this phone, as many as fit whole, the selected one
/// always in sight (<see cref="TabStripPanel"/>), as on Windows. ▾, always at their right, lists the
/// tabs left out, then the views to open. A view tab has its bar under
/// the tabs: how many nodes it shows, and ⋮ for its actions (move it, a tree's unlinked nodes, Close view).
/// </summary>
public sealed partial class HomePage
{
    // Room between two tabs.
    private const double TabGap = 4;

    private void ShowTabs(Vault vault)
    {
        var tabs = new TabList(_shell.Services.ViewTabs).Of(vault.Graph, vault.Id);
        if (!tabs.Contains(_tab))
        {
            _tab = Tab.Search;
        }

        Tabs.Children.Clear();
        foreach (var tab in tabs)
        {
            var button = new Button { Content = tab.Name, Tag = tab, Margin = new Thickness(0, 0, TabGap, 0), Classes = { "tab", tab == _tab ? "accent" : "flat" } };
            button.Click += (_, _) => Select(tab);
            Tabs.Children.Add(button);
            if (tab == _tab)
            {
                Tabs.Selected = button;
            }
        }

        ViewBar.IsVisible = !_tab.IsSearch;
    }

    // ▾: the tabs left out of the strip; then the views not open as a tab, from a second sheet.
    private void AllTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is not { } vault)
        {
            return;
        }

        var hidden = Tabs.Hidden.Select(button => button.Tag).OfType<Tab>();
        var closed = new TabList(_shell.Services.ViewTabs).Closed(vault.Graph, vault.Id);
        _shell.ShowSheet("Tabs",
        [
            .. hidden.Select(tab => new SheetChoice(tab.Name, () => Select(tab), Icon: tab.View is { } view ? IconPaths.NameOf(NodeVisual.Of(vault.Graph, view)) : "home")),
            new("Open a view…", () => _shell.ShowSheet("Open a view", closed.Select(view => new SheetChoice(view.Name, () => OpenTab(vault, view), Icon: IconPaths.NameOf(NodeVisual.Of(vault.Graph, view))))),
                IsEnabled: closed.Count > 0, Icon: "add"),
        ]);
    }

    private void ShowViewBar(IReadOnlyList<object> rows)
    {
        var noun = rows is [CardRow, ..] ? "card" : "row";
        ViewCount.Text = rows.Count == 1 ? $"1 {noun}" : $"{rows.Count} {noun}s";
    }

    // ⋮ of a view tab: its actions: move it left or right among the view tabs (Home stays first),
    // as dragged on Windows; a tree's unlinked nodes, to move into it; Close view.
    private void ViewActions_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is { } vault && _tab.View is { } view)
        {
            var tabs = new TabList(_shell.Services.ViewTabs);
            var isTree = ImportDestinations.TreeOf(vault.Graph, view) is not null;
            _shell.ShowSheet(_tab.Name,
            [
                .. tabs.CanShift(vault.Graph, vault.Id, view, -1) ? [new SheetChoice("Move left", () => ShiftTab(vault, view, -1), Icon: "chevron-left")] : Array.Empty<SheetChoice>(),
                .. tabs.CanShift(vault.Graph, vault.Id, view, 1) ? [new SheetChoice("Move right", () => ShiftTab(vault, view, 1), Icon: "chevron-right")] : Array.Empty<SheetChoice>(),
                .. isTree ? [new SheetChoice("Unlinked nodes…", () => ShowUnlinked(view), Icon: "unlink")] : Array.Empty<SheetChoice>(),
                new("Close view", () => CloseTab(vault, view), Icon: "close"),
            ]);
        }
    }

    private void ShiftTab(Vault vault, Node view, int step)
    {
        _shell.Services.Log.Info("Action", step < 0 ? "View tab moved left" : "View tab moved right");
        new TabList(_shell.Services.ViewTabs).Shift(vault.Graph, vault.Id, view, step);
        ShowTabs(vault);
    }

    private void ShowUnlinked(Node view)
    {
        _shell.Services.Log.Info("Action", "Unlinked nodes shown");
        _shell.Push(new UnlinkedPage(_shell, view));
    }

    private void Select(Tab tab)
    {
        _tab = tab;
        ClearSearch();
        Refresh();
    }

    private void OpenTab(Vault vault, Node view)
    {
        _shell.Services.Log.Info("Action", "View opened as a tab");
        _shell.Services.ViewTabs.Open(vault.Id, view.Id);
        Select(new Tab(view));
    }

    // Only the tab goes, not the view: nothing to confirm.
    private void CloseTab(Vault vault, Node view)
    {
        _shell.Services.Log.Info("Action", "View tab closed");
        _shell.Services.ViewTabs.Close(vault.Id, view.Id);
        Select(Tab.Search);
    }
}
