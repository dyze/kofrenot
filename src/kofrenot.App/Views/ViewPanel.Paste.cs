using System.Windows.Controls;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// The files or the image copied, pasted in a view as datasets, once their preview is accepted:
/// in a tree under the node selected (Paste files here, in its menu), in a graph from its menus.
/// Offered only while the clipboard holds them, not in a vault read only.
/// </summary>
public partial class ViewPanel
{
    private IClipboard? _clipboard;

    /// <summary>The system's clipboard, whose files or image can be pasted as datasets; null: not offered.</summary>
    public IClipboard? Clipboard
    {
        get => _clipboard;
        set => _clipboard = GraphBoard.Clipboard = value;
    }

    /// <summary>Imports what was pasted as datasets, under a node or with no parent, once its preview is accepted; the datasets imported.</summary>
    public Func<Pasted, ParentLink?, Task<IReadOnlyList<Node>>>? ImportPasted { get; set; }

    private bool CanPaste(Func<ClipboardContent, bool> fits) =>
        !IsReadOnly() && ImportPasted is not null && _clipboard is { } clipboard && fits(clipboard.Content);

    private void AddPasteFilesItem(ContextMenu menu)
    {
        if (_definition is not null && SelectedItem is not null && CanPaste(PasteOffer.FitsImport))
        {
            menu.Items.Add(MenuItems.Of("Paste files here…", string.Empty, () => _ = PasteFilesUnder()));
        }
    }

    private async Task PasteFilesUnder()
    {
        if (_definition is not { } definition || SelectedItem is not { } onto || ImportPasted is null || _clipboard is null)
        {
            return;
        }

        Log.Info("Action", "Paste of files in a tree");
        if (PasteOffer.Read(_clipboard) is not { } pasted)
        {
            MessageDialog.Inform(System.Windows.Window.GetWindow(this), "The clipboard holds no file nor image any more.");
            return;
        }

        if ((await ImportPasted(pasted, new ParentLink(onto.Node, definition.LinkType))).Count > 0)
        {
            SetExpanded(onto.Key, isExpanded: true);
        }

        Refresh();
    }
}
