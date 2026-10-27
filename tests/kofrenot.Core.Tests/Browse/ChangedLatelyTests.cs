namespace Kofrenot.Core.Tests.Browse;

public sealed class ChangedLatelyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void TheNodesChanged_InTheLastWeek_AreListed_NewestFirst()
    {
        var old = Add("Old", Now.AddDays(-8));
        var dune = Add("Dune", Now.AddDays(-3));
        var herbert = Add("Frank Herbert", Now.AddHours(-1));

        var changed = ChangedLately.Of(_graph, Now);

        Assert.Equal([herbert, dune], changed.Select(change => change.Node));
        Assert.Equal(Now.AddHours(-1), changed[0].At);
        Assert.DoesNotContain(old, changed.Select(change => change.Node));
    }

    [Fact]
    public void ANoteSaved_OrALinkAdded_IsAChange()
    {
        var note = Add("Ideas", Now.AddDays(-20));
        var (kitchen, knives) = (Add("Kitchen", Now.AddDays(-20)), Add("Knives", Now.AddDays(-20)));

        _graph.Apply(new NoteEdited(Guid.NewGuid(), Now.AddMinutes(-5), note.Id, []));
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, kitchen.Id, knives.Id, Now.AddMinutes(-10)));

        Assert.Equal([note, kitchen, knives], ChangedLately.Of(_graph, Now).Select(change => change.Node));
    }

    [Fact]
    public void AtMost_Max_AreListed()
    {
        for (var i = 0; i < ChangedLately.Max + 5; i++)
        {
            Add($"Node {i}", Now.AddMinutes(-i));
        }

        var changed = ChangedLately.Of(_graph, Now);

        Assert.Equal(ChangedLately.Max, changed.Count);
        Assert.Equal("Node 0", changed[0].Node.Name);
    }

    private Node Add(string name, DateTimeOffset created)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, created);
        _graph.Add(node);
        return node;
    }
}
