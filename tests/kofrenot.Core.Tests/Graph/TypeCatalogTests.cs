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
}
