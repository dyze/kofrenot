namespace Kofrenot.Core.Tests.Browse;

public sealed class NodeBrowserTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly NodeBrowser _browser = new();

    [Fact]
    public void Closed_ItShowsNothing()
    {
        Assert.False(_browser.IsOpen);
        Assert.Equal(0, _browser.Depth);
        Assert.Null(_browser.Details);
        Assert.Null(_browser.Graph);
        Assert.Empty(_browser.Trail);
        Assert.False(_browser.MoveSelection(1));
        Assert.False(_browser.OpenSelected());
    }

    [Fact]
    public void ANode_ShowsItsProperties_AndItsLinks_OldestFirst()
    {
        var dune = Add("Dune");
        var (herbert, arrakis) = (Add("Frank Herbert"), Add("Arrakis"));
        Link(dune, herbert, minutes: 1);
        Link(arrakis, dune, minutes: 2);

        _browser.Open(_graph, dune);

        var details = _browser.Details!;
        Assert.Same(_graph, _browser.Graph);
        Assert.Equal((dune, "Dune", "Generic"), (details.Node, details.Name, details.Type));
        Assert.Equal([("relates to", "Frank Herbert", true), ("relates to", "Arrakis", false)],
            details.Links.Select(link => (link.Type, link.Other, link.IsSelected)));
        Assert.All(details.Links, link => Assert.True(link.CanOpen));
        Assert.Equal(["Dune"], _browser.Trail);
    }

    [Fact]
    public void UnknownTypes_AreShownByTheirIds()
    {
        var dune = Add("Dune", type: "book");
        Link(dune, Add("Frank Herbert"), type: "written-by");

        _browser.Open(_graph, dune);

        Assert.Equal("book", _browser.Details!.Type);
        Assert.Equal("written-by", Assert.Single(_browser.Details.Links).Type);
    }

    [Fact]
    public void Homonyms_AreMarked()
    {
        var older = Add("Dune", minutes: 1);
        Add("Dune", minutes: 2);
        Link(older, Add("Arrakis"));

        _browser.Open(_graph, older);

        Assert.Equal("Dune¹", _browser.Details!.Name);
    }

    [Fact]
    public void UpDown_SelectALink_WithinTheList()
    {
        var dune = Add("Dune");
        Link(dune, Add("Frank Herbert"), minutes: 1);
        Link(dune, Add("Arrakis"), minutes: 2);
        _browser.Open(_graph, dune);

        Assert.False(_browser.MoveSelection(-1));
        Assert.True(_browser.MoveSelection(1));
        Assert.False(_browser.MoveSelection(1));

        Assert.Equal("Arrakis", _browser.Details!.Links.Single(link => link.IsSelected).Other);
    }

    [Fact]
    public void ANodeWithoutLinks_HasNothingToSelect()
    {
        _browser.Open(_graph, Add("Dune"));

        Assert.Empty(_browser.Details!.Links);
        Assert.False(_browser.MoveSelection(1));
        Assert.False(_browser.OpenSelected());
    }

    [Fact]
    public void Enter_OpensTheSelectedNode_OneLevelDeeper_AndBackShowsTheSameSelection()
    {
        var dune = Add("Dune");
        Link(dune, Add("Frank Herbert"), minutes: 1);
        var arrakis = Add("Arrakis");
        Link(dune, arrakis, minutes: 2);
        _browser.Open(_graph, dune);
        _browser.MoveSelection(1);

        Assert.True(_browser.OpenSelected());
        Assert.Equal(2, _browser.Depth);
        Assert.Same(arrakis, _browser.Details!.Node);
        Assert.Equal(["Dune", "Arrakis"], _browser.Trail);

        _browser.Back();
        Assert.Equal(1, _browser.Depth);
        Assert.Equal("Arrakis", _browser.Details!.Links.Single(link => link.IsSelected).Other);
    }

    [Fact]
    public void ANodeAlreadyInTheTrail_CannotBeOpenedAgain()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        Link(dune, herbert);
        _browser.Open(_graph, dune);
        _browser.OpenSelected();

        var back = Assert.Single(_browser.Details!.Links);
        Assert.Same(dune, back.OtherNode);
        Assert.False(back.CanOpen);
        Assert.False(_browser.OpenSelected());
        Assert.False(_browser.OpenLinked(dune));
        Assert.Equal(2, _browser.Depth);
    }

    [Fact]
    public void TheTrail_StopsAtFourLevels()
    {
        var nodes = Enumerable.Range(1, 6).Select(index => Add($"Node {index}", minutes: index)).ToList();
        for (var index = 1; index < nodes.Count; index++)
        {
            Link(nodes[index - 1], nodes[index], minutes: index);
        }

        _browser.Open(_graph, nodes[0]);
        while (_browser.OpenLinked(nodes[_browser.Depth]))
        {
        }

        Assert.Equal(NodeBrowser.MaxDepth, _browser.Depth);
        Assert.Equal(["Node 1", "Node 2", "Node 3", "Node 4"], _browser.Trail);
        Assert.All(_browser.Details!.Links, link => Assert.False(link.CanOpen));
    }

    [Fact]
    public void ANodeNotLinked_CannotBeOpenedFromTheDetails()
    {
        var dune = Add("Dune");
        _browser.Open(_graph, dune);

        Assert.False(_browser.OpenLinked(Add("Arrakis")));
    }

    [Fact]
    public void ABreadcrumbItem_GoesBackToItsLevel_AndTheSearchClosesTheDetails()
    {
        var nodes = Enumerable.Range(1, 3).Select(index => Add($"Node {index}", minutes: index)).ToList();
        Link(nodes[0], nodes[1]);
        Link(nodes[1], nodes[2], minutes: 1);
        _browser.Open(_graph, nodes[0]);
        _browser.OpenLinked(nodes[1]);
        _browser.OpenLinked(nodes[2]);

        _browser.BackTo(1);
        Assert.Same(nodes[0], _browser.Details!.Node);

        _browser.BackTo(5);
        Assert.Equal(1, _browser.Depth);

        _browser.Back();
        Assert.False(_browser.IsOpen);
        Assert.Null(_browser.Graph);
    }

    [Fact]
    public void OpeningFromTheSearch_StartsANewTrail()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        Link(dune, herbert);
        _browser.Open(_graph, dune);
        _browser.OpenSelected();

        _browser.Open(_graph, herbert);
        _browser.Close();
        _browser.Open(_graph, herbert);

        Assert.Equal(["Frank Herbert"], _browser.Trail);
    }

    [Fact]
    public void ALinkRowOfTheSearch_OpensItsNode_AtLevel2()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        Link(dune, herbert);

        _browser.Open(_graph, herbert, from: dune);

        Assert.Equal(["Dune", "Frank Herbert"], _browser.Trail);
        Assert.Same(herbert, _browser.Details!.Node);
        _browser.Back();
        Assert.Same(dune, _browser.Details!.Node);
    }

    [Fact]
    public void ARename_AppearsInTheDetails_AndTheTrail()
    {
        var dune = Add("Dune");
        _browser.Open(_graph, dune);

        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "Dune (novel)", [dune.Id]));

        Assert.Equal("Dune (novel)", _browser.Details!.Name);
        Assert.Equal(["Dune (novel)"], _browser.Trail);
        Assert.Empty(_browser.Details.NameConflict);
    }

    [Fact]
    public void ANameConflict_ListsTheNames_OldestFirst()
    {
        var dune = Add("Dune");
        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(2), dune.Id, "Dune, 1965", [dune.Id]));
        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "Dune (novel)", [dune.Id]));

        _browser.Open(_graph, dune);

        Assert.Equal(["Dune (novel)", "Dune, 1965"], _browser.Details!.NameConflict);
        Assert.Equal("Dune, 1965", _browser.Details.Name);
    }

    [Fact]
    public void TheDetails_ListTheTypes_OldestFirst()
    {
        var dune = Add("Dune");
        _graph.Apply(new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(2), dune.Id, "dataset"));
        _graph.Apply(new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "note"));
        _graph.Apply(new NodeTypeRemoved(Guid.NewGuid(), Start.AddMinutes(3), dune.Id, [dune.Id]));

        _browser.Open(_graph, dune);

        Assert.Equal([TypeCatalog.Note, TypeCatalog.Dataset], _browser.Details!.Types);
        Assert.Equal("Note, Dataset", _browser.Details.Type);
    }

    [Fact]
    public void ANodeWithNoTypeLeft_IsGeneric()
    {
        var dune = Add("Dune");
        _graph.Apply(new NodeTypeRemoved(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, [dune.Id]));

        _browser.Open(_graph, dune);

        Assert.Equal([TypeCatalog.Generic], _browser.Details!.Types);
        Assert.Equal("Generic", _browser.Details.Type);
        Assert.Empty(_browser.Details.Fields);
    }

    [Fact]
    public void TheDetails_ShowTheColour_AndAColourConflict_OldestFirst()
    {
        var dune = Add("Dune");
        _browser.Open(_graph, dune);

        Assert.Equal((NodeColor.None, 0), (_browser.Details!.Color, _browser.Details.ColorConflict.Count));

        _graph.Apply(new NodeColored(Guid.NewGuid(), Start.AddMinutes(2), dune.Id, "blue", []));
        _graph.Apply(new NodeColored(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "yellow", []));
        _browser.Open(_graph, dune);

        Assert.Equal(NodeColor.Blue, _browser.Details!.Color);
        Assert.Equal([NodeColor.Yellow, NodeColor.Blue], _browser.Details.ColorConflict);
    }

    [Fact]
    public void ATypeUnknownToThisVersion_IsShownByItsId()
    {
        var dune = Add("Dune");
        _graph.Apply(new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "book", [dune.Id]));
        _graph.Apply(new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(2), dune.Id, "note", [dune.Id]));

        _browser.Open(_graph, dune);

        Assert.Equal(["book", "Note"], _browser.Details!.Types.Select(type => type.Name));
    }

    [Fact]
    public void ALinkAddedElsewhere_AppearsInTheDetails()
    {
        var dune = Add("Dune");
        _browser.Open(_graph, dune);

        Link(dune, Add("Arrakis"));

        Assert.Equal("Arrakis", Assert.Single(_browser.Details!.Links).Other);
    }

    [Fact]
    public void ADirectedLink_ReadsFromTheNodeShown()
    {
        var (kitchen, knives) = (Add("Kitchen"), Add("Knives"));
        Link(kitchen, knives, "contains");

        _browser.Open(_graph, kitchen);
        var fromKitchen = Assert.Single(_browser.Details!.Links).Type;
        _browser.Open(_graph, knives);

        Assert.Equal(("contains", "contained by"), (fromKitchen, Assert.Single(_browser.Details!.Links).Type));
    }

    [Fact]
    public void AClickOnALink_SelectsIt_AsUpDownWould()
    {
        var dune = Add("Dune");
        var (herbert, arrakis) = (Add("Frank Herbert"), Add("Arrakis"));
        Link(dune, herbert, minutes: 1);
        Link(dune, arrakis, minutes: 2);
        _browser.Open(_graph, dune);
        var second = _browser.Details!.Links[1];

        Assert.True(_browser.Select(second));
        Assert.Equal([false, true], _browser.Details!.Links.Select(link => link.IsSelected));
        Assert.False(_browser.Select(second));
        Assert.False(_browser.Select(second with { OtherNode = Add("Elsewhere") }));
        Assert.True(_browser.OpenSelected());
        Assert.Equal(arrakis, _browser.Details!.Node);
    }

    private Node Add(string name, string? type = null, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), type ?? TypeCatalog.Generic.Id, name, Start.AddMinutes(minutes));
        _graph.Add(node);
        return node;
    }

    private void Link(Node from, Node to, string type = "relates-to", int minutes = 0) =>
        _graph.Add(new Link(Guid.NewGuid(), type, from.Id, to.Id, Start.AddMinutes(minutes)));
}
