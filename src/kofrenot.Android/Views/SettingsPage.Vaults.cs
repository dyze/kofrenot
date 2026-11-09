using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Settings > Vault's list of the vaults on the phone (<see cref="VaultList"/>), and Settings > Node types and
/// Link types: the open vault's types, read only (<see cref="TypeList"/>), as they are changed on Windows.
/// </summary>
public sealed partial class SettingsPage
{
    private void AddVault_Click(object? sender, RoutedEventArgs e) => _shell.Push(new VaultsPage(_shell, adding: true));

    // The vault open removed: no vault to come back to, the vaults shown instead.
    private void VaultRemoved(bool wasOpen)
    {
        if (wasOpen)
        {
            _shell.Show(new VaultsPage(_shell));
        }
        else
        {
            Refresh();
        }
    }

    private void ShowTypes()
    {
        var graph = _shell.Services.Session.Current?.Graph;
        NodeTypes.ItemsSource = graph is null ? null : TypeList.NodeTypes(graph);
        LinkTypes.ItemsSource = graph is null ? null : TypeList.LinkTypes(graph);
    }

    private static string CountOf(ItemsControl types, string what) =>
        types.ItemCount switch
        {
            0 => "No vault open",
            1 => $"1 {what}",
            var count => $"{count} {what}s",
        };
}
