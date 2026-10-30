using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.Phone.Views;

/// <summary>
/// ＋ > Add a node…: the creation screen with its name to type, as a view's New on Windows: in a
/// list, of the list's type; in a tree, under its root; on the search tab, of the type chosen there.
/// A list of datasets or views has none: a file is added with Files…, a view on Windows.
/// </summary>
public sealed partial class HomePage
{
    private SheetChoice? AddNodeChoice()
    {
        if (Vault is not { } vault || FormOf(vault.Graph) is not { } form)
        {
            return null;
        }

        return new SheetChoice("Add a node…", () =>
        {
            _shell.Services.Log.Info("Action", "Creation screen shown for a new node");
            _shell.Push(new CreationPage(_shell, form, added: () => { }));
        });
    }

    private CreationForm? FormOf(KnowledgeGraph graph)
    {
        if (_tab.View is not { } view)
        {
            return CreationForm.NewNode(graph);
        }

        return new ViewPlanner(graph).Load(view).Definition switch
        {
            FlatDefinition definition when graph.Types.FindNodeType(definition.NodeType) is { } type && type != TypeCatalog.Dataset && type != TypeCatalog.View
                => CreationForm.NewNode(graph, type),
            TreeDefinition definition when new CoreTreeView(graph, definition).Root is { } root => CreationForm.NewNode(graph, under: new ParentLink(root.Node, definition.LinkType)),
            _ => null,
        };
    }
}
