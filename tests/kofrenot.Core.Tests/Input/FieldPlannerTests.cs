namespace Kofrenot.Core.Tests.Input;

public sealed class FieldPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);
    private static readonly FieldDefinition Read = new("read", "Read", FieldKind.Checkbox);
    private static readonly FieldDefinition Genre = new("genre", "Genre", FieldKind.Choice, ["Novel", "Short story"]);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };
    private readonly Node _frank = new(Guid.NewGuid(), "person", "Frank Herbert", Start);

    public FieldPlannerTests()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [Born, Read, Genre], []));
        _graph.Add(_frank);
    }

    private FieldPlanner Planner => new(_graph, _clock);

    private NodeField Field(string label) => NodeFields.Of(_graph, _frank.Id).Single(field => field.Label == label);

    private void Apply(FieldPlan plan)
    {
        foreach (var change in plan.Changes)
        {
            _graph.Apply(change);
        }
    }

    [Fact]
    public void AValueTyped_IsWritten_AsItsKindWritesIt()
    {
        var plan = Planner.Set(_frank, Field("Born"), " 1920-10-08 ");

        Assert.True(plan.CanApply);
        Assert.Equal(string.Empty, plan.Status);
        var value = Assert.IsType<FieldSet>(Assert.Single(plan.Changes));
        Assert.Equal((_frank.Id, "person", "born", "Born", FieldKind.Date, "1920-10-08", Start.AddMinutes(10)), (value.Node, value.Owner, value.Field, value.Name, value.Kind, value.Value, value.At));
        Assert.Empty(value.Replaces);
    }

    [Fact]
    public void AValueNotFitting_IsRefused()
    {
        var plan = Planner.Set(_frank, Field("Born"), "soon");

        Assert.Equal((FieldProblem.Invalid, false), (plan.Problem, plan.CanApply));
        Assert.Equal("Type a date, like 2026-10-27", plan.Status);
        Assert.Equal(FieldProblem.Invalid, Planner.Set(_frank, Field("Genre"), "Poem").Problem);
    }

    [Fact]
    public void ANewValue_ReplacesTheCurrentOnes()
    {
        Apply(Planner.Set(_frank, Field("Born"), "1920-10-08"));
        var current = Field("Born").Current;

        var plan = Planner.Set(_frank, Field("Born"), "1920-10-09");

        Assert.Equal(current, Assert.IsType<FieldSet>(Assert.Single(plan.Changes)).Replaces);
    }

    [Fact]
    public void TheSameValue_IsUnchanged_UnlessInConflict()
    {
        Apply(Planner.Set(_frank, Field("Born"), "1920-10-08"));

        Assert.Equal(FieldProblem.Unchanged, Planner.Set(_frank, Field("Born"), "1920-10-08").Problem);
        Assert.Equal(string.Empty, Planner.Set(_frank, Field("Born"), "1920-10-08").Status);

        var first = Field("Born").Values!.Shown;
        _graph.Apply(first with { Id = Guid.NewGuid(), Value = "1921-01-01", Replaces = [first.Id] });
        _graph.Apply(first with { Id = Guid.NewGuid(), At = Start.AddMinutes(11), Value = "1920-10-08", Replaces = [first.Id] });

        Assert.True(Planner.Set(_frank, Field("Born"), "1920-10-08").CanApply);
    }

    [Fact]
    public void AConflict_IsResolved_ByKeepingOneValue()
    {
        Apply(Planner.Set(_frank, Field("Born"), "1920-10-08"));
        var first = Field("Born").Values!.Shown;
        _graph.Apply(first with { Id = Guid.NewGuid(), Value = "1921-01-01", Replaces = [first.Id] });
        _graph.Apply(first with { Id = Guid.NewGuid(), Value = "1922-01-01", Replaces = [first.Id] });
        var field = Field("Born");

        Apply(Planner.Keep(_frank, field, field.Values!.Current[0]));

        Assert.Equal((false, field.Values.Current[0].Value), (Field("Born").Values!.IsConflict, Field("Born").Value));
    }

    [Fact]
    public void AnEmptyValue_ClearsTheField()
    {
        Assert.Equal(FieldProblem.Unchanged, Planner.Set(_frank, Field("Born"), " ").Problem);
        Apply(Planner.Set(_frank, Field("Born"), "1920-10-08"));

        Apply(Planner.Set(_frank, Field("Born"), " "));

        Assert.Null(Field("Born").Value);
        Assert.Equal(FieldProblem.Unchanged, Planner.Clear(_frank, Field("Born")).Problem);
    }

    [Fact]
    public void ACheckbox_IsCheckedOrNot()
    {
        Apply(Planner.Check(_frank, Field("Read"), isChecked: true));

        Assert.Equal("true", Field("Read").Value);
        Assert.Equal(FieldProblem.Unchanged, Planner.Check(_frank, Field("Read"), isChecked: true).Problem);
    }

    [Fact]
    public void AChoice_IsOneOfItsOptions()
    {
        Apply(Planner.Set(_frank, Field("Genre"), "novel"));

        Assert.Equal("Novel", Field("Genre").Value);
    }

    [Fact]
    public void AnOwnField_IsAdded_WithItsFirstValue()
    {
        var plan = Planner.AddOwn(_frank, " Birthplace ", FieldKind.Text, "Tacoma");

        var value = Assert.IsType<FieldSet>(Assert.Single(plan.Changes));
        Assert.Equal((FieldSet.Own, "Birthplace", FieldKind.Text, "Tacoma"), (value.Owner, value.Name, value.Kind, value.Value));
        Apply(plan);
        Assert.True(Field("Birthplace").IsOwn);
    }

    [Fact]
    public void AnOwnField_NeedsAName_AValue_AndANameOfItsOwn()
    {
        Assert.Equal("Name the field.", Planner.AddOwn(_frank, " ", FieldKind.Text, "Tacoma").Status);
        Assert.Equal("Type a value", Planner.AddOwn(_frank, "Birthplace", FieldKind.Text, "").Status);
        Assert.Equal(FieldProblem.Invalid, Planner.AddOwn(_frank, "Died", FieldKind.Date, "soon").Problem);

        Apply(Planner.AddOwn(_frank, "Birthplace", FieldKind.Text, "Tacoma"));

        Assert.Equal("It has a field of its own with this name already.", Planner.AddOwn(_frank, "birthplace", FieldKind.Text, "Seattle").Status);
        Assert.True(Planner.AddOwn(_frank, "Born", FieldKind.Text, "Tacoma").CanApply);
    }

    [Fact]
    public void AnOwnField_IsNotAChoice()
    {
        Assert.DoesNotContain(FieldKind.Choice, FieldPlanner.OwnKinds);
        Assert.Throws<ArgumentOutOfRangeException>(() => Planner.AddOwn(_frank, "Genre", FieldKind.Choice, "Novel"));
    }

    [Fact]
    public void TwoFieldsOfTheSameName_AreMerged_KeepingOneValue()
    {
        Apply(Planner.Set(_frank, Field("Born"), "1920-10-08"));
        Apply(Planner.AddOwn(_frank, "Born", FieldKind.Text, "1920-10-09"));
        var (kept, dropped) = (Field("Born (Person)"), Field("Born (own)"));

        var plan = Planner.Merge(_frank, kept, dropped, "1920-10-09");

        Assert.Equal(2, plan.Changes.Count);
        Apply(plan);
        Assert.Equal("1920-10-09", Field("Born").Value);
        Assert.Single(NodeFields.Of(_graph, _frank.Id), field => field.Name == "Born");
    }

    [Fact]
    public void AMergedValue_MustFitTheFieldKept()
    {
        Apply(Planner.AddOwn(_frank, "Born", FieldKind.Text, "In 1920"));
        var (kept, dropped) = (Field("Born (Person)"), Field("Born (own)"));

        Assert.Equal("“In 1920” does not fit Born (Person).", Planner.Merge(_frank, kept, dropped, "In 1920").Status);
        Assert.Equal(FieldProblem.Invalid, Planner.Merge(_frank, Field("Genre"), dropped, "Poem").Problem);
    }
}
