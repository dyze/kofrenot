using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

public enum ViewDisplay
{
    Tree,
    Flat,
}

/// <summary>
/// A view's definition as edited (on the creation screen, or later): a tree (from <see cref="Root"/>,
/// through <see cref="LinkType"/>), or a list (of <see cref="NodeType"/>). A tree needs its root.
/// </summary>
public sealed record ViewDraft(ViewDisplay Display, Node? Root, LinkType LinkType, NodeType NodeType)
{
    public const int RootChoices = 8;

    public static ViewDraft Default { get; } = new(ViewDisplay.Tree, Root: null, TypeCatalog.Contains, TypeCatalog.Note);

    /// <summary>The definition, or null while a tree has no root.</summary>
    public ViewDefinition? Definition => Display switch
    {
        ViewDisplay.Tree => Root is null ? null : new TreeDefinition(Root.Id, LinkType.Id),
        _ => new FlatDefinition(NodeType.Id),
    };

    /// <summary>
    /// A saved definition, to edit it: a root removed (or not synced yet) is to choose again; a type
    /// this version does not know is kept by its id.
    /// </summary>
    public static ViewDraft Of(ViewDefinition definition, KnowledgeGraph graph) => definition switch
    {
        TreeDefinition tree => Default with
        {
            Root = graph.FindNode(tree.Root),
            LinkType = graph.Types.FindLinkType(tree.LinkType) ?? new LinkType(tree.LinkType, [tree.LinkType]),
        },
        FlatDefinition flat => Default with
        {
            Display = ViewDisplay.Flat,
            NodeType = graph.Types.FindNodeType(flat.NodeType) ?? new NodeType(flat.NodeType, flat.NodeType),
        },
        _ => Default,
    };

    /// <summary>The nodes a tree can start from, found by name as in the input box.</summary>
    public static IReadOnlyList<SearchHit> FindRoots(KnowledgeGraph graph, string text) => GraphSearch.Find(graph, text, RootChoices);

    /// <summary>What a view shows, in a sentence: "A tree from “Projects” through “contains”", "A list of every Note".</summary>
    public static string Describe(ViewDefinition? definition, KnowledgeGraph graph) => definition switch
    {
        TreeDefinition tree => graph.FindNode(tree.Root) is { } root
            ? $"A tree from “{root.Name}” through “{graph.Types.FindLinkType(tree.LinkType)?.Name ?? tree.LinkType}”"
            : "A tree from a node not here: removed, or not synced yet",
        FlatDefinition flat => $"A list of every {graph.Types.NodeTypeName(flat.NodeType)}",
        _ => "No definition yet",
    };
}
