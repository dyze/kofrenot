namespace Kofrenot.Core.Tests.Notes;

public sealed class MarkdownBlocksTests
{
    [Fact]
    public void EachLine_IsABlock_ItsKindFromItsStart()
    {
        var blocks = MarkdownBlocks.Parse("# Kitchen\r\nTo buy:\n\n- milk\n  * eggs\n- [ ] bread\n- [x] salt\n### Later");

        Assert.Equal(
            [BlockKind.Heading, BlockKind.Paragraph, BlockKind.Paragraph, BlockKind.Bullet, BlockKind.Bullet, BlockKind.Task, BlockKind.Task, BlockKind.Heading],
            blocks.Select(block => block.Kind));
        Assert.Equal([1, 0, 0, 0, 1, 0, 0, 3], blocks.Select(block => block.Level));
        Assert.Equal(["Kitchen", "To buy:", "", "milk", "eggs", "bread", "salt", "Later"], blocks.Select(block => string.Concat(block.Runs.Select(run => run.Text))));
        Assert.Empty(blocks[2].Runs);
        Assert.False(blocks[5].IsDone);
        Assert.True(blocks[6].IsDone);
    }

    [Theory]
    [InlineData("#Kitchen")]
    [InlineData("####### Seven")]
    [InlineData("-milk")]
    [InlineData("- ")]
    public void ALineNotMarked_IsAParagraph_AsTyped(string line)
    {
        var block = Assert.Single(MarkdownBlocks.Parse(line));

        Assert.Equal(BlockKind.Paragraph, block.Kind);
        Assert.Equal(line, string.Concat(block.Runs.Select(run => run.Text)));
    }

    [Fact]
    public void Emphasis_SplitsTheText()
    {
        Assert.Equal(
            [new TextRun("a "), new TextRun("bold", IsBold: true), new TextRun(" and "), new TextRun("italic", IsItalic: true), new TextRun(" end")],
            MarkdownBlocks.Runs("a **bold** and *italic* end"));
        Assert.Equal([new TextRun("both", IsBold: true, IsItalic: true)], MarkdownBlocks.Runs("***both***"));
    }

    [Theory]
    [InlineData("2 * 3 = 6")]
    [InlineData("**not closed")]
    [InlineData("****")]
    [InlineData("[no link]")]
    [InlineData("[](empty label)")]
    [InlineData("[label]()")]
    [InlineData("[label](two words)")]
    [InlineData("[label](not closed")]
    public void AMarkNotClosed_IsText(string text) =>
        Assert.Equal(text, string.Concat(MarkdownBlocks.Runs(text).Select(run => run.Text)));

    [Fact]
    public void ALink_KeepsItsUrl_AndTheEmphasisAroundIt()
    {
        Assert.Equal(
            [new TextRun("See "), new TextRun("the site", Link: "https://example.com"), new TextRun(".")],
            MarkdownBlocks.Runs("See [the site](https://example.com)."));
        Assert.Equal([new TextRun("here", IsBold: true, Link: "url")], MarkdownBlocks.Runs("**[here](url)**"));
    }
}
