using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The tabs: the search, then the views opened on this phone, within the screen's width, the selected
/// one kept in sight. ▾, always at their right, lists
/// them all in a sheet, to reach one among dozens, then the views to open. A view tab has its bar under
/// the tabs: how many nodes it shows, and ⋮ for its actions (Close view).
/// </summary>
public sealed partial class HomePage
{
    private void ShowTabs(Vault vault)
    {
        var tabs = new TabList(_shell.Services.ViewTabs).Of(vault.Graph, vault.Id);
        if (!tabs.Contains(_tab))
        {
            _tab = Tab.Search;
        }

        Tabs.Children.Clear();
        Button? selected = null;
        foreach (var tab in tabs)
        {
            var button = new Button { Content = tab.Name, Tag = tab, Classes = { "tab", tab == _tab ? "accent" : "flat" } };
            button.Click += (_, _) => Select(tab);
            Tabs.Children.Add(button);
            selected = tab == _tab ? button : selected;
        }

        ViewBar.IsVisible = !_tab.IsSearch;
        Dispatcher.UIThread.Post(() => selected?.BringIntoView(), DispatcherPriority.Loaded);
    }

    // ▾: every tab open, the current one marked; then the views not open as a tab, from a second sheet.
    private void AllTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is not { } vault)
        {
            return;
        }

        var tabs = new TabList(_shell.Services.ViewTabs).Of(vault.Graph, vault.Id);
        var closed = new TabList(_shell.Services.ViewTabs).Closed(vault.Graph, vault.Id);
        _shell.ShowSheet("Tabs",
        [
            .. tabs.Select(tab => new SheetChoice(tab.Name, () => Select(tab), IsCurrent: tab == _tab, Icon: tab.IsSearch ? "SearchIcon" : "ListIcon")),
            new("Open a view…", () => _shell.ShowSheet("Open a view", closed.Select(view => new SheetChoice(view.Name, () => OpenTab(vault, view), Icon: "ListIcon"))),
                IsEnabled: closed.Count > 0, Icon: "AddIcon"),
        ]);
    }

    private void ShowViewBar(int count) => ViewCount.Text = count == 1 ? "1 row" : $"{count} rows";

    // ⋮ of a view tab: its actions, only Close view for now.
    private void ViewActions_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is { } vault && _tab.View is { } view)
        {
            _shell.ShowSheet(_tab.Name, [new("Close view", () => CloseTab(vault, view), Icon: "CloseIcon")]);
        }
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
