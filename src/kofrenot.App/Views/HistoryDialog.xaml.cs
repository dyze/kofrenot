using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.History;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A row of the history: a version of the node.</summary>
public sealed record HistoryRow(HistoryEntry Entry)
{
    public string When => Entry.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);

    public string What => Entry.What;

    public string Label => Entry.Label;

    public bool CanRestore => Entry.CanRestore;

    public bool IsOnlyCurrent => Entry.IsCurrent && !Entry.CanRestore;
}

/// <summary>Lists a node's versions (<see cref="NodeHistory"/>); Restore closes it with the version chosen.</summary>
public partial class HistoryDialog : Window
{
    private HistoryEntry? _chosen;

    private HistoryDialog(Window owner, string nodeName, IReadOnlyList<HistoryEntry> entries)
    {
        InitializeComponent();
        Owner = owner;
        HeadingText.Text = $"History of “{nodeName}”";
        RowsList.ItemsSource = entries.Select(entry => new HistoryRow(entry)).ToList();
    }

    /// <summary>The version chosen to restore; null when closed.</summary>
    public static HistoryEntry? Ask(Window owner, string nodeName, IReadOnlyList<HistoryEntry> entries)
    {
        var dialog = new HistoryDialog(owner, nodeName, entries);
        return dialog.ShowDialog() == true ? dialog._chosen : null;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HistoryRow row })
        {
            _chosen = row.Entry;
            DialogResult = true;
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
