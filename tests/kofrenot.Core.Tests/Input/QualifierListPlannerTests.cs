namespace Kofrenot.Core.Tests.Input;

/// <summary>The qualifiers a link type gives its links, the user's or a built-in one's.</summary>
public sealed class QualifierListPlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly LinkTypePlanner _planner;

    public QualifierListPlannerTests() => _planner = new LinkTypePlanner(_graph, _clock);

    private CustomLinkType Wrote()
    {
        _graph.Apply(_planner.Plan("wrote", "written by").Change!);
        return _graph.LinkTypes.All.Single();
    }

    [Fact]
    public void QualifiersGiven_ToALinkTypeOfTheUsers_KeepItsNames()
    {
        var wrote = Wrote();

        var plan = _planner.Qualifiers(wrote.Type, [new FieldDraft(null, " Date ", FieldKind.Date), new FieldDraft(null, "Cover", FieldKind.Image)]);
        _graph.Apply(plan.Change!);

        Assert.True(plan.CanApply);
        Assert.StartsWith("Changes the qualifiers of “wrote”", plan.Status);
        Assert.Equal(("wrote", "written by"), (plan.Change!.Name, plan.Change.ReverseName));
        Assert.Equal([wrote.Names[0].Id], plan.Change.Replaces);
        Assert.Equal(["Date", "Cover"], _graph.LinkTypes.FieldsOf(wrote.Type.Id).Select(field => field.Name));
        Assert.Equal("wrote", _graph.LinkTypes.All.Single().Type.Name);
    }

    [Fact]
    public void QualifiersGiven_ToABuiltInLinkType_WriteItsNames()
    {
        var plan = _planner.Qualifiers(TypeCatalog.Contains, [new FieldDraft(null, "Since", FieldKind.Date)]);

        Assert.Equal((TypeCatalog.Contains.Id, "contains", "contained by"), (plan.Change!.LinkType, plan.Change.Name, plan.Change.ReverseName));
        Assert.Empty(plan.Change.Replaces);
        _graph.Apply(plan.Change);
        Assert.Equal("Since", Assert.Single(_graph.LinkTypes.FieldsOf(TypeCatalog.Contains.Id)).Name);
    }

    [Fact]
    public void NoQualifier_ForATypeWithout_ChangesNothing() =>
        Assert.False(_planner.Qualifiers(TypeCatalog.RelatesTo, []).CanApply);

    [Fact]
    public void TheSameQualifiers_ChangeNothing()
    {
        var wrote = Wrote();
        _graph.Apply(_planner.Qualifiers(wrote.Type, [new FieldDraft(null, "Date", FieldKind.Date)]).Change!);
        var date = _graph.LinkTypes.FieldsOf(wrote.Type.Id).Single();

        var plan = _planner.Qualifiers(wrote.Type, [FieldDraft.Of(date)]);

        Assert.False(plan.CanApply);
        Assert.Equal(string.Empty, plan.Status);
    }

    [Theory]
    [InlineData(" ", FieldKind.Date, "Name every qualifier.")]
    [InlineData("Date", FieldKind.Date, "Two qualifiers are named “Date”.")]
    [InlineData("Format", FieldKind.Choice, "Give “Format” its options, each once.")]
    public void BadQualifiers_AreRefused(string name, FieldKind kind, string status)
    {
        var plan = _planner.Qualifiers(TypeCatalog.RelatesTo, [new FieldDraft(null, "Date", FieldKind.Date), new FieldDraft(null, name, kind)]);

        Assert.False(plan.CanApply);
        Assert.Equal(status, plan.Status);
    }

    [Fact]
    public void ARename_KeepsTheQualifiers()
    {
        var wrote = Wrote();
        _graph.Apply(_planner.Qualifiers(wrote.Type, [new FieldDraft(null, "Date", FieldKind.Date)]).Change!);

        var rename = _planner.Rename(_graph.LinkTypes.All.Single(), "authored", "authored by").Change!;
        _graph.Apply(rename);

        Assert.Equal("Date", Assert.Single(rename.Fields).Name);
        Assert.Equal("authored", _graph.LinkTypes.All.Single().Type.Name);
    }
}
