using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>A tab in the tab strip, and whether it is the selected one.</summary>
public sealed record TabRow(Tab Tab, bool IsSelected)
{
    public string Name => Tab.Name;
}

/// <summary>
/// The tabs: the search, then the views open on this device. A view's tab shows its tree or list
/// instead of the search; the details opened from it go back to it.
/// </summary>
public partial class MainWindow
{
    private Guid? _tabVault;
    private Guid? _selectedView;

    /// <summary>The view of the selected tab, or null for the search (also when that view is gone).</summary>
    private Node? SelectedView(Vault vault)
    {
        if (_tabVault != vault.Id)
        {
            _tabVault = vault.Id;
            _selectedView = null;
        }

        return _tabList.Of(vault.Graph, vault.Id).FirstOrDefault(tab => tab.View?.Id == _selectedView)?.View;
    }

    private void ShowTabs(Vault vault, Node? selected)
    {
        TabsList.ItemsSource = _tabList.Of(vault.Graph, vault.Id).Select(tab => new TabRow(tab, tab.View?.Id == selected?.Id)).ToList();
        DetailsPanel.HomeLabel = $"⌂ {selected?.Name ?? Tab.Search.Name}";
        if (selected is not null && !DetailsShown)
        {
            ViewContent.Show(vault.Id, vault.Graph, selected);
        }
    }

    /// <summary>Selects a tab: the details open, if any, are left first.</summary>
    private void SelectTab(Node? view)
    {
        if (DetailsShown)
        {
            DetailsPanel.LeaveNote();
            DetailsPanel.Browser.Close();
            DetailsPanel.Show();
        }

        _selectedView = view?.Id;
        _log.Info("Action", view is null ? "Search tab selected" : "View tab selected");
        ShowVault();
        FocusTab();
    }

    /// <summary>The keyboard to the selected tab: the input box, or the view.</summary>
    private void FocusTab()
    {
        if (ViewContent.IsVisible)
        {
            ViewContent.FocusContent();
        }
        else
        {
            InputBox.Focus();
        }
    }

    /// <summary>Opens a view as the last tab (unless open already), and selects it.</summary>
    private void OpenTab(Node view)
    {
        if (_session.Current is { } vault)
        {
            _tabs.Open(vault.Id, view.Id);
            _log.Info("Action", "View tab opened");
            SelectTab(view);
        }
    }

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TabRow row })
        {
            SelectTab(row.Tab.View);
            e.Handled = true;
        }
    }

    /// <summary>⚙ on the selected view's tab: edits what it shows.</summary>
    private void EditTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TabRow { Tab.View: { } view } })
        {
            _session.EditView(this, view);
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TabRow { Tab.View: { } view } row } || _session.Current is not { } vault)
        {
            return;
        }

        _tabs.Close(vault.Id, view.Id);
        _log.Info("Action", "View tab closed");
        if (row.IsSelected)
        {
            SelectTab(null);
        }
        else
        {
            ShowVault();
        }
    }

    /// <summary>+ lists the views not open yet, then New view…: choosing one opens it in a new tab.</summary>
    private void AddTab_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Current is not { } vault)
        {
            return;
        }

        // The window's font: the button's is the icon font, which the menu would inherit.
        var menu = new ContextMenu { PlacementTarget = AddTabButton, FontFamily = FontFamily };
        foreach (var view in _tabList.Closed(vault.Graph, vault.Id))
        {
            var item = new MenuItem { Header = view.Name };
            item.Click += (_, _) => OpenTab(view);
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        var create = new MenuItem { Header = "New view…" };
        create.Click += (_, _) => CreateView();
        menu.Items.Add(create);
        menu.IsOpen = true;
    }

    /// <summary>New view…: the creation screen for a view, its name typed there; it then opens in a tab.</summary>
    private void CreateView()
    {
        _log.Info("Action", "New view");
        if (RecordRecent(_session.CreateNode(this, TypeCatalog.View)) is { } added && added.NewNodes.FirstOrDefault() is { } view)
        {
            OpenTab(view);
        }
    }

    private void ViewContent_OpenNode(Node node) => OpenDetails(node, from: null);
}
