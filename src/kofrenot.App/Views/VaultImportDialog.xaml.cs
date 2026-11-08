using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A vault of the vault box: another registered one, by its name.</summary>
public sealed record OtherVaultRow(RegisteredVault Vault, string Name);

/// <summary>A link type of the other vault, to import each node's subtree by.</summary>
public sealed record SubtreeTypeRow(string Id, string Name);

/// <summary>What to import from another vault: the nodes its query found there, with their subtree by a link type or not.</summary>
public sealed record VaultImportRequest(string VaultName, Vault Source, string Query, IReadOnlyList<Node> Hits, string? SubtreeLinkType);

/// <summary>
/// Imports › From a vault…: another vault read (<see cref="VaultSession.VaultOf"/>, its pass phrase
/// asked when not remembered), a query typed on it with the input box's completion
/// (<see cref="NodeQuerySearch"/>, all its nodes kept), and the subtree option. Writes nothing.
/// </summary>
public partial class VaultImportDialog : Window
{
    private readonly VaultSession _session;
    private Vault? _source;
    private IReadOnlyList<Node> _hits = [];

    private VaultImportDialog(Window owner, VaultSession session)
    {
        InitializeComponent();
        Owner = owner;
        _session = session;
        VaultBox.ItemsSource = session.Vaults
            .Where(vault => vault.Location != session.Selected?.Location)
            .Select(vault => new OtherVaultRow(vault, session.NameOf(vault)))
            .ToList();
        QueryBox.GraphOf = () => _source?.Graph;
        QueryBox.Changed += RunQuery;
        RunQuery();
    }

    /// <summary>What to import; null when cancelled.</summary>
    public static VaultImportRequest? Ask(Window owner, VaultSession session)
    {
        var dialog = new VaultImportDialog(owner, session);
        if (dialog.ShowDialog() != true || dialog._source is not { } source)
        {
            return null;
        }

        var subtree = dialog.SubtreeBox.IsChecked == true ? (dialog.LinkTypeBox.SelectedItem as SubtreeTypeRow)?.Id : null;
        return new VaultImportRequest(((OtherVaultRow)dialog.VaultBox.SelectedItem).Name, source, dialog.QueryBox.Input.Text.Trim(), dialog._hits, subtree);
    }

    // Another vault chosen: read (asking its pass phrase), its link types offered for the subtree.
    private void VaultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _source = VaultBox.SelectedItem is OtherVaultRow row ? _session.VaultOf(new WpfQuestions(this), row.Vault) : null;
        var types = _source?.Graph.Types.LinkTypes.Select(type => new SubtreeTypeRow(type.Id, type.Name)).ToList() ?? [];
        LinkTypeBox.ItemsSource = types;
        LinkTypeBox.SelectedItem = types.FirstOrDefault(type => type.Id == TypeCatalog.Contains.Id);
        QueryBox.Clear();
        RunQuery();
        QueryBox.FocusText();
    }

    // The nodes the query finds there, counted under the box.
    private void RunQuery()
    {
        var text = QueryBox.Input.Text;
        var query = _source is { } source && QueryParser.IsQuery(text) ? QueryParser.Parse(text, source.Graph.Types) : null;
        var result = query is { IsEmpty: false } ? NodeQuerySearch.Find(_source!.Graph, query, [], DateTimeOffset.UtcNow, TimeZoneInfo.Local, int.MaxValue) : null;
        _hits = result?.Problem is null ? result?.Hits.Select(hit => hit.Node).ToList() ?? [] : [];
        CountText.Text = _source is null ? "Choose a vault first."
            : result is null ? QueryCompletion.Hint
            : result.Problem ?? result.Total switch
            {
                0 => "No node found.",
                1 => "1 node found.",
                _ => $"{result.Total} nodes found.",
            };
        PreviewButton.IsEnabled = _hits.Count > 0;
    }

    // Tab or Enter accepts the proposal, Up/Down move in the proposal list; Enter without one previews.
    private void QueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = e.Key switch
        {
            Key.Tab => QueryBox.AcceptProposal(),
            Key.Up or Key.Down when QueryBox.HasProposalList => QueryBox.MoveProposal(e.Key == Key.Up ? -1 : 1),
            Key.Enter when QueryBox.HasProposalList => QueryBox.AcceptProposal(),
            _ => false,
        };
    }

    private void Preview_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
