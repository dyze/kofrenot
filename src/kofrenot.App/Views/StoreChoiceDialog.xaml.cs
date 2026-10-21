using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Server;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A row of the list: a store, by its id and size.</summary>
public sealed record StoreRow(ServerStore Store)
{
    public string Text => Store.Files == 0
        ? $"Vault {Store.Id.ToString()[..8]} · empty"
        : $"Vault {Store.Id.ToString()[..8]} · {ByteSize.Format(Store.Bytes)} in {Store.Files} files";
}

/// <summary>Chooses one of the server's vaults not listed here yet.</summary>
public partial class StoreChoiceDialog : Window
{
    private StoreChoiceDialog(Window owner, IReadOnlyList<ServerStore> stores)
    {
        InitializeComponent();
        Owner = owner;
        StoreList.ItemsSource = stores.Select(store => new StoreRow(store)).ToList();
        StoreList.SelectedIndex = 0;
        Loaded += (_, _) => StoreList.Focus();
    }

    /// <summary>The store chosen; null when cancelled.</summary>
    public static ServerStore? Ask(Window owner, IReadOnlyList<ServerStore> stores)
    {
        var dialog = new StoreChoiceDialog(owner, stores);
        return dialog.ShowDialog() == true ? (dialog.StoreList.SelectedItem as StoreRow)?.Store : null;
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
