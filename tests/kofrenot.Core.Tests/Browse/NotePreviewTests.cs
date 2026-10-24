namespace Kofrenot.Core.Tests.Browse;

public sealed class NotePreviewTests
{
    [Fact]
    public void AText_ShowsItsFilledLines_AsTyped()
    {
        var preview = NotePreview.Of("# Weekend\r\n\r\nAsk Paul\r\n");

        Assert.Equal(["# Weekend", "Ask Paul"], preview.Lines);
        Assert.Empty(preview.Items);
        Assert.Equal((0, false), (preview.CheckedCount, preview.HasMore));
    }

    [Fact]
    public void ALongText_ShowsItsFirstLines_AndSaysThereIsMore()
    {
        var preview = NotePreview.Of(string.Join('\n', Enumerable.Range(1, NotePreview.MaxLines + 1)));

        Assert.Equal(NotePreview.MaxLines, preview.Lines.Count);
        Assert.Equal("1", preview.Lines[0]);
        Assert.True(preview.HasMore);
    }

    [Fact]
    public void ALongLine_IsCutShort() =>
        Assert.Equal(new string('a', NotePreview.MaxLineLength) + "…", NotePreview.Of(new string('a', NotePreview.MaxLineLength + 5)).Lines.Single());

    [Fact]
    public void ATaskList_ShowsItsUncheckedItems_AndCountsTheOthers()
    {
        var preview = NotePreview.Of("- [ ] Office appliances\n- [x] Kitchen\n- [ ] Stationery\n- [x] Tidbits");

        Assert.Empty(preview.Lines);
        Assert.Equal(["Office appliances", "Stationery"], preview.Items);
        Assert.Equal((2, false), (preview.CheckedCount, preview.HasMore));
    }

    [Fact]
    public void ALongTaskList_SaysThereIsMore() =>
        Assert.True(NotePreview.Of(string.Join('\n', Enumerable.Range(1, NotePreview.MaxLines + 1).Select(n => $"- [ ] {n}"))).HasMore);

    [Fact]
    public void AnEmptyNote_ShowsNothing()
    {
        var preview = NotePreview.Of(string.Empty);

        Assert.Empty(preview.Lines);
        Assert.Empty(preview.Items);
    }
}
