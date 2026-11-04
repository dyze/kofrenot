using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The files or the image copied, pasted in a graph view as datasets once their preview is accepted:
/// from the background's menu with no link, kept where the menu was opened, side by side (a
/// placement); from a card's menu, linked to it through one of the view's link types. Offered only
/// while the clipboard holds files (no folder) or an image, not in a vault read only.
/// </summary>
public partial class GraphPanel
{
    /// <summary>The system's clipboard, whose files or image can be pasted as datasets; null: not offered.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>Imports what was pasted as datasets, linked to a card or to none, once its preview is accepted; the datasets imported.</summary>
    public Func<Pasted, ParentLink?, Task<IReadOnlyList<Node>>>? ImportPasted { get; set; }

    private bool CanPaste => !IsReadOnly() && ImportPasted is not null && Clipboard is { } clipboard && PasteOffer.FitsFiles(clipboard.Content);

    private void AddPasteItem(ContextMenu menu, Point at)
    {
        if (CanPaste)
        {
            menu.Items.Add(MenuItems.Of("Paste files…", string.Empty, () => _ = PasteAt(at)));
        }
    }

    private void AddPasteItems(ContextMenu menu, Node card, IReadOnlyList<LinkVerb> verbs)
    {
        if (!CanPaste || verbs.Count == 0)
        {
            return;
        }

        var paste = new MenuItem { Header = "Paste files" };
        foreach (var verb in verbs)
        {
            paste.Items.Add(MenuItems.Of($"{card.Name} {verb.Name} …", string.Empty, () => _ = PasteLinkedTo(new ParentLink(card, verb.Type.Id, verb.IsReverse))));
        }

        menu.Items.Add(paste);
    }

    /// <summary>The datasets pasted with no link, placed side by side from where the menu was opened; then selected.</summary>
    private async Task PasteAt(Point at)
    {
        if (_view is not { } view || await Paste(under: null) is not { Count: > 0 } added || _graph is not { } graph || _definition is not { } definition)
        {
            return;
        }

        var shown = added.Where(node => definition.NodeTypes.Count == 0 || definition.NodeTypes.Any(kept => graph.HasType(node.Id, kept))).ToList();
        if (shown.Count < added.Count)
        {
            MessageDialog.Inform(Window.GetWindow(this), "The datasets are added, but this graph does not show them: their type is not one the view keeps.");
            return;
        }

        var step = GraphLayout.CardWidth + GraphLayout.ColumnGap;
        var positions = shown.Select((node, index) => new NodePosition(node.Id, at.X + (index * step), at.Y));
        if (new GraphPlacer(graph).Move(view.Id, positions) is { } placed && Write?.Invoke([placed], $"Nodes placed in a graph view ({shown.Count})") == true)
        {
            SelectOnly(shown.Select(node => node.Id));
        }
    }

    private async Task PasteLinkedTo(ParentLink under)
    {
        if (await Paste(under) is { Count: > 0 } added)
        {
            SelectOnly(added.Select(node => node.Id));
        }
    }

    private async Task<IReadOnlyList<Node>> Paste(ParentLink? under)
    {
        if (Clipboard is null || ImportPasted is null)
        {
            return [];
        }

        Log.Info("Action", under is null ? "Paste of files in a graph view" : "Paste of files linked to a card in a graph view");
        if (PasteOffer.Read(Clipboard) is not { } pasted)
        {
            MessageDialog.Inform(Window.GetWindow(this), "The clipboard holds no file nor image any more.");
            return [];
        }

        return await ImportPasted(pasted, under);
    }
}
