namespace Kofrenot.Core.Tests.Graph;

public sealed class TypeCatalogTests
{
    [Fact]
    public void RelatesTo_IsBuiltIn_Symmetric_WithItsAlias()
    {
        var type = TypeCatalog.BuiltIn.FindLinkType("relates-to");

        Assert.NotNull(type);
        Assert.True(type.IsSymmetric);
        Assert.Equal("relates to", type.Name);
        Assert.Contains("related to", type.Phrases);
    }

    [Fact]
    public void AnUnknownLinkType_IsNotFound() =>
        Assert.Null(TypeCatalog.BuiltIn.FindLinkType("written-by"));

    [Fact]
    public void TheGenericNodeType_HasAStableId() =>
        Assert.Equal("generic", TypeCatalog.Generic.Id);

    [Fact]
    public void Generic_IsTheOnlyBuiltInNodeType() =>
        Assert.Same(TypeCatalog.Generic, Assert.Single(TypeCatalog.BuiltIn.NodeTypes));

    [Fact]
    public void ANodeType_IsFoundByItsId()
    {
        Assert.Same(TypeCatalog.Generic, TypeCatalog.BuiltIn.FindNodeType("generic"));
        Assert.Null(TypeCatalog.BuiltIn.FindNodeType("book"));
    }

    [Fact]
    public void AnUnknownType_IsNamedByItsId()
    {
        Assert.Equal("Generic", TypeCatalog.BuiltIn.NodeTypeName("generic"));
        Assert.Equal("book", TypeCatalog.BuiltIn.NodeTypeName("book"));
        Assert.Equal("relates to", TypeCatalog.BuiltIn.LinkTypeName("relates-to"));
        Assert.Equal("written-by", TypeCatalog.BuiltIn.LinkTypeName("written-by"));
    }
}
