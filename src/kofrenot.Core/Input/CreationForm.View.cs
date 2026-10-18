using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

public enum ViewDisplay
{
    Tree,
    Flat,
}

/// <summary>
/// A new view's definition as chosen on its step: a tree (from <see cref="Root"/>, through
/// <see cref="LinkType"/>), or a list (of <see cref="NodeType"/>). A tree needs its root.
/// </summary>
public sealed record ViewDraft(ViewDisplay Display, Node? Root, LinkType LinkType, NodeType NodeType)
{
    public static ViewDraft Default { get; } = new(ViewDisplay.Tree, Root: null, TypeCatalog.Contains, TypeCatalog.Note);

    /// <summary>The definition, or null while a tree has no root.</summary>
    public ViewDefinition? Definition => Display switch
    {
        ViewDisplay.Tree => Root is null ? null : new TreeDefinition(Root.Id, LinkType.Id),
        _ => new FlatDefinition(NodeType.Id),
    };
}

/// <summary>A new view's step: its definition, saved with the creation as its first version.</summary>
public sealed partial class CreationForm
{
    public const int RootChoices = 8;

    private readonly Dictionary<Side, ViewDraft> _views = new() { [Side.Left] = ViewDraft.Default, [Side.Right] = ViewDraft.Default };

    /// <summary>A new node of type view: its step has its definition to choose.</summary>
    public bool IsView(Side side) => TypeOf(side).Id == TypeCatalog.View.Id;

    public ViewDraft ViewOf(Side side) => _views[side];

    public void SetView(Side side, ViewDraft view) => _views[side] = view;

    /// <summary>The nodes a tree can start from, found by name as in the input box.</summary>
    public IReadOnlyList<SearchHit> FindRoots(string text) => GraphSearch.Find(_graph, text, RootChoices);

    /// <summary>False on a new view's step while its definition is not complete.</summary>
    public bool CanLeaveStep => Step.Side is not { } side || !IsView(side) || _views[side].Definition is not null;

    /// <summary>The plan can be added, and every new view has its definition.</summary>
    public bool CanAdd => Plan.CanAdd && NewSides.All(side => !IsView(side) || _views[side].Definition is not null);

    /// <summary>The first version of each new view, as <see cref="NotesToSave"/>.</summary>
    public IReadOnlyList<ViewEdited> ViewsToSave() =>
        NewNodes()
            .Where(pair => IsView(pair.Side) && _views[pair.Side].Definition is not null)
            .Select(pair => new ViewEdited(Guid.NewGuid(), _clock.GetUtcNow(), pair.Node.Id, _views[pair.Side].Definition!, []))
            .ToList();
}
