using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// The trash can, last of the header (Ctrl+Del): deletes the node shown with its links, asked first, then
/// shows the level before. A link row's × (or its right-click menu) removes that link. Hidden in a vault shared to read only.
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>True in a vault shared with the user to read only: nothing is deleted.</summary>
    public Func<bool> IsReadOnly { get; set; } = () => false;

    private void ShowDelete()
    {
        var readOnly = IsReadOnly();
        DeleteButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        LinksList.Tag = readOnly; // Hides the × of the link rows (their template).
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e) => DeleteNode();

    /// <summary>Ctrl+Del, unless a text box has the focus (there it deletes a word).</summary>
    public bool HandleDeleteKey() => Keyboard.FocusedElement is not TextBoxBase && DeleteNode();

    private bool DeleteNode()
    {
        if (IsReadOnly() || Browser is not { Graph: { } graph, Details: { } details } || Write is null || !LeaveFields())
        {
            return false;
        }

        if (NodeDeleter.Delete(Window.GetWindow(this), graph, details.Node, Write, Log))
        {
            Show(); // Gone: closes its note or file, unsaved changes dropped, before going back.
            GoBackTo(Browser.Depth - 1);
        }

        return true;
    }

    private void RemoveLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkRow row })
        {
            RemoveLink(row.Link);
        }

        e.Handled = true;
    }

    private void RemoveLink(Link link)
    {
        if (IsReadOnly() || Browser is not { Graph: { } graph, Details: { } details } || Write is null)
        {
            return;
        }

        var removal = new DeletionPlanner(graph).Link(link, details.Node.Id);
        if (!MessageDialog.Confirm(Window.GetWindow(this), removal.Question, "Remove"))
        {
            Log.Info("Action", "Link removal cancelled");
            return;
        }

        if (Write(removal.Change, "Link removed"))
        {
            Show();
        }
    }
}
