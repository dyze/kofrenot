namespace Kofrenot.Core.Tests.Input;

public sealed class InputParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyText_IsNoRequest(string text) =>
        Assert.Null(InputParser.Parse(text));

    [Theory]
    [InlineData("Dune", "Dune")]
    [InlineData("  John   Doe ", "John Doe")]
    [InlineData("relates to", "relates to")]
    [InlineData("relates to Dune", "relates to Dune")]
    [InlineData("Dune relates", "Dune relates")]
    [InlineData("Dune relatesto Herbert", "Dune relatesto Herbert")]
    public void TextWithoutALinkPhraseAfterAWord_IsANode(string text, string name) =>
        Assert.Equal(new NodeRequest(name), InputParser.Parse(text));

    [Theory]
    [InlineData("Dune relates to Frank Herbert", "Dune", "Frank Herbert")]
    [InlineData("Apple related to red", "Apple", "red")]
    [InlineData("Apple  RELATED   To  red", "Apple", "red")]
    [InlineData("The Lord of the Rings relates to J. R. R. Tolkien", "The Lord of the Rings", "J. R. R. Tolkien")]
    [InlineData("A relates to B relates to C", "A", "B relates to C")]
    public void TextWithALinkPhrase_IsALink(string text, string left, string right) =>
        Assert.Equal(new LinkRequest(left, TypeCatalog.RelatesTo, right), InputParser.Parse(text));

    [Fact]
    public void ALinkBeingTyped_HasAnEmptyRightSide() =>
        Assert.Equal(new LinkRequest("Dune", TypeCatalog.RelatesTo, ""), InputParser.Parse("Dune relates to "));

    [Fact]
    public void AtOnePlace_TheLongestPhraseWins()
    {
        var short_ = new LinkType("near", ["near"], IsSymmetric: true);
        var long_ = new LinkType("near-to", ["near to"], IsSymmetric: true);

        var request = InputParser.Parse("Paris near to London", new TypeCatalog([short_, long_]));

        Assert.Equal(new LinkRequest("Paris", long_, "London"), request);
    }

    [Fact]
    public void ThePhraseNearestTheStart_Wins()
    {
        var near = new LinkType("near", ["near"], IsSymmetric: true);
        var catalog = new TypeCatalog([TypeCatalog.RelatesTo, near]);

        var request = InputParser.Parse("Paris near Rome relates to Italy", catalog);

        Assert.Equal(new LinkRequest("Paris", near, "Rome relates to Italy"), request);
    }
}
