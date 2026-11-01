using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A search result's right-click menu: Delete… its node with its links, asked first; none in a vault read only.</summary>
public partial class MainWindow
{
    private void Result_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ResultRow { Node: { } node } row } frame || _session.Current is not { } vault || _session.IsReadOnly)
        {
            return;
        }

        Presented(_input.Select(row));
        var delete = new MenuItem { Header = "Delete…" };
        delete.Click += (_, _) =>
        {
            NodeDeleter.Delete(this, vault.Graph, node, (change, action) => _session.Write(Ask, change, action), _log);
            ShowInput();
            InputBox.Focus();
        };
        var menu = new ContextMenu { PlacementTarget = frame };
        menu.Items.Add(delete);
        menu.IsOpen = true;
        e.Handled = true;
    }
}
