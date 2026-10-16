namespace Kofrenot.Core.Tests.Graph;

public sealed class NameKeyTests
{
    [Theory]
    [InlineData("Dune", "dune")]
    [InlineData("  Frank   Herbert ", "frank herbert")]
    [InlineData("Émile Zola", "emile zola")]
    [InlineData("Ça\tva", "ca va")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void IgnoresCaseAccentsAndExtraSpaces(string name, string expected) =>
        Assert.Equal(expected, NameKey.Of(name));

    [Fact]
    public void TwoSpellingsOfOneName_HaveTheSameKey() =>
        Assert.Equal(NameKey.Of("ÉMILE ZOLA"), NameKey.Of("emile  zola"));
}
