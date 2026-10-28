using System.Windows;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>A view's details: what it shows, in a sentence, and Edit… to change it.</summary>
public partial class NodeDetailsPanel
{
    /// <summary>Edits what a view shows (a dialog, then the vault written).</summary>
    public Action<Node>? EditView { get; set; }

    private void ShowView(NodeDetails details)
    {
        var isView = details.Node.Type == TypeCatalog.View.Id && Browser.Graph is not null && ContentShown;
        ShowsLabel.Visibility = ShowsPanel.Visibility = isView ? Visibility.Visible : Visibility.Collapsed;
        if (isView)
        {
            var loaded = new ViewPlanner(Browser.Graph!).Load(details.Node);
            var conflict = loaded.IsConflict ? " (edited on two computers: Edit… to keep one)" : string.Empty;
            ShowsText.Text = ViewDraft.Describe(loaded.Definition, Browser.Graph!) + conflict;
        }
    }

    private void EditView_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Details is { } details)
        {
            EditView?.Invoke(details.Node);
        }
    }
}
