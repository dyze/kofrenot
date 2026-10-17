namespace Kofrenot.Core.Tests.Browse;

public sealed class PinTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly PinPlanner _planner;

    public PinTests() => _planner = new PinPlanner(_graph, _clock);

    [Fact]
    public void ANodeNotPinned_IsPinned()
    {
        var dune = Add("Dune");

        var pin = Assert.IsType<NodePinned>(_planner.Toggle(dune));

        Assert.Equal((dune.Id, _clock.Now), (pin.Node, pin.At));
        Assert.True(_graph.Apply(pin));
        Assert.False(_graph.Apply(pin));
        Assert.Equal([dune], _graph.PinnedNodes);
        Assert.Equal([pin.Id], _graph.PinsOf(dune.Id));
    }

    [Fact]
    public void APinnedNode_IsUnpinned_WithEveryPinItHas()
    {
        var dune = Add("Dune");
        var first = new NodePinned(Guid.NewGuid(), _clock.Now, dune.Id);
        var second = new NodePinned(Guid.NewGuid(), _clock.Now.AddMinutes(1), dune.Id);
        _graph.Apply(first);
        _graph.Apply(second);

        var removal = Assert.IsType<ChangesRemoved>(_planner.Toggle(dune));
        _graph.Apply(removal);

        Assert.Equal(new[] { first.Id, second.Id }.Order(), removal.Removes.Order());
        Assert.Empty(_graph.PinnedNodes);
        Assert.Empty(_graph.PinsOf(dune.Id));
    }

    [Fact]
    public void PinnedNodes_AreListedOnce_TheFirstPinnedFirst_OnlyWhenKnown()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        _graph.Apply(new NodePinned(Guid.NewGuid(), _clock.Now.AddMinutes(2), dune.Id));
        _graph.Apply(new NodePinned(Guid.NewGuid(), _clock.Now.AddMinutes(1), herbert.Id));
        _graph.Apply(new NodePinned(Guid.NewGuid(), _clock.Now.AddMinutes(3), dune.Id));
        _graph.Apply(new NodePinned(Guid.NewGuid(), _clock.Now, Guid.NewGuid()));

        Assert.Equal([herbert, dune], _graph.PinnedNodes);
    }

    [Fact]
    public void TheDetails_SayWhetherTheNodeIsPinned()
    {
        var dune = Add("Dune");
        var browser = new NodeBrowser();
        browser.Open(_graph, dune);
        Assert.False(browser.Details!.IsPinned);

        _graph.Apply(_planner.Toggle(dune));

        Assert.True(browser.Details!.IsPinned);
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), "generic", name, _clock.Now);
        _graph.Add(node);
        return node;
    }
}
