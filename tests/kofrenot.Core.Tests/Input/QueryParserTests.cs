namespace Kofrenot.Core.Tests.Input;

public sealed class QueryParserTests
{
    private static readonly TypeCatalog Types = TypeCatalog.BuiltIn;
    private static readonly LinkVerb ContainedBy = new(TypeCatalog.Contains, IsReverse: true);
    private static readonly LinkVerb Contains = new(TypeCatalog.Contains, IsReverse: false);

    [Theory]
    [InlineData("?note:", "")]
    [InlineData("  ? Note :  Ide ", "Ide")]
    public void ATypesName_AndAColon_ListsItsNodes_ByTheNameAfter(string text, string name) =>
        Assert.Equal(new NodeQuery(TypeCatalog.Note, name, null, null), QueryParser.Parse(text, Types));

    [Fact]
    public void ALinkPhrase_ListsTheNodesLinkedSo()
    {
        Assert.Equal(new NodeQuery(null, "", new LinkQuery(ContainedBy, "Kitchen"), null), QueryParser.Parse("?contained by  Kitchen", Types));
        Assert.Equal(new NodeQuery(null, "", new LinkQuery(Contains, ""), null), QueryParser.Parse("?Contains", Types));
    }

    [Fact]
    public void ATimesWord_AndAColon_ListsTheNodesByWhen()
    {
        Assert.Equal(new NodeQuery(null, "", null, new TimeQuery(TimeKind.Changed, "this week")), QueryParser.Parse("?changed: this week", Types));
        Assert.Equal(new NodeQuery(null, "", null, new TimeQuery(TimeKind.Opened, "")), QueryParser.Parse("?Opened:", Types));
    }

    [Fact]
    public void AType_NarrowsALinkOrATime()
    {
        Assert.Equal(new NodeQuery(TypeCatalog.Note, "", new LinkQuery(Contains, "Knives"), null), QueryParser.Parse("?note: contains Knives", Types));
        Assert.Equal(new NodeQuery(TypeCatalog.Note, "", null, new TimeQuery(TimeKind.Added, "2025")), QueryParser.Parse("?note:added: 2025", Types));
    }

    [Fact]
    public void ATimesWord_WinsOverATypeOfThatName()
    {
        var changed = new NodeType("changed", "Changed");
        var types = new TypeCatalog([TypeCatalog.Generic, changed], [TypeCatalog.Contains]);

        Assert.Equal(new NodeQuery(null, "", null, new TimeQuery(TimeKind.Changed, "today")), QueryParser.Parse("?changed: today", types));
    }

    [Theory]
    [InlineData("?", true)]
    [InlineData("  ?  ", true)]
    [InlineData("?Dune", false)]
    public void AnythingElse_AfterTheMark_IsAName(string text, bool empty)
    {
        var query = QueryParser.Parse(text, Types)!;

        Assert.Equal(text.Trim()[1..].Trim(), query.Name);
        Assert.Equal(empty, query.IsEmpty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Dune")]
    [InlineData("note:")]
    [InlineData("contained by Kitchen")]
    [InlineData("Star Wars: Episode IV")]
    [InlineData("Dune?")]
    public void WithoutTheMarkFirst_ItIsNoQuery(string text)
    {
        Assert.Null(QueryParser.Parse(text, Types));
        Assert.False(QueryParser.IsQuery(text));
    }
}
