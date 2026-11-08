namespace Kofrenot.Core.Tests.Input;

public sealed class NodeQuerySearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void AType_ListsItsNodes_ByName_ThoseMatchingTheNameTyped()
    {
        var (ideas, dune) = (Add("Ideas", TypeCatalog.Note), Add("Dune notes", TypeCatalog.Note));
        Add("Dune");

        Assert.Equal([dune, ideas], Nodes(Find("note:")));
        Assert.Equal([dune], Nodes(Find("note: dun")));
    }

    [Fact]
    public void ALinkPhrase_ListsTheNodesReadingItToTheNode()
    {
        var (kitchen, knives, forks) = (Add("Kitchen"), Add("Knives"), Add("Forks"));
        Link(kitchen, knives);
        Link(kitchen, forks);
        Link(knives, kitchen);

        Assert.Equal([forks, knives], Nodes(Find("contained by Kitchen")));
        Assert.Equal([kitchen], Nodes(Find("contains Forks")));
        Assert.Equal([forks, kitchen, knives], Nodes(Find("contained by")));
    }

    [Fact]
    public void ANodeTyped_IsTheOneOfThatName_ElseThoseMatchingIt()
    {
        var (kitchen, table) = (Add("Kitchen"), Add("Kitchen table"));
        var (knives, cloth) = (Add("Knives"), Add("Cloth"));
        Link(kitchen, knives);
        Link(table, cloth);
        Link(kitchen, kitchen);

        Assert.Equal([knives], Nodes(Find("contained by Kitchen")));
        Assert.Equal([cloth, knives], Nodes(Find("contained by Kitch")));
    }

    [Fact]
    public void AType_NarrowsALink()
    {
        var (kitchen, knives, recipe) = (Add("Kitchen"), Add("Knives"), Add("Recipe", TypeCatalog.Note));
        Link(kitchen, knives);
        Link(kitchen, recipe);

        Assert.Equal([recipe], Nodes(Find("note: contained by Kitchen")));
    }

    [Fact]
    public void ATime_ListsTheNodesInItsPeriod_NewestFirst_WithWhen()
    {
        var old = Add("Old", created: Now.AddDays(-40));
        var (yesterday, hour) = (Add("Yesterday", created: Now.AddDays(-1)), Add("An hour ago", created: Now.AddHours(-1)));

        var found = Find("added: this week");

        Assert.Equal([hour, yesterday], Nodes(found));
        Assert.Equal(["added 1 h ago", "added yesterday"], found.Hits.Select(hit => hit.Detail));
        Assert.Equal([hour, yesterday, old], Nodes(Find("changed:")));
        Assert.Equal([old], Nodes(Find("changed: ..2026-10")));
    }

    [Fact]
    public void Opened_ListsTheNodesOpenedHere_ThoseWithNoTimeKept_ForAnyTimeOnly()
    {
        var (dune, herbert, arrakis) = (Add("Dune"), Add("Frank Herbert"), Add("Arrakis"));
        Add("Never opened");
        OpenedNode[] opened = [new(herbert.Id, Now.AddMinutes(-5)), new(arrakis.Id, null), new(dune.Id, Now.AddDays(-3)), new(herbert.Id, Now.AddDays(-9))];

        Assert.Equal([herbert, dune, arrakis], Nodes(Find("opened:", opened)));
        Assert.Equal([herbert], Nodes(Find("opened: today", opened)));
        Assert.Equal(["opened 5 min ago", "opened 3 days ago", null], Find("opened:", opened).Hits.Select(hit => hit.Detail));
    }

    [Fact]
    public void AnUnknownPeriod_IsAProblem()
    {
        Add("Dune");

        var found = Find("changed: soon");

        Assert.Empty(found.Hits);
        Assert.Equal($"“soon” is no period. {TimeQuery.Hint}", found.Summary);
    }

    [Fact]
    public void AtMost_TheLimit_IsListed_TheSummarySayingHowMany()
    {
        for (var i = 0; i < NodeQuerySearch.Limit + 5; i++)
        {
            Add($"Note {i:D3}", TypeCatalog.Note);
        }

        var found = Find("note:");

        Assert.Equal(NodeQuerySearch.Limit, found.Hits.Count);
        Assert.Equal(NodeQuerySearch.Limit + 5, found.Total);
        Assert.Equal($"{NodeQuerySearch.Limit + 5} nodes found, the first {NodeQuerySearch.Limit} listed. Up/Down, then Enter to see the details of one.", found.Summary);
    }

    [Fact]
    public void AnotherLimit_KeepsThatManyHits()
    {
        for (var i = 0; i < NodeQuerySearch.Limit + 5; i++)
        {
            Add($"Note {i:D3}", TypeCatalog.Note);
        }

        var found = NodeQuerySearch.Find(_graph, QueryParser.Parse("?note:", _graph.Types)!, [], Now, TimeZoneInfo.Utc, int.MaxValue);

        Assert.Equal((NodeQuerySearch.Limit + 5, NodeQuerySearch.Limit + 5), (found.Hits.Count, found.Total));
    }

    [Fact]
    public void TheSummary_SaysHowManyWereFound()
    {
        Assert.Equal("No node found.", new QueryResult([], 0).Summary);
        Assert.Equal("1 node found. Down, then Enter to see its details.", new QueryResult([new(Add("Dune"), null)], 1).Summary);
        Assert.Equal("2 nodes found. Up/Down, then Enter to see the details of one.", new QueryResult([new(Add("A"), null), new(Add("B"), null)], 2).Summary);
    }

    private QueryResult Find(string text, IReadOnlyList<OpenedNode>? opened = null) =>
        NodeQuerySearch.Find(_graph, QueryParser.Parse($"?{text}", _graph.Types)!, opened ?? [], Now, TimeZoneInfo.Utc);

    private static List<Node> Nodes(QueryResult found) => found.Hits.Select(hit => hit.Node).ToList();

    private Node Add(string name, NodeType? type = null, DateTimeOffset? created = null)
    {
        var node = new Node(Guid.NewGuid(), (type ?? TypeCatalog.Generic).Id, name, created ?? Now.AddDays(-100));
        _graph.Add(node);
        return node;
    }

    private void Link(Node from, Node to) =>
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, from.Id, to.Id, Now.AddDays(-100)));
}
