namespace Kofrenot.Core.Tests.Views;

/// <summary>A node copied in a tree keeps its types, its colour and its fields' values, an image's file written again.</summary>
public sealed class CopyFieldsTests
{
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);
    private static readonly FieldDefinition Portrait = new("portrait", "Portrait", FieldKind.Image);
    private static readonly DatasetFile Photo = new("bruce.jpg", 1200, new string('a', 64));

    private readonly TreeGraph _tree = new();
    private readonly ManualClock _clock = new() { Now = TreeGraph.Start.AddHours(1) };
    private readonly Node _bruce;
    private readonly Node _archive;

    public CopyFieldsTests()
    {
        _tree.Graph.Apply(new NodeTypeDefined(Guid.NewGuid(), TreeGraph.Start, "person", "Person", null, null, [Born, Portrait], []));
        _bruce = _tree.Add("Bruce", "person");
        _archive = _tree.Add("Archive");
    }

    private Node CopyOfBruce(CopyPlan plan)
    {
        _tree.Apply(plan.Changes);
        return _tree.Graph.Nodes.Single(node => node.Name == "Bruce" && node.Id != _bruce.Id);
    }

    private CopyPlan Plan(Func<ContentEdited, bool>? isAvailable = null) =>
        new CopyPlanner(_tree.Graph, TypeCatalog.Contains.Id, _clock).Plan(_bruce, _archive, isAvailable ?? (_ => true));

    private FieldSet Set(FieldDefinition field, string? value, DatasetFile? file = null, params Guid[] replaces)
    {
        var set = new FieldSet(Guid.NewGuid(), TreeGraph.Start, _bruce.Id, "person", field.Id, field.Name, field.Kind, value, replaces, file);
        _tree.Graph.Apply(set);
        return set;
    }

    [Fact]
    public void ACopy_HasAllTheTypes_OfItsOriginal()
    {
        _tree.Graph.Apply(new NodeTypeAdded(Guid.NewGuid(), TreeGraph.Start.AddMinutes(1), _bruce.Id, "note"));

        var copy = CopyOfBruce(Plan());

        Assert.Equal(["person", "note"], _tree.Graph.TypeIdsOf(copy.Id));
        Assert.Equal("note", copy.Type);
    }

    [Fact]
    public void ACopyOfANodeWithNoTypeLeft_IsGeneric()
    {
        _tree.Graph.Apply(new NodeTypeRemoved(Guid.NewGuid(), TreeGraph.Start, _bruce.Id, [_bruce.Id]));

        var copy = CopyOfBruce(Plan());

        Assert.Equal(["generic"], _tree.Graph.TypeIdsOf(copy.Id));
    }

    [Fact]
    public void ACopy_HasTheColourOfItsOriginal()
    {
        _tree.Graph.Apply(new NodeColored(Guid.NewGuid(), TreeGraph.Start, _bruce.Id, "teal", []));

        var copy = CopyOfBruce(Plan());

        Assert.Equal(NodeColor.Teal, _tree.Graph.ColorOf(copy.Id));
    }

    [Fact]
    public void ACopyOfANodeWithoutColour_HasNone()
    {
        var plan = Plan();

        Assert.DoesNotContain(plan.Changes, change => change is NodeColored);
        Assert.Equal(NodeColor.None, _tree.Graph.ColorOf(CopyOfBruce(plan).Id));
    }

    [Fact]
    public void ACopy_HasTheValuesShown_NotTheClearedOnes()
    {
        var first = Set(Born, "1920-10-08");
        Set(Born, "1920-10-09", null, first.Id);
        var own = new FieldSet(Guid.NewGuid(), TreeGraph.Start, _bruce.Id, FieldSet.Own, "nick", "Nickname", FieldKind.Text, "Batman", []);
        _tree.Graph.Apply(own);
        _tree.Graph.Apply(own with { Id = Guid.NewGuid(), Value = null, Replaces = [own.Id] });

        var copy = CopyOfBruce(Plan());

        var fields = NodeFields.Of(_tree.Graph, copy.Id);
        Assert.Equal("1920-10-09", fields.Single(field => field.Name == "Born").Value);
        Assert.DoesNotContain(fields, field => field.Name == "Nickname");
        var value = Assert.Single(_tree.Graph.FieldSetsOf(copy.Id));
        Assert.Equal((_clock.Now, 0), (value.At, value.Replaces.Count));
    }

    [Fact]
    public void AnImage_IsCopied_ItsFileWrittenAgain_AsADatasets()
    {
        var image = Set(Portrait, null, Photo);

        var plan = Plan();
        var copy = CopyOfBruce(plan);

        var copied = NodeFields.Of(_tree.Graph, copy.Id).Single(field => field.Kind == FieldKind.Image).Image!;
        Assert.Equal((Photo, (Guid?)null), (copied.File, copied.Content));
        var content = Assert.Single(plan.Contents);
        Assert.Equal((image.Id, copied.Id), (content.From.Id, content.To.Id));
        Assert.Equal(copy.Id, content.To.Node);
    }

    [Fact]
    public void AnImageNotHere_KeepsTheNodeFromBeingCopied()
    {
        var image = Set(Portrait, null, Photo);

        var plan = Plan(version => version.Id != image.Id);

        Assert.False(plan.CanCopy);
        Assert.Equal([_bruce], plan.Missing);
    }

    [Fact]
    public void AnImageKeptFromAConflict_IsCopiedFromTheFileItKeeps()
    {
        var kept = Set(Portrait, null, Photo);
        _tree.Graph.Apply(kept with { Id = Guid.NewGuid(), At = TreeGraph.Start.AddMinutes(1), Content = kept.Id, Replaces = [kept.Id] });

        var content = Assert.Single(Plan().Contents);

        Assert.Equal(kept.Id, content.From.Id);
    }
}
