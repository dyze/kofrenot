namespace Kofrenot.Core.Tests.Graph;

public sealed class ColorTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Note.Id, "Dune", Start);

    public ColorTests() => _graph.Add(_dune);

    private NodeColored ColorDune(string? color, int minutes, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), _dune.Id, color, replaces);

    [Fact]
    public void ANode_HasNoColour_UntilGivenOne()
    {
        Assert.Empty(_graph.ColorsOf(_dune.Id));
        Assert.Equal(NodeColor.None, _graph.ColorOf(_dune.Id));
        Assert.Equal(NodeColor.None, _graph.ColorOf(Guid.NewGuid()));
    }

    [Fact]
    public void AColour_IsShown_AndAppliedOnce()
    {
        var yellow = ColorDune("yellow", 1);

        Assert.True(_graph.Apply(yellow));
        Assert.False(_graph.Apply(yellow));

        Assert.Equal([new ColorVersion(yellow.Id, NodeColor.Yellow, yellow.At)], _graph.ColorsOf(_dune.Id));
        Assert.Equal(NodeColor.Yellow, _graph.ColorOf(_dune.Id));
        Assert.Equal([yellow], _graph.ColoringsOf(_dune.Id));
    }

    [Fact]
    public void ALaterColour_ReplacesTheEarlierOne()
    {
        var yellow = ColorDune("yellow", 1);
        _graph.Apply(ColorDune("green", 2, yellow.Id));
        _graph.Apply(yellow);

        Assert.Equal(NodeColor.Green, Assert.Single(_graph.ColorsOf(_dune.Id)).Color);
        Assert.Equal(2, _graph.ColoringsOf(_dune.Id).Count());
    }

    [Fact]
    public void ANullColour_TakesItAway()
    {
        var yellow = ColorDune("yellow", 1);
        _graph.Apply(yellow);
        _graph.Apply(ColorDune(null, 2, yellow.Id));

        Assert.Equal(NodeColor.None, _graph.ColorOf(_dune.Id));
    }

    [Fact]
    public void ColoursGivenApart_AreAConflict_ShowingTheLatest_WhateverTheOrder()
    {
        var yellow = ColorDune("yellow", 1);
        var mine = ColorDune("green", 2, yellow.Id);
        var theirs = ColorDune("blue", 3, yellow.Id);

        _graph.Apply(theirs);
        _graph.Apply(mine);
        _graph.Apply(yellow);

        Assert.Equal([NodeColor.Green, NodeColor.Blue], _graph.ColorsOf(_dune.Id).Select(version => version.Color));
        Assert.Equal(NodeColor.Blue, _graph.ColorOf(_dune.Id));

        _graph.Apply(ColorDune("green", 4, mine.Id, theirs.Id));

        Assert.Equal(NodeColor.Green, Assert.Single(_graph.ColorsOf(_dune.Id)).Color);
    }

    [Fact]
    public void AColourUnknownToThisVersion_IsKept_AndShownAsNone()
    {
        var unknown = ColorDune("orange", 1);
        _graph.Apply(unknown);

        Assert.Equal([new ColorVersion(unknown.Id, NodeColor.None, unknown.At)], _graph.ColorsOf(_dune.Id));
    }

    [Fact]
    public void AColour_ArrivingBeforeItsNode_IsShownOnceItArrives()
    {
        var graph = new KnowledgeGraph();
        graph.Apply(new NodeColored(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "pink", []));
        graph.Add(_dune);

        Assert.Equal(NodeColor.Pink, graph.ColorOf(_dune.Id));
    }

    [Theory]
    [InlineData(NodeColor.None, null)]
    [InlineData(NodeColor.Yellow, "yellow")]
    [InlineData(NodeColor.Grey, "grey")]
    public void AColour_IsWrittenByItsName(NodeColor color, string? name)
    {
        Assert.Equal(name, NodeColorNames.Of(color));
        Assert.Equal(color, NodeColorNames.Parse(name));
    }

    [Theory]
    [InlineData("Teal", NodeColor.Teal)]
    [InlineData("orange", NodeColor.None)]
    [InlineData("1", NodeColor.None)]
    [InlineData("", NodeColor.None)]
    public void AName_IsReadIgnoringCase_AndAnUnknownOneIsNone(string name, NodeColor color) =>
        Assert.Equal(color, NodeColorNames.Parse(name));
}
