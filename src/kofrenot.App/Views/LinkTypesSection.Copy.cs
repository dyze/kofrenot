using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>Settings > Link types > Copy from…: copies another vault's link types and aliases here (<see cref="LinkTypeCopyPlanner"/>).</summary>
public partial class LinkTypesSection
{
    private IReadOnlyList<LinkCopyLine>? _copies;

    private IReadOnlyList<RegisteredVault> OtherVaults =>
        _session is null ? [] : _session.Vaults.Where(vault => vault.Location != _session.Selected?.Location).ToList();

    // The list, or the copy panel in its place while another vault's link types are shown.
    private void ShowCopy(KnowledgeGraph? graph)
    {
        CopyFromButton.IsEnabled = graph is not null && OtherVaults.Count > 0;
        _copies = graph is null ? null : _copies;
        ListPanel.Visibility = _copies is null ? Visibility.Visible : Visibility.Collapsed;
        CopyPanel.Visibility = _copies is null ? Visibility.Collapsed : Visibility.Visible;
        CopyList.ItemsSource = _copies;
    }

    private void CopyFrom_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = CopyFromButton };
        foreach (var vault in OtherVaults)
        {
            var item = new MenuItem { Header = _session!.NameOf(vault) };
            item.Click += (_, _) => ReadTypes(vault);
            menu.Items.Add(item);
        }

        menu.IsOpen = menu.Items.Count > 0;
    }

    // Another vault's link types, read (its pass phrase asked when not remembered), each with what copying it does.
    private void ReadTypes(RegisteredVault vault)
    {
        var window = Window.GetWindow(this)!;
        if (_session is null || Graph is not { } graph || _session.GraphOf(new WpfQuestions(window), vault) is not { } source)
        {
            return;
        }

        var name = _session.NameOf(vault);
        var copies = new LinkTypeCopyPlanner(graph).Plan(source);
        if (copies.Count == 0)
        {
            MessageDialog.Inform(window, $"“{name}” has no link types, qualifiers or aliases of its own.");
            return;
        }

        CopyTitle.Text = $"Copy from “{name}”";
        _copies = copies.Select(copy => new LinkCopyLine(copy)).ToList();
        Show();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var changes = _copies?.Where(line => line.IsChecked).SelectMany(line => line.Copy.Changes).ToList() ?? [];
        if (_session is null || changes.Count == 0)
        {
            return;
        }

        var types = _copies!.Count(line => line.IsChecked && line.CanCopy);
        if (_session.Write(new WpfQuestions(Window.GetWindow(this)!), changes, $"Link types copied ({types}, {changes.Count} changes)"))
        {
            _copies = null;
        }

        Show();
    }

    private void CancelCopy_Click(object sender, RoutedEventArgs e)
    {
        _copies = null;
        Show();
    }
}
