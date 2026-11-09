using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A row of the list: a store, by its name (else its id), whose it is, and its size.</summary>
public sealed record StoreRow(StoreChoice Choice)
{
    public string Text => $"{Choice.Title} · {Whose} · " + (Store.Files == 0
        ? "empty"
        : $"{ByteSize.Format(Store.Bytes)} in {Store.Files} files");

    private ServerStore Store => Choice.Store;

    private string Whose => Store.Role == StoreRole.Owner ? "yours" : $"shared with you, {Store.Role.ToString().ToLowerInvariant()}";
}

/// <summary>Chooses one of the server's vaults not listed here yet.</summary>
public partial class StoreChoiceDialog : Window
{
    private StoreChoiceDialog(Window owner, IReadOnlyList<StoreChoice> stores)
    {
        InitializeComponent();
        Owner = owner;
        StoreList.ItemsSource = stores.Select(store => new StoreRow(store)).ToList();
        StoreList.SelectedIndex = 0;
        Loaded += (_, _) => StoreList.Focus();
    }

    /// <summary>The store chosen; null when cancelled.</summary>
    public static StoreChoice? Ask(Window owner, IReadOnlyList<StoreChoice> stores)
    {
        var dialog = new StoreChoiceDialog(owner, stores);
        return dialog.ShowDialog() == true ? (dialog.StoreList.SelectedItem as StoreRow)?.Choice : null;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (StoreList.SelectedItem is not null)
        {
            DialogResult = true;
        }
    }

    private void StoreList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AddButton_Click(sender, e);

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
