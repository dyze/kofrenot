namespace Kofrenot.Core.Tests.Input;

public sealed class ChipInputTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ChipInput _input = new();

    [Fact]
    public void ANewInput_IsEmpty()
    {
        Assert.True(_input.IsEmpty);
        Assert.Equal(string.Empty, _input.Text);
        Assert.Empty(_input.Chips(_graph, plan: null));
        Assert.False(_input.Backspace());
    }

    [Fact]
    public void ALinkTypeTypedWithoutASpace_StaysText()
    {
        Assert.False(TypeEach("obj1 is a"));

        Assert.Equal("obj1 is a", _input.Tail);
        Assert.Empty(_input.Chips(_graph, plan: null));
    }

    [Fact]
    public void ASpaceAfterALinkType_MakesTheFirstNodeAndTheLinkTypeLabels()
    {
        Assert.True(TypeEach("obj1 is a "));

        Assert.Equal(string.Empty, _input.Tail);
        Assert.Equal("obj1 is a ", _input.Text);
        Assert.Equal([new InputChip("obj1", false, IsNew: true), new InputChip("is a", true)], _input.Chips(_graph, plan: null));
    }

    [Fact]
    public void APastedLink_KeepsTheSecondNodeAsText_AndMultiWordPartsAsTyped()
    {
        Assert.True(_input.Type(_graph, "The   old map  Contained By  Kitchen drawer "));

        Assert.Equal("Kitchen drawer ", _input.Tail);
        Assert.Equal(["The old map", "Contained By"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.Equal(new LinkRequest("The old map", new LinkVerb(TypeCatalog.Contains, IsReverse: true), "Kitchen drawer"), InputParser.Parse(_input.Text));
    }

    [Fact]
    public void AnEditInsideTheText_MakesNoLabel()
    {
        _input.Type(_graph, "obj1  a ");

        Assert.False(_input.Type(_graph, "obj1 is a "));
        Assert.Empty(_input.Chips(_graph, plan: null));
        Assert.Null(_input.Proposal(_graph));
    }

    [Fact]
    public void Backspace_TurnsTheLastLabelBackIntoText_OneByOne()
    {
        TypeEach("obj1 is a ");

        Assert.True(_input.Backspace());
        Assert.Equal("is a", _input.Tail);
        Assert.Equal("obj1 is a", _input.Text);
        Assert.False(_input.Backspace());

        _input.Type(_graph, string.Empty);
        Assert.True(_input.Backspace());
        Assert.Equal("obj1", _input.Tail);
        Assert.False(_input.IsEmpty);
        _input.Type(_graph, string.Empty);
        Assert.True(_input.IsEmpty);
    }

    [Fact]
    public void ALabelTurnedBackIntoText_FormsAgainWithTheNextSpace()
    {
        TypeEach("obj1 is a ");
        _input.Backspace();

        Assert.DoesNotContain(_input.Chips(_graph, plan: null), chip => chip.IsLinkType);
        Assert.True(_input.Type(_graph, "is an "));
        Assert.Equal("is an", _input.Chips(_graph, plan: null)[1].Text);
    }

    [Fact]
    public void AProposal_ShowsOnlyWhileTypingAtTheEnd()
    {
        Add("obj2");
        TypeEach("obj1 is a o");

        Assert.Equal("bj2", _input.Proposal(_graph)?.Rest);
        _input.Type(_graph, string.Empty);
        Assert.Null(_input.Proposal(_graph));
        _input.Type(_graph, "o");
        Assert.Equal("bj2", _input.Proposal(_graph)?.Rest);
    }

    [Fact]
    public void TheSameTextTypedAgain_KeepsTheProposal()
    {
        Add("Dune");
        TypeEach("dun");

        Assert.False(_input.Type(_graph, "dun"));

        Assert.Equal("Dune", _input.Proposal(_graph)?.Label);
    }

    [Fact]
    public void AcceptingALinkType_MakesItAndTheFirstNodeLabels()
    {
        TypeEach("Old map con");

        Assert.True(_input.Accept(_graph));

        Assert.Equal(["Old map", "contains"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.Equal(string.Empty, _input.Tail);
        Assert.Null(_input.Proposal(_graph));
    }

    [Fact]
    public void AcceptingANode_MakesALabelInItsOwnCase_OnEitherSide()
    {
        var dune = Add("Dune");
        Add("Frank Herbert");

        TypeEach("du");
        Assert.True(_input.Accept(_graph));
        Assert.Equal("Dune ", _input.Text);

        TypeEach(" relates to fr");
        Assert.True(_input.Accept(_graph));
        Assert.Equal("Dune relates to Frank Herbert ", _input.Text);
        Assert.Equal(new InputChip("Dune", false, Homonyms: 1, Visual: new NodeVisual(NodeIcon.Generic)), _input.Chips(_graph, plan: null)[0]);

        Assert.True(_input.Backspace());
        Assert.Equal("Frank Herbert", _input.Tail);
        Assert.Same(dune, _graph.NodesNamed(_input.Chips(_graph, plan: null)[0].Text)[0]);
    }

    [Fact]
    public void WithoutAProposal_AcceptAndDismissDoNothing()
    {
        TypeEach("obj1");

        Assert.False(_input.Accept(_graph));
        Assert.False(_input.Dismiss(_graph));
    }

    [Fact]
    public void Dismiss_HidesTheProposalUntilMoreIsTyped()
    {
        Add("Dune");
        TypeEach("Du");

        Assert.True(_input.Dismiss(_graph));
        Assert.Null(_input.Proposal(_graph));
        _input.Type(_graph, "Dun");
        Assert.Equal("e", _input.Proposal(_graph)?.Rest);
    }

    [Fact]
    public void Unlabel_TurnsAllTheLabelsBackIntoOneText()
    {
        TypeEach("obj1 is a obj2");

        _input.Unlabel();

        Assert.Empty(_input.Chips(_graph, plan: null));
        Assert.Equal("obj1 is a obj2", _input.Tail);
        Assert.Equal(_input.Tail, _input.Text);
    }

    [Fact]
    public void LabelAll_MakesTheSecondNodeALabelToo()
    {
        Add("b");
        TypeEach("a is a  b ");

        _input.LabelAll(_graph.Types);

        Assert.Equal(string.Empty, _input.Tail);
        Assert.Equal(
            [new InputChip("a", false, IsNew: true), new InputChip("is a", true), new InputChip("b", false, Homonyms: 1, Visual: new NodeVisual(NodeIcon.Generic))],
            _input.Chips(_graph, plan: null));
        Assert.Equal(new LinkRequest("a", new LinkVerb(TypeCatalog.IsA, IsReverse: false), "b"), InputParser.Parse(_input.Text));
    }

    [Fact]
    public void LabelAll_ReadsTextTypedWithoutLabels()
    {
        _input.Type(_graph, "Knives contained by Kitchen");

        _input.LabelAll(_graph.Types);

        Assert.Equal(["Knives", "contained by", "Kitchen"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
    }

    [Fact]
    public void LabelAll_MakesASingleNodeALabel_AndLeavesAMissingSecondNode()
    {
        TypeEach("Dune  Messiah");
        _input.LabelAll(_graph.Types);
        Assert.Equal(["Dune Messiah"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));

        _input.Clear();
        _input.Type(_graph, "a is a");
        _input.LabelAll(_graph.Types);
        Assert.Equal(["a", "is a"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.Null(_input.Proposal(_graph));
    }

    [Fact]
    public void LabelAll_LeavesAnEmptyTextEmpty()
    {
        _input.LabelAll(_graph.Types);

        Assert.True(_input.IsEmpty);
    }

    [Fact]
    public void Clear_EmptiesTheLabelsAndTheText()
    {
        TypeEach("obj1 is a obj2");

        _input.Clear();

        Assert.True(_input.IsEmpty);
        Assert.Equal(string.Empty, _input.Text);
        Assert.Null(_input.Proposal(_graph));
    }

    [Fact]
    public void ANodeLabel_PicturesTheHomonymThePlanUses()
    {
        Add("Dune");
        Add("Dune", type: TypeCatalog.Note.Id);
        TypeEach("Dune is a novel");
        var input = new InputPresenter();
        input.Update(_graph, _input.Text);
        input.SwitchSide();
        input.MoveChoice(1);

        var chosen = _input.Chips(_graph, input.Plan)[0];
        var oldest = _input.Chips(_graph, plan: null)[0];

        Assert.Equal(("2", NodeIcon.Note), (chosen.HomonymMark, chosen.Visual!.Icon));
        Assert.Equal(NodeIcon.Generic, oldest.Visual!.Icon);
        Assert.Equal(string.Empty, _input.Chips(_graph, plan: null)[1].HomonymMark);
    }

    // Types the text one character at a time after the current one, as the input box does; true when labels formed.
    private bool TypeEach(string text)
    {
        var formed = false;
        var tail = _input.Tail;
        foreach (var character in text)
        {
            if (_input.Type(_graph, tail += character))
            {
                formed = true;
                tail = _input.Tail;
            }
        }

        return formed;
    }

    private Node Add(string name, string? type = null)
    {
        var node = new Node(Guid.NewGuid(), type ?? TypeCatalog.Generic.Id, name, Start.AddMinutes(_graph.Nodes.Count));
        _graph.Add(node);
        return node;
    }
}
