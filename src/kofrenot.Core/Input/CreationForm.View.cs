using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Input;

/// <summary>A new view's step: its definition, saved with the creation as its first version.</summary>
public sealed partial class CreationForm
{
    private readonly Dictionary<Side, ViewDraft> _views = new() { [Side.Left] = ViewDraft.Default, [Side.Right] = ViewDraft.Default };

    /// <summary>A new node of type view: its step has its definition to choose.</summary>
    public bool IsView(Side side) => TypeOf(side).Id == TypeCatalog.View.Id;

    public ViewDraft ViewOf(Side side) => _views[side];

    public void SetView(Side side, ViewDraft view) => _views[side] = view;

    /// <summary>The graph a view's definition is read in: its roots found, its query read.</summary>
    public KnowledgeGraph Graph => _graph;

    /// <summary>A new view showing what a query typed in the input box finds; its name to type.</summary>
    public static CreationForm QueryView(KnowledgeGraph graph, string query, TimeProvider? clock = null)
    {
        var form = NewNode(graph, TypeCatalog.View, clock: clock);
        form.SetView(Side.Left, ViewDraft.Default with { Display = ViewDisplay.Query, Query = QueryDraft.Default.WithText(query.Trim(), graph) });
        return form;
    }

    /// <summary>False on a new view's step while its definition is not complete.</summary>
    public bool CanLeaveStep => Step.Side is not { } side || !IsView(side) || _views[side].Definition is not null;

    /// <summary>The plan can be added, its node has a name, and every new view has its definition.</summary>
    public bool CanAdd => Plan.CanAdd && HasName && NewSides.All(side => !IsView(side) || _views[side].Definition is not null);

    /// <summary>The first version of each new view, as <see cref="NotesToSave"/>.</summary>
    public IReadOnlyList<ViewEdited> ViewsToSave() =>
        NewNodes()
            .Where(pair => IsView(pair.Side) && _views[pair.Side].Definition is not null)
            .Select(pair => new ViewEdited(Guid.NewGuid(), _clock.GetUtcNow(), pair.Node.Id, _views[pair.Side].Definition!, []))
            .ToList();
}
