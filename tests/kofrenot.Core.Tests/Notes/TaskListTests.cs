namespace Kofrenot.Core.Tests.Notes;

public sealed class TaskListTests
{
    [Theory]
    [InlineData("- [ ] one\n- [x] two", true)]
    [InlineData("- [ ] one\n\n- [X] two\n", true)]
    [InlineData("- [ ]", true)]
    [InlineData("- [ ] one\ntext", false)]
    [InlineData("- one", false)]
    [InlineData("- [ ]text", false)]
    [InlineData("", false)]
    [InlineData(" \n", false)]
    public void IsList_WhenEveryFilledLineIsAnItem(string text, bool isList) =>
        Assert.Equal(isList, TaskList.IsList(text));

    [Fact]
    public void Parse_GivesTheItems_AndLeavesOtherLinesOut() =>
        Assert.Equal(
            [new TaskItem("one", false), new TaskItem("two", true), new TaskItem(string.Empty, false), new TaskItem(" indented", false)],
            TaskList.Parse("# title\r\n- [ ] one\r\n- [x] two\r\n- [ ] \r\n- [ ]  indented"));

    [Fact]
    public void Format_WritesOneLinePerItem() =>
        Assert.Equal("- [ ] one\r\n- [x] two", TaskList.Format([new TaskItem("one", false), new TaskItem("two", true)], "\r\n"));

    [Fact]
    public void Format_ThenParse_GivesTheItemsBack()
    {
        TaskItem[] items = [new("one", true), new(string.Empty, false), new("three", false)];

        Assert.Equal(items, TaskList.Parse(TaskList.Format(items)));
    }

    [Fact]
    public void Show_TurnsEachLineIntoAnItem_KeepsItems_AndDropsBlankLines() =>
        Assert.Equal("- [ ] one\n- [x] two\n- [ ] three", TaskList.Show("one\n- [x] two\n\nthree\n"));

    [Fact]
    public void Show_KeepsTheLineBreaks() =>
        Assert.Equal("- [ ] one\r\n- [ ] two", TaskList.Show("one\r\ntwo"));

    [Theory]
    [InlineData("")]
    [InlineData("\n \n")]
    public void Show_OnAnEmptyText_GivesOneEmptyItem(string text) =>
        Assert.Equal("- [ ] ", TaskList.Show(text));

    [Fact]
    public void Hide_TurnsItemsIntoTheirText_AndKeepsOtherLines() =>
        Assert.Equal("# title\r\none\r\ntwo\r\n", TaskList.Hide("# title\r\n- [ ] one\r\n- [x] two\r\n"));

    [Fact]
    public void Show_ThenHide_GivesTheLinesBack() =>
        Assert.Equal("one\ntwo", TaskList.Hide(TaskList.Show("one\ntwo")));

    private static readonly TaskItem[] Abcd = [new("a", false), new("b", false), new("c", true), new("d", false)];

    [Theory]
    [InlineData(0, 2, "bcad")]
    [InlineData(3, 0, "dabc")]
    [InlineData(1, 3, "acdb")]
    [InlineData(2, 2, "abcd")]
    public void Move_PutsTheItemAtItsNewPlace_KeepingTheOthersInOrder(int from, int to, string order)
    {
        var moved = TaskList.Move(Abcd, from, to);

        Assert.Equal(order, string.Concat(moved.Select(item => item.Text)));
        Assert.True(moved.Single(item => item.Text == "c").IsDone);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 4)]
    public void Move_RefusesAnIndexNotInTheList(int from, int to) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskList.Move(Abcd, from, to));

    [Fact]
    public void ProgressOf_CountsTheItemsChecked() =>
        Assert.Equal(new TaskProgress(1, 3), TaskList.ProgressOf("text\n- [ ] one\n- [x] two\n- [ ] three"));

    [Fact]
    public void ProgressOf_ATextWithoutItems_IsNone() =>
        Assert.Equal(new TaskProgress(0, 0), TaskList.ProgressOf("text"));

    [Theory]
    [InlineData("a\r\nb", "\r\n")]
    [InlineData("a\nb", "\n")]
    [InlineData("a", "\n")]
    public void LineBreakOf_IsTheOneTheTextUses(string text, string lineBreak) =>
        Assert.Equal(lineBreak, TaskList.LineBreakOf(text));
}
