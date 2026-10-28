namespace Kofrenot.Core.Tests.Input;

public sealed class TypeCopyPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);

    private readonly KnowledgeGraph _source = new();
    private readonly KnowledgeGraph _target = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };

    private static NodeTypeDefined Person(string name = "Person", params FieldDefinition[] fields) =>
        new(Guid.NewGuid(), Start, "person", name, "Contact", "blue", fields.Length == 0 ? [Born] : fields, []);

    private TypeCopy Copy(NodeTypeDefined source)
    {
        _source.Apply(source);
        return Assert.Single(new TypeCopyPlanner(_target, _clock).Plan(_source.NodeTypes.All));
    }

    [Fact]
    public void ATypeNotHere_IsAdded_WithItsIds()
    {
        var copy = Copy(Person());

        Assert.Equal((TypeCopyOutcome.Adds, "Adds it"), (copy.Outcome, copy.Status));
        var change = copy.Change!;
        Assert.Equal(("person", "Person", "Contact", "blue", Start.AddMinutes(10)), (change.NodeType, change.Name, change.Icon, change.Color, change.At));
        Assert.Equal([Born], change.Fields);
        Assert.Empty(change.Replaces);
    }

    [Fact]
    public void AColourlessType_IsCopiedWithout()
    {
        var copy = Copy(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [], []));

        Assert.Null(copy.Change!.Color);
    }

    [Fact]
    public void ATypeHere_DefinedOtherwise_IsUpdated()
    {
        var here = Person("Human");
        _target.Apply(here);

        var copy = Copy(Person());

        Assert.Equal((TypeCopyOutcome.Updates, "Updates it"), (copy.Outcome, copy.Status));
        Assert.Equal([here.Id], copy.Change!.Replaces);
        _target.Apply(copy.Change);
        Assert.Equal("Person", _target.NodeTypes.Find("person")!.Type.Name);
    }

    [Fact]
    public void ATypeHere_AsItIs_WritesNothing()
    {
        _target.Apply(Person());

        var copy = Copy(Person());

        Assert.Equal((TypeCopyOutcome.Same, "Already the same here", (NodeTypeDefined?)null), (copy.Outcome, copy.Status, copy.Change));
    }

    [Fact]
    public void ATypeInConflictHere_IsUpdated_ReplacingBoth()
    {
        var here = Person();
        _target.Apply(here);
        var (mine, theirs) = (here with { Id = Guid.NewGuid(), Replaces = [here.Id] }, here with { Id = Guid.NewGuid(), Name = "Human", Replaces = [here.Id] });
        _target.Apply(mine);
        _target.Apply(theirs);

        var copy = Copy(Person());

        Assert.Equal(TypeCopyOutcome.Updates, copy.Outcome);
        Assert.Equal(2, copy.Change!.Replaces.Count);
    }

    [Fact]
    public void AnotherTypeOfItsName_RefusesTheCopy()
    {
        _target.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "people", "person", null, null, [], []));

        var copy = Copy(Person());

        Assert.Equal((TypeCopyOutcome.NameTaken, "“person” is a node type here already: rename one first"), (copy.Outcome, copy.Status));
        Assert.Null(copy.Change);
    }

    [Fact]
    public void ABuiltInTypesName_RefusesTheCopy()
    {
        Assert.Equal(TypeCopyOutcome.NameTaken, Copy(new NodeTypeDefined(Guid.NewGuid(), Start, "my-note", "Note", null, null, [], [])).Outcome);
    }

    [Fact]
    public void ATypeRemovedHere_IsAddedBack()
    {
        var here = Person();
        _target.Apply(here);
        _target.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [here.Id]));

        var copy = Copy(Person());

        Assert.Equal(TypeCopyOutcome.Adds, copy.Outcome);
        _target.Apply(copy.Change!);
        Assert.NotNull(_target.NodeTypes.Find("person"));
    }

    [Fact]
    public void ValuesOnNodesHere_FollowTheTypeCopied()
    {
        var frank = new Node(Guid.NewGuid(), "person", "Frank Herbert", Start);
        _target.Add(frank);
        _target.Apply(new FieldSet(Guid.NewGuid(), Start, frank.Id, "person", "born", "Born", FieldKind.Date, "1920-10-08", []));

        _target.Apply(Copy(Person()).Change!);

        var field = Assert.Single(NodeFields.Of(_target, frank.Id));
        Assert.Equal(("1920-10-08", false), (field.Value, field.IsOwn));
    }
}
