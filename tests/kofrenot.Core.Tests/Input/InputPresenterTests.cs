namespace Kofrenot.Core.Tests.Input;

public sealed class InputPresenterTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly InputPresenter _input = new();

    [Fact]
    public void AnEmptyText_ShowsTheHint()
    {
        _input.Update(_graph, "  ");

        Assert.Null(_input.Request);
        Assert.Null(_input.Plan);
        Assert.Equal(InputPresenter.Hint, _input.Preview);
        Assert.Empty(_input.Rows);
    }

    [Fact]
    public void ANewName_SaysEnterAddsIt()
    {
        _input.Update(_graph, "Dune");

        Assert.Equal(new NodeRequest("Dune"), _input.Request);
        Assert.Equal("Enter to add the node “Dune”.", _input.Preview);
    }

    [Fact]
    public void AnExistingName_IsSaidToExist_AndListedWithItsLinks()
    {
        var dune = Add("Dune");
        var herbert = Add("Frank Herbert");
        Link(dune, herbert);
        var messiah = Add("Dune Messiah");

        _input.Update(_graph, "dune");

        Assert.Equal("“dune” exists already. Enter to add another one.", _input.Preview);
        Assert.Equal(
            [new ResultRow("Dune", false, false, dune), new ResultRow("relates to Frank Herbert", true, false, herbert, dune), new ResultRow("Dune Messiah", false, false, messiah)],
            _input.Rows);
    }

    [Fact]
    public void DownSelectsANode_OrALink_AndEnterWouldOpenItsDetails()
    {
        var dune = Add("Dune");
        var herbert = Add("Frank Herbert");
        Link(dune, herbert);
        var messiah = Add("Dune Messiah");
        _input.Update(_graph, "dune");

        Assert.Null(_input.SelectedRow?.Node);
        Assert.True(_input.MoveSelection(1));
        Assert.Same(dune, _input.SelectedRow?.Node);
        Assert.True(_input.Rows[0].IsChosen);
        Assert.Equal("Enter to see the details of “Dune”. Up/Down: another one.", _input.Preview);

        Assert.True(_input.MoveSelection(1));
        Assert.Equal((herbert, dune), (_input.SelectedRow?.Node, _input.SelectedRow?.From));
        Assert.True(_input.Rows[1].IsChosen);
        Assert.False(_input.Rows[0].IsChosen);
        Assert.Equal("Enter to see the details of “Frank Herbert”. Up/Down: another one.", _input.Preview);

        Assert.True(_input.MoveSelection(1));
        Assert.Same(messiah, _input.SelectedRow?.Node);
        Assert.False(_input.MoveSelection(1));
        Assert.Same(messiah, _input.SelectedRow?.Node);
    }

    [Fact]
    public void UpFromTheFirstNode_ClearsTheSelection()
    {
        Add("Dune");
        _input.Update(_graph, "Dune");

        Assert.False(_input.MoveSelection(-1));
        _input.MoveSelection(1);
        Assert.True(_input.MoveSelection(-1));

        Assert.Null(_input.SelectedRow?.Node);
        Assert.StartsWith("“Dune” exists already.", _input.Preview);
    }

    [Fact]
    public void TheSelection_IsKeptForTheSameText_AndClearedByTyping()
    {
        var dune = Add("Dune");
        _input.Update(_graph, "Dune");
        _input.MoveSelection(1);

        _input.Update(_graph, "Dune");
        Assert.Same(dune, _input.SelectedRow?.Node);

        _input.Update(_graph, "Dun");
        Assert.Null(_input.SelectedRow?.Node);
    }

    [Fact]
    public void NothingToSelect_WithoutNodesFound_OrWhileTypingALink()
    {
        Add("Dune");

        Assert.False(_input.MoveSelection(1));
        _input.Update(_graph, "Arrakis");
        Assert.False(_input.MoveSelection(1));
        _input.Update(_graph, "Dune relates to Arrakis");
        Assert.False(_input.MoveSelection(1));
    }

    [Fact]
    public void Homonyms_AreCounted_AndMarked()
    {
        Add("Dune", minutes: 1);
        Add("Dune", minutes: 2);

        _input.Update(_graph, "Dune");

        Assert.Equal("“Dune” exists already (2 times). Enter to add another one.", _input.Preview);
        Assert.Equal(["Dune¹", "Dune²"], _input.Rows.Select(row => row.Text));
    }

    [Fact]
    public void ALinkBeingTyped_AsksForItsSecondNode()
    {
        _input.Update(_graph, "Dune relates to");

        Assert.Equal("Type the node to link “Dune” to.", _input.Preview);
    }

    [Fact]
    public void ALinkToItself_IsRefused()
    {
        _input.Update(_graph, "Dune relates to dune");

        Assert.Equal("A node cannot be linked to itself.", _input.Preview);
    }

    [Fact]
    public void AnExistingLink_IsSaidToExist()
    {
        Link(Add("Dune"), Add("Frank Herbert"));

        _input.Update(_graph, "Frank Herbert relates to Dune");

        Assert.Equal("“Frank Herbert” relates to “Dune” already.", _input.Preview);
    }

    [Fact]
    public void ANewLink_SaysWhatEnterAdds_AndSearchesTheSecondNode()
    {
        Add("Dune");
        Add("Frank Herbert");

        _input.Update(_graph, "Dune relates to Fra");

        Assert.Equal("Enter to add the node “Fra”, the link “Dune relates to Fra”.", _input.Preview);
        Assert.Equal(["Frank Herbert"], _input.Rows.Select(row => row.Text));
    }

    [Fact]
    public void AmongHomonyms_UpDownChoose_AndTheChoiceIsKept()
    {
        Add("Dune", minutes: 1);
        Add("Dune", minutes: 2);
        _input.Update(_graph, "Arrakis relates to Dune");

        Assert.Contains("Up/Down: which “Dune”. Tab: the other side.", _input.Preview);
        Assert.Equal([true, false], _input.Rows.Select(row => row.IsChosen));

        Assert.True(_input.MoveChoice(1));
        Assert.Equal(new AddChoices(0, 1), _input.Choices);
        Assert.Equal([false, true], _input.Rows.Select(row => row.IsChosen));
        Assert.Contains("“Arrakis relates to Dune²”", _input.Preview);

        _input.Update(_graph, "arrakis relates to dune");
        Assert.Equal(1, _input.Choices.Right);

        Assert.True(_input.MoveChoice(1));
        Assert.Equal(0, _input.Choices.Right);
    }

    [Fact]
    public void AnotherName_ResetsTheChoice()
    {
        Add("Dune", minutes: 1);
        Add("Dune", minutes: 2);
        _input.Update(_graph, "Arrakis relates to Dune");
        _input.MoveChoice(1);

        _input.Update(_graph, "Arrakis relates to Dun");
        _input.Update(_graph, "Arrakis relates to Dune");

        Assert.Equal(AddChoices.Default, _input.Choices);
    }

    [Fact]
    public void Tab_SwitchesTheSideSearched_AndChosen()
    {
        Add("Dune", minutes: 1);
        Add("Dune", minutes: 2);
        Add("Arrakis");
        _input.Update(_graph, "Dune relates to Arrakis");

        Assert.Equal(["Arrakis"], _input.Rows.Select(row => row.Text));
        Assert.True(_input.SwitchSide());
        Assert.Equal(["Dune¹", "Dune²"], _input.Rows.Where(row => !row.IsLink).Select(row => row.Text));

        Assert.True(_input.MoveChoice(-1));
        Assert.Equal(new AddChoices(1, 0), _input.Choices);
    }

    [Fact]
    public void WithoutAChoiceToMake_UpDownAndTabDoNothing()
    {
        Assert.False(_input.MoveChoice(1));
        Assert.False(_input.SwitchSide());

        _input.Update(_graph, "Dune");
        Assert.False(_input.MoveChoice(1));
        Assert.False(_input.SwitchSide());

        _input.Update(_graph, "Dune relates to Arrakis");
        Assert.False(_input.MoveChoice(1));
    }

    [Fact]
    public void ALinkOfAnUnknownType_IsListedByItsId()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        Link(dune, herbert, type: "written-by");

        _input.Update(_graph, "Dune");

        Assert.Contains(_input.Rows, row => row is { Text: "written-by Frank Herbert", IsLink: true });
    }

    private Node Add(string name, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start.AddMinutes(minutes));
        _graph.Add(node);
        return node;
    }

    private void Link(Node from, Node to, string type = "relates-to") =>
        _graph.Add(new Link(Guid.NewGuid(), type, from.Id, to.Id, Start));
}
