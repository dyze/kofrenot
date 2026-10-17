namespace Kofrenot.Core.Tests.Graph;

public sealed class TypeCatalogTests
{
    [Fact]
    public void RelatesTo_IsBuiltIn_Symmetric_WithItsAlias()
    {
        var type = TypeCatalog.BuiltIn.FindLinkType("relates-to");

        Assert.NotNull(type);
        Assert.True(type.IsSymmetric);
        Assert.Null(type.Reverse);
        Assert.Equal("relates to", type.Name);
        Assert.Contains("related to", type.Phrases);
    }

    [Theory]
    [InlineData("contains", "contains", "contained by")]
    [InlineData("is-a", "is a", "has instance")]
    public void DirectedTypes_AreBuiltIn_WithTheirReverse(string id, string name, string reverse)
    {
        var type = TypeCatalog.BuiltIn.FindLinkType(id);

        Assert.NotNull(type);
        Assert.False(type.IsSymmetric);
        Assert.Equal((name, reverse), (type.Forward.Name, type.Reverse!.Name));
    }

    [Fact]
    public void IsA_HasTheAlias_IsAn() =>
        Assert.Equal(["is a", "is an"], TypeCatalog.IsA.Forward.Phrases);

    [Fact]
    public void Verbs_ListEachTypeForward_ThenReverse_UnlessSymmetric() =>
        Assert.Equal(
            ["relates to", "contains", "contained by", "is a", "has instance"],
            TypeCatalog.BuiltIn.Verbs.Select(verb => verb.Name));

    [Fact]
    public void TypesAndVerbs_AreNamedByTheirName()
    {
        Assert.Equal("contains", TypeCatalog.Contains.ToString());
        Assert.Equal("contained by", TypeCatalog.Contains.Reverse!.ToString());
    }

    [Fact]
    public void AVerb_IsEqualToTheSameWayOfTheSameType()
    {
        Assert.Equal(TypeCatalog.Contains.Reverse, new LinkVerb(TypeCatalog.Contains, IsReverse: true));
        Assert.NotEqual(TypeCatalog.Contains.Forward, TypeCatalog.Contains.Reverse);
    }

    [Fact]
    public void ALink_ReadsForwardFromItsFromNode_AndReverseFromItsToNode()
    {
        var (box, room) = (Guid.NewGuid(), Guid.NewGuid());
        var link = new Link(Guid.NewGuid(), "contains", room, box, DateTimeOffset.UnixEpoch);

        Assert.Equal("contains", TypeCatalog.BuiltIn.LinkName(link, room));
        Assert.Equal("contained by", TypeCatalog.BuiltIn.LinkName(link, box));
    }

    [Fact]
    public void ASymmetricLink_ReadsTheSameFromBothNodes()
    {
        var (dune, herbert) = (Guid.NewGuid(), Guid.NewGuid());
        var link = new Link(Guid.NewGuid(), "relates-to", dune, herbert, DateTimeOffset.UnixEpoch);

        Assert.Equal("relates to", TypeCatalog.BuiltIn.LinkName(link, dune));
        Assert.Equal("relates to", TypeCatalog.BuiltIn.LinkName(link, herbert));
    }

    [Fact]
    public void AnUnknownLinkType_IsNotFound_AndNamedByItsId()
    {
        var link = new Link(Guid.NewGuid(), "written-by", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch);

        Assert.Null(TypeCatalog.BuiltIn.FindLinkType("written-by"));
        Assert.Equal("written-by", TypeCatalog.BuiltIn.LinkName(link, link.To));
    }

    [Fact]
    public void TheGenericNodeType_HasAStableId() =>
        Assert.Equal("generic", TypeCatalog.Generic.Id);

    [Fact]
    public void GenericAndNote_AreTheBuiltInNodeTypes() =>
        Assert.Equal([("generic", "Generic"), ("note", "Note")], TypeCatalog.BuiltIn.NodeTypes.Select(type => (type.Id, type.Name)));

    [Fact]
    public void ANodeType_IsFoundByItsId()
    {
        Assert.Same(TypeCatalog.Generic, TypeCatalog.BuiltIn.FindNodeType("generic"));
        Assert.Null(TypeCatalog.BuiltIn.FindNodeType("book"));
    }

    [Fact]
    public void AnUnknownNodeType_IsNamedByItsId()
    {
        Assert.Equal("Generic", TypeCatalog.BuiltIn.NodeTypeName("generic"));
        Assert.Equal("book", TypeCatalog.BuiltIn.NodeTypeName("book"));
    }
}
