namespace Kofrenot.Core.Tests.Notes;

public sealed class MarkdownFormatTests
{
    [Fact]
    public void Emphasis_WrapsTheSelection_AndKeepsItSelected() =>
        Assert.Equal(new TextEdit("a **bold** word", 4, 4), MarkdownFormat.Emphasis(new TextEdit("a bold word", 2, 4), "**"));

    [Fact]
    public void Emphasis_WithoutSelection_PutsTheCaretBetweenTheMarks() =>
        Assert.Equal(new TextEdit("say **", 5, 0), MarkdownFormat.Emphasis(new TextEdit("say ", 4, 0), "*"));

    [Fact]
    public void LinePrefix_AddsToTheCaretLine() =>
        Assert.Equal(new TextEdit("one\n# two\nthree", 4, 5), MarkdownFormat.LinePrefix(new TextEdit("one\ntwo\nthree", 5, 0), "# "));

    [Fact]
    public void LinePrefix_AddsToEachSelectedLine_ButNotTheLineAfterAFinalBreak() =>
        Assert.Equal(
            new TextEdit("- one\n- two\nthree", 0, 11),
            MarkdownFormat.LinePrefix(new TextEdit("one\ntwo\nthree", 0, 8), "- "));

    [Fact]
    public void LinePrefix_IsRemoved_WhenEveryLineHasIt() =>
        Assert.Equal(new TextEdit("one\ntwo", 0, 7), MarkdownFormat.LinePrefix(new TextEdit("- one\n- two", 2, 7), "- "));

    [Theory]
    [InlineData("", 0)]
    [InlineData("last", 4)]
    public void LinePrefix_WorksOnAnEmptyText_AndTheLastLine(string text, int caret) =>
        Assert.Equal("# " + text, MarkdownFormat.LinePrefix(new TextEdit(text, caret, 0), "# ").Text);

    [Fact]
    public void Link_WrapsTheSelection_AndSelectsTheUrl()
    {
        var edit = MarkdownFormat.Link(new TextEdit("see docs here", 4, 4));

        Assert.Equal("see [docs](url) here", edit.Text);
        Assert.Equal(MarkdownFormat.LinkPlaceholder, edit.Text.Substring(edit.Start, edit.Length));
    }
}
