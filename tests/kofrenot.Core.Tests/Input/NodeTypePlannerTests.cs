namespace Kofrenot.Core.Tests.Input;

public sealed class NodeTypePlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };

    private NodeTypePlanner Planner => new(_graph, _clock);

    private static NodeTypeDraft Draft(string name, params FieldDraft[] fields) => new(name, "Contact", NodeColor.Blue, fields);

    private CustomNodeType Define(NodeTypeDraft draft)
    {
        _graph.Apply(Planner.Plan(draft).Change!);
        return _graph.NodeTypes.All[^1];
    }

    [Fact]
    public void ANewType_IsDefined_WithItsFields()
    {
        var plan = Planner.Plan(Draft(" Person ", new FieldDraft(null, " Born ", FieldKind.Date), new FieldDraft(null, "Genre", FieldKind.Choice, [" Novel ", "Poem"])));

        Assert.Equal((true, "Adds “Person”."), (plan.CanApply, plan.Status));
        var change = plan.Change!;
        Assert.Equal((change.Id.ToString(), "Person", "Contact", "blue", Start.AddMinutes(10)), (change.NodeType, change.Name, change.Icon, change.Color, change.At));
        Assert.Empty(change.Replaces);
        Assert.Equal(["Born", "Genre"], change.Fields.Select(field => field.Name));
        Assert.Equal(["Novel", "Poem"], change.Fields[1].Choices);
        Assert.NotEqual(change.Fields[0].Id, change.Fields[1].Id);
    }

    [Fact]
    public void AnIconThisVersionDoesNotKnow_AndNoColour_AreNone()
    {
        var change = Planner.Plan(new NodeTypeDraft("Person", "Unicorn", NodeColor.None, [])).Change!;

        Assert.Equal((null, null), (change.Icon, change.Color));
    }

    [Fact]
    public void ANonChoiceField_HasNoOptions()
    {
        var change = Planner.Plan(Draft("Person", new FieldDraft(null, "Born", FieldKind.Date, ["x"]))).Change!;

        Assert.Empty(change.Fields[0].Choices);
    }

    [Theory]
    [InlineData(" ", NodeTypeProblem.Empty, "Type a name.")]
    [InlineData("note", NodeTypeProblem.Taken, "“Note” is a node type already.")]
    public void AName_IsNeeded_AndOfItsOwn(string name, NodeTypeProblem problem, string status)
    {
        var plan = Planner.Plan(Draft(name));

        Assert.Equal((problem, status, false), (plan.Problem, plan.Status, plan.CanApply));
    }

    [Fact]
    public void FieldsNeedANameEach_AndOfTheirOwn()
    {
        Assert.Equal("Name every field.", Planner.Plan(Draft("Person", new FieldDraft(null, " ", FieldKind.Text))).Status);
        Assert.Equal(
            "Two fields are named “Born”.",
            Planner.Plan(Draft("Person", new FieldDraft(null, "Born", FieldKind.Text), new FieldDraft(null, "born", FieldKind.Date))).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Novel| ")]
    [InlineData("Novel|novel")]
    public void AChoice_NeedsItsOptions_EachOnce(string choices)
    {
        var options = choices.Length == 0 ? [] : choices.Split('|');

        Assert.Equal("Give “Genre” its options, each once.", Planner.Plan(Draft("Book", new FieldDraft(null, "Genre", FieldKind.Choice, options))).Status);
    }

    [Fact]
    public void AContentField_IsNotForAUsersType()
    {
        Assert.Throws<ArgumentException>(() => Planner.Plan(Draft("Book", new FieldDraft(null, "Text", FieldKind.Markdown))));
    }

    [Fact]
    public void AnEdit_ReplacesTheDefinition_KeepingTheIdsOfItsFields()
    {
        var person = Define(Draft("Person", new FieldDraft(null, "Born", FieldKind.Date)));
        var born = person.Type.Fields[0];

        var plan = Planner.Edit(person, Draft("Human", FieldDraft.Of(born) with { Name = "Birth date" }, new FieldDraft(null, "Died", FieldKind.Date)));

        Assert.StartsWith("Changes “Person”", plan.Status);
        var change = plan.Change!;
        Assert.Equal((person.Type.Id, "Human"), (change.NodeType, change.Name));
        Assert.Equal([person.Definitions[0].Id], change.Replaces);
        Assert.Equal(born.Id, change.Fields[0].Id);
    }

    [Fact]
    public void AnEdit_KeepsItsOwnName_AndIsUnchanged_WhenNothingChanges()
    {
        var person = Define(Draft("Person", new FieldDraft(null, "Born", FieldKind.Date)));

        var plan = Planner.Edit(person, NodeTypeDraft.Of(person.Type));

        Assert.Equal((NodeTypeProblem.Unchanged, string.Empty), (plan.Problem, plan.Status));
        Assert.True(Planner.Edit(person, NodeTypeDraft.Of(person.Type) with { Color = NodeColor.Green }).CanApply);
        Assert.Equal(NodeTypeProblem.Taken, Planner.Edit(person, NodeTypeDraft.Of(person.Type) with { Name = "View" }).Problem);
    }

    [Fact]
    public void AnEditOfAConflict_ReplacesBoth()
    {
        var person = Define(Draft("Person"));
        var first = person.Definitions[0];
        _graph.Apply(first with { Id = Guid.NewGuid(), Name = "Human", Replaces = [first.Id] });
        _graph.Apply(first with { Id = Guid.NewGuid(), Name = "People", Replaces = [first.Id] });
        var conflict = _graph.NodeTypes.All[0];

        var plan = Planner.Edit(conflict, NodeTypeDraft.Of(conflict.Type));

        Assert.True(plan.CanApply);
        Assert.Equal(conflict.Definitions.Select(definition => definition.Id), plan.Change!.Replaces);
    }

    [Fact]
    public void ARemoval_TakesEveryDefinition_AndSaysWhatItsNodesKeep()
    {
        var person = Define(Draft("Person"));
        var removal = Planner.Remove(person);

        Assert.Equal(person.Ids, removal.Change.Removes);
        Assert.Equal("Remove the node type “Person”?", removal.Question);

        _graph.Add(new Node(Guid.NewGuid(), person.Type.Id, "Frank Herbert", Start));
        Assert.Equal("Remove the node type “Person”? 1 node has it: it keeps its values, as its own fields.", Planner.Remove(person).Question);

        _graph.Add(new Node(Guid.NewGuid(), person.Type.Id, "Brian Herbert", Start));
        Assert.Equal("Remove the node type “Person”? 2 nodes have it: they keep their values, as their own fields.", Planner.Remove(person).Question);
    }
}
