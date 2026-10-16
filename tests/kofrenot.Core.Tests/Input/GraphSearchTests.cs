namespace Kofrenot.Core.Tests.Input;

public sealed class GraphSearchTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void ANameMatches_WhenOneOfItsWordsStartsWithTheText()
    {
        Add("Dune", "Dundee", "Frank Herbert", "Redundancy");

        Assert.Equal(["Dundee", "Dune"], Names(GraphSearch.Find(_graph, "dun")));
        Assert.Equal(["Frank Herbert"], Names(GraphSearch.Find(_graph, "HER")));
        Assert.Equal(["Frank Herbert"], Names(GraphSearch.Find(_graph, "frank  he")));
    }

    [Fact]
    public void AccentsAreIgnored()
    {
        Add("Émile Zola");

        Assert.Single(GraphSearch.Find(_graph, "emi"));
    }

    [Fact]
    public void AnEmptyText_FindsNothing()
    {
        Add("Dune");

        Assert.Empty(GraphSearch.Find(_graph, "  "));
    }

    [Fact]
    public void ExactNamesComeFirst_ThenNamesStartingWithTheText_ThenTheOthers()
    {
        Add("Old Dune", "Dunes", "Dune");

        var hits = GraphSearch.Find(_graph, "dune");

        Assert.Equal(["Dune", "Dunes", "Old Dune"], Names(hits));
        Assert.True(hits[0].IsExact);
        Assert.False(hits[1].IsExact);
    }

    [Fact]
    public void Homonyms_AreAllListed_OldestFirst_WithTheirMark()
    {
        var older = Add("Dune", minutes: 1);
        var newer = Add("dune", minutes: 2);

        var hits = GraphSearch.Find(_graph, "Dune");

        Assert.Equal([older, newer], hits.Select(hit => hit.Node));
        Assert.Equal(["¹", "²"], hits.Select(hit => hit.Mark));
        Assert.All(hits, hit => Assert.Equal(2, hit.Homonyms));
    }

    [Fact]
    public void AUniqueName_HasNoMark()
    {
        Add("Dune");

        Assert.Equal(string.Empty, GraphSearch.Find(_graph, "Dune")[0].Mark);
    }

    [Fact]
    public void TheResults_AreLimited()
    {
        for (var index = 0; index < 30; index++)
        {
            Add($"Node {index}");
        }

        Assert.Equal(GraphSearch.DefaultLimit, GraphSearch.Find(_graph, "node").Count);
        Assert.Equal(5, GraphSearch.Find(_graph, "node", limit: 5).Count);
    }

    [Theory]
    [InlineData(2, "²")]
    [InlineData(10, "¹⁰")]
    public void Superscript_WritesEveryDigit(int number, string expected) =>
        Assert.Equal(expected, SearchHit.Superscript(number));

    private Node Add(string name, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start.AddMinutes(minutes));
        _graph.Add(node);
        return node;
    }

    private void Add(params string[] names)
    {
        foreach (var name in names)
        {
            Add(name);
        }
    }

    private static IEnumerable<string> Names(IEnumerable<SearchHit> hits) => hits.Select(hit => hit.Node.Name);
}
