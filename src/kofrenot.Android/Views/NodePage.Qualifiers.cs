using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A link's sheet, a tap on a link with qualifiers: open its other node, each qualifier to fill as a field is
/// (a tap), one of the link's own to add, their history. A long press still removes the link (NodePage.Actions).
/// Its type's qualifiers are defined on Windows.
/// </summary>
public sealed partial class NodePage
{
    private void ShowLinkSheet(NodeRow row, Link link)
    {
        if (Vault is not { } vault)
        {
            return;
        }

        var canEdit = !_shell.Services.Session.IsReadOnly;
        var choices = new List<SheetChoice>();
        if (row.CanOpen)
        {
            choices.Add(new SheetChoice($"Open “{row.Node.Name}”", () => _shell.Push(new NodePage(_shell, row.Node)), Icon: "open"));
        }

        foreach (var field in NodeFields.OfLink(vault.Graph, link))
        {
            var shown = field.HasValue ? field.Display : "—";
            choices.Add(new SheetChoice($"{field.Label}: {shown}", () =>
            {
                if (canEdit)
                {
                    _shell.Run(() => FillAsync(field, link));
                }
            }));
        }

        if (canEdit)
        {
            choices.Add(new SheetChoice("Add a qualifier…", () => _shell.Run(() => AddFieldAsync(link)), Icon: "add"));
        }

        choices.Add(new SheetChoice("History", () => _shell.Push(new HistoryPage(_shell, link, $"{row.Detail} {row.Node.Name}")), Icon: "history"));
        _shell.Services.Log.Info("Action", "Link qualifiers shown");
        _shell.ShowSheet(row.Detail.Length > 0 ? $"{row.Detail} {row.Node.Name}" : row.Node.Name, choices);
    }
}
