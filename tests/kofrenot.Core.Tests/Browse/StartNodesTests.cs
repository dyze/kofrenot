namespace Kofrenot.Core.Tests.Browse;

public sealed class StartNodesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void TheNodesChanged_InTheLastWeek_AreRecent_NewestFirst()
    {
        var old = Add("Old", Now.AddDays(-8));
        var dune = Add("Dune", Now.AddDays(-3));
        var herbert = Add("Frank Herbert", Now.AddHours(-1));

        var start = StartNodes.Of(_graph, [], Now);

        Assert.Equal([herbert, dune], start.Recent.Select(recent => recent.Node));
        Assert.Equal(["changed 1 h ago", "changed 3 days ago"], start.Recent.Select(recent => recent.Detail));
        Assert.DoesNotContain(old, start.Recent.Select(recent => recent.Node));
    }

    [Fact]
    public void ANoteSaved_OrALinkAdded_IsAChange()
    {
        var note = Add("Ideas", Now.AddDays(-20));
        var (kitchen, knives) = (Add("Kitchen", Now.AddDays(-20)), Add("Knives", Now.AddDays(-20)));

        _graph.Apply(new NoteEdited(Guid.NewGuid(), Now.AddMinutes(-5), note.Id, []));
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, kitchen.Id, knives.Id, Now.AddMinutes(-10)));

        Assert.Equal([note, kitchen, knives], StartNodes.Of(_graph, [], Now).Recent.Select(recent => recent.Node));
    }

    [Fact]
    public void OpenedAndChanged_AreMerged_EachOnce_ByTheLaterOfBoth()
    {
        var dune = Add("Dune", Now.AddDays(-20));
        var herbert = Add("Frank Herbert", Now.AddMinutes(-30));
        var arrakis = Add("Arrakis", Now.AddHours(-3));
        OpenedNode[] opened = [new(dune.Id, Now.AddHours(-1)), new(arrakis.Id, Now.AddMinutes(-10)), new(herbert.Id, Now.AddHours(-2))];

        var recent = StartNodes.Of(_graph, opened, Now).Recent;

        Assert.Equal([arrakis, herbert, dune], recent.Select(node => node.Node));
        Assert.Equal(["opened 10 min ago", "changed 30 min ago", "opened 1 h ago"], recent.Select(node => node.Detail));
    }

    [Fact]
    public void ANodeOpened_LongAgo_StaysRecent_AndUnknownOnesAreSkipped()
    {
        var dune = Add("Dune", Now.AddDays(-40));

        var recent = StartNodes.Of(_graph, [new(Guid.NewGuid(), Now), new(dune.Id, Now.AddDays(-30))], Now).Recent;

        Assert.Equal([new RecentNode(dune, "opened 30 days ago")], recent);
    }

    [Fact]
    public void NodesOpened_BeforeTheirTimeWasKept_ComeLast_InTheirOrder()
    {
        var (dune, herbert) = (Add("Dune", Now.AddDays(-20)), Add("Frank Herbert", Now.AddDays(-20)));
        var arrakis = Add("Arrakis", Now.AddDays(-2));

        var recent = StartNodes.Of(_graph, [new(herbert.Id, null), new(dune.Id, null)], Now).Recent;

        Assert.Equal([new RecentNode(arrakis, "changed 2 days ago"), new RecentNode(herbert, null), new RecentNode(dune, null)], recent);
    }

    [Fact]
    public void PinnedNodes_AreNotRecent_AndLeaveRoomForOthers()
    {
        var pinned = Add("Pinned", Now);
        _graph.Apply(new NodePinned(Guid.NewGuid(), Now, pinned.Id));
        for (var i = 1; i <= StartNodes.MaxRecent; i++)
        {
            Add($"Node {i}", Now.AddMinutes(-i));
        }

        var start = StartNodes.Of(_graph, [new(pinned.Id, Now)], Now);

        Assert.Equal([pinned], start.Pinned);
        Assert.Equal(StartNodes.MaxRecent, start.Recent.Count);
        Assert.DoesNotContain(pinned, start.Recent.Select(recent => recent.Node));
    }

    [Fact]
    public void AtMost_MaxRecent_AreListed()
    {
        for (var i = 0; i < StartNodes.MaxRecent + 5; i++)
        {
            Add($"Node {i}", Now.AddMinutes(-i));
        }

        var recent = StartNodes.Of(_graph, [], Now).Recent;

        Assert.Equal(StartNodes.MaxRecent, recent.Count);
        Assert.Equal("Node 0", recent[0].Node.Name);
    }

    private Node Add(string name, DateTimeOffset created)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, created);
        _graph.Add(node);
        return node;
    }
}
