namespace Kofrenot.Core.Tests.Graph;

public sealed class FieldValuesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _frank = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Frank Herbert", Start);

    public FieldValuesTests() => _graph.Add(_frank);

    private FieldSet Set(string field, string? value, int minutes, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), _frank.Id, "person", field, field, FieldKind.Text, value, replaces);

    [Fact]
    public void ANode_HasNoFieldSet_UntilOneIs()
    {
        Assert.Empty(_graph.FieldsOf(_frank.Id));
        Assert.Empty(_graph.FieldSetsOf(_frank.Id));
    }

    [Fact]
    public void AValue_IsKept_AndAppliedOnce()
    {
        var born = Set("born", "1920", 1);

        Assert.True(_graph.Apply(born));
        Assert.False(_graph.Apply(born));

        var field = Assert.Single(_graph.FieldsOf(_frank.Id));
        Assert.Equal(("person", "born", born), (field.Owner, field.Field, field.Shown));
        Assert.False(field.IsConflict);
        Assert.Equal([born], _graph.FieldSetsOf(_frank.Id));
    }

    [Fact]
    public void ALaterValue_ReplacesTheEarlierOne_OfTheSameField()
    {
        var born = Set("born", "1920", 1);
        _graph.Apply(born);
        _graph.Apply(Set("died", "1986", 2));
        var fixedBorn = Set("born", "1920-10-08", 3, born.Id);
        _graph.Apply(fixedBorn);

        Assert.Equal([("born", "1920-10-08"), ("died", "1986")], _graph.FieldsOf(_frank.Id).Select(field => (field.Field, field.Shown.Value)));
        Assert.Single(Assert.Single(_graph.FieldsOf(_frank.Id), field => field.Field == "born").Current);
    }

    [Fact]
    public void ValuesMadeApart_AreAConflict_ShowingTheLatest()
    {
        var born = Set("born", "1920", 1);
        _graph.Apply(born);
        _graph.Apply(Set("born", "1921", 3, born.Id));
        _graph.Apply(Set("born", "1922", 2, born.Id));

        var field = Assert.Single(_graph.FieldsOf(_frank.Id));
        Assert.True(field.IsConflict);
        Assert.Equal(["1922", "1921"], field.Current.Select(value => value.Value));
        Assert.Equal("1921", field.Shown.Value);
    }

    [Fact]
    public void AClearedValue_IsAVersionWithoutValue()
    {
        var born = Set("born", "1920", 1);
        _graph.Apply(born);
        _graph.Apply(Set("born", null, 2, born.Id));

        Assert.Null(Assert.Single(_graph.FieldsOf(_frank.Id)).Shown.Value);
    }

    [Fact]
    public void AnOwnField_IsApartFromATypesFieldOfTheSameId()
    {
        _graph.Apply(Set("born", "1920", 1));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start.AddMinutes(2), _frank.Id, FieldSet.Own, "born", "Born", FieldKind.Text, "Tacoma", []));

        Assert.Equal([("person", "1920"), (FieldSet.Own, "Tacoma")], _graph.FieldsOf(_frank.Id).Select(field => (field.Owner, field.Shown.Value)));
    }

    [Fact]
    public void AValue_ChangesWhenTheNodeLastChanged()
    {
        var born = Set("born", "1920", 30);
        _graph.Apply(born);

        Assert.Equal(born.At, NodeFacts.ModifiedOf(_graph, _frank));
    }
}
