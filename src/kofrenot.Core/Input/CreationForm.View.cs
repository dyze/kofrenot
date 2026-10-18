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

    /// <summary>The nodes a tree can start from (<see cref="ViewDraft.FindRoots"/>).</summary>
    public IReadOnlyList<SearchHit> FindRoots(string text) => ViewDraft.FindRoots(_graph, text);

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
