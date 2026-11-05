namespace Kofrenot.Core.Tests.Input;

/// <summary>Qualifiers as labels in the input box: <c>[Dune] [written by] [Frank Herbert], [Date] 1965</c>.</summary>
public sealed class QualifierChipsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ChipInput _input = new();

    public QualifierChipsTests() =>
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by",
            [new FieldDefinition("date", "Date", FieldKind.Date), new FieldDefinition("source", "Source", FieldKind.Url)], []));

    [Fact]
    public void ACommaAndAQualifiersName_WithASpace_MakeTheSecondNodeAndTheNameLabels()
    {
        Assert.True(TypeEach("Dune written by Frank Herbert, Date "));

        Assert.Equal(["Dune", "written by", "Frank Herbert", "Date"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.True(_input.Chips(_graph, plan: null)[^1].IsQualifier);
        Assert.Equal(string.Empty, _input.Tail);

        TypeEach("1965, Source isfdb.org");

        Assert.Equal(["Dune", "written by", "Frank Herbert", "Date 1965", "Source"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.Equal("isfdb.org", _input.Tail);
        Assert.Equal("Dune written by Frank Herbert, Date 1965, Source isfdb.org", _input.Text);
    }

    [Fact]
    public void ACommaNotFollowedByAQualifier_StaysText()
    {
        TypeEach("Dune written by Herbert, Frank ");

        Assert.Equal(["Dune", "written by"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.Equal("Herbert, Frank ", _input.Tail);
    }

    [Fact]
    public void AQualifiersName_IsProposed_AndTabMakesItALabel()
    {
        TypeEach("Dune written by Frank Herbert, so");

        var proposal = _input.Proposal(_graph)!;
        Assert.Equal(("urce", "Source", true), (proposal.Rest, proposal.Label, proposal.IsQualifier));
        Assert.True(_input.Accept(_graph));
        Assert.Equal(["Dune", "written by", "Frank Herbert", "Source"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));

        TypeEach("isfdb.org, da");
        Assert.True(_input.Accept(_graph));
        Assert.Equal(["Dune", "written by", "Frank Herbert", "Source isfdb.org", "Date"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
    }

    [Fact]
    public void ANodeProposal_GivesWayAfterAComma()
    {
        _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Frank Herbert", Start));
        TypeEach("Dune written by Fr");
        Assert.True(_input.Accept(_graph));

        TypeEach(", d");
        Assert.Equal("Date", _input.Proposal(_graph)!.Label);
        TypeEach("x");
        Assert.Null(_input.Proposal(_graph));
    }

    [Fact]
    public void Backspace_TurnsTheLastQualifierBackIntoText()
    {
        TypeEach("Dune written by Frank Herbert, Date 1965, Source ");

        Assert.True(_input.Backspace());
        Assert.Equal(", Source", _input.Tail);
        Assert.Equal("Dune written by Frank Herbert, Date 1965 , Source", _input.Text);
        _input.Type(_graph, string.Empty);
        Assert.True(_input.Backspace());
        Assert.Equal(", Date 1965", _input.Tail);
        Assert.Equal(["Dune", "written by", "Frank Herbert"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
    }

    [Fact]
    public void Enter_LabelsTheQualifiersToo()
    {
        TypeEach("Dune written by Frank Herbert, Date 1965, Source isfdb.org");
        _input.Unlabel();

        _input.LabelAll(_graph);

        Assert.Equal(["Dune", "written by", "Frank Herbert", "Date 1965", "Source isfdb.org"], _input.Chips(_graph, plan: null).Select(chip => chip.Text));
        Assert.Equal("Dune written by Frank Herbert, Date 1965, Source isfdb.org ", _input.Text);
        _input.Clear();
        Assert.True(_input.IsEmpty);
        Assert.Empty(_input.Chips(_graph, plan: null));
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
}
