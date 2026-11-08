namespace Kofrenot.Core.Tests.Input;

public sealed class LinkTypeCopyPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Year = new("year", "Year", FieldKind.Number);
    private static readonly Guid WroteId = Guid.NewGuid();

    private readonly KnowledgeGraph _source = new();
    private readonly KnowledgeGraph _target = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };

    private static LinkTypeDefined Wrote(string name = "wrote", string? reverse = "written by", params FieldDefinition[] fields) =>
        new(Guid.NewGuid(), Start, WroteId.ToString(), name, reverse, fields.Length == 0 ? [Year] : fields, []);

    private int _aliases;

    // Each alias a second after the one before: the oldest of a phrase is the one read.
    private AliasAdded Alias(string type, string phrase, bool isReverse = false) => new(Guid.NewGuid(), Start.AddMinutes(1).AddSeconds(_aliases++), type, isReverse, phrase);

    private IReadOnlyList<LinkTypeCopy> Plan() => new LinkTypeCopyPlanner(_target, _clock).Plan(_source);

    private LinkTypeCopy Copy(params Change[] source)
    {
        foreach (var change in source)
        {
            _source.Apply(change);
        }

        return Assert.Single(Plan());
    }

    private void Apply(LinkTypeCopy copy)
    {
        foreach (var change in copy.Changes)
        {
            _target.Apply(change);
        }
    }

    [Fact]
    public void AVaultWithoutTypesOrAliases_HasNothingToCopy()
    {
        Assert.Empty(Plan());
    }

    [Fact]
    public void ATypeNotHere_IsAdded_WithItsId()
    {
        var copy = Copy(Wrote());

        Assert.Equal((LinkTypeCopyOutcome.Adds, "Adds it", false), (copy.Outcome, copy.Status, copy.IsBuiltIn));
        var change = copy.Definition!;
        Assert.Equal((WroteId.ToString(), "wrote", "written by", Start.AddMinutes(10)), (change.LinkType, change.Name, change.ReverseName, change.At));
        Assert.Equal([Year], change.Fields);
        Assert.Empty(change.Replaces);
        Assert.Equal([change], copy.Changes);
    }

    [Fact]
    public void ATypeHere_DefinedOtherwise_IsUpdated()
    {
        var here = Wrote("authored", "authored by");
        _target.Apply(here);

        var copy = Copy(Wrote());

        Assert.Equal((LinkTypeCopyOutcome.Updates, "Updates it"), (copy.Outcome, copy.Status));
        Assert.Equal([here.Id], copy.Definition!.Replaces);
        Apply(copy);
        Assert.Equal("wrote", _target.LinkTypes.Find(WroteId.ToString())!.Type.Name);
    }

    [Fact]
    public void ATypeHere_WithOtherQualifiers_IsUpdated()
    {
        _target.Apply(Wrote(fields: new FieldDefinition("source", "Source", FieldKind.Text)));

        Assert.Equal(LinkTypeCopyOutcome.Updates, Copy(Wrote()).Outcome);
    }

    [Fact]
    public void ATypeHere_AsItIs_WritesNothing()
    {
        _target.Apply(Wrote());

        var copy = Copy(Wrote());

        Assert.Equal((LinkTypeCopyOutcome.Same, "Already the same here"), (copy.Outcome, copy.Status));
        Assert.Empty(copy.Changes);
    }

    [Fact]
    public void ATypeInConflictHere_IsUpdated_ReplacingBoth()
    {
        var here = Wrote();
        _target.Apply(here);
        _target.Apply(here with { Id = Guid.NewGuid(), Replaces = [here.Id] });
        _target.Apply(here with { Id = Guid.NewGuid(), Name = "authored", Replaces = [here.Id] });

        var copy = Copy(Wrote());

        Assert.Equal(LinkTypeCopyOutcome.Updates, copy.Outcome);
        Assert.Equal(2, copy.Definition!.Replaces.Count);
    }

    [Fact]
    public void ATypeRemovedHere_IsAddedBack()
    {
        var here = Wrote();
        _target.Apply(here);
        _target.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [here.Id]));

        var copy = Copy(Wrote());

        Assert.Equal(LinkTypeCopyOutcome.Adds, copy.Outcome);
        Apply(copy);
        Assert.NotNull(_target.LinkTypes.Find(WroteId.ToString()));
    }

    [Fact]
    public void AnotherTypeReadingItsName_RefusesTheCopy_AndItsAliases()
    {
        _target.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "other", "wrote", null, [], []));

        var copy = Copy(Wrote(), Alias(WroteId.ToString(), "penned"));

        Assert.Equal((LinkTypeCopyOutcome.NameTaken, "“wrote” is a phrase of “wrote” here already: rename one first"), (copy.Outcome, copy.Status));
        Assert.Empty(copy.Changes);
    }

    [Fact]
    public void ABuiltInPhrase_AsItsReverseName_RefusesTheCopy()
    {
        var copy = Copy(Wrote(reverse: "contained by"));

        Assert.Equal((LinkTypeCopyOutcome.NameTaken, "contained by", "contained by"), (copy.Outcome, copy.Taken, copy.TakenBy!.Name));
    }

    [Fact]
    public void AnAliasHere_ReadingItsName_RefusesTheCopy()
    {
        _target.Apply(Alias("relates-to", "wrote"));

        Assert.Equal(LinkTypeCopyOutcome.NameTaken, Copy(Wrote()).Outcome);
    }

    [Fact]
    public void ItsOwnPhrasesHere_DoNotRefuseIt()
    {
        var here = Wrote("authored", "authored by");
        _target.Apply(here);
        _target.Apply(Alias(WroteId.ToString(), "wrote"));

        Assert.Equal(LinkTypeCopyOutcome.Updates, Copy(Wrote()).Outcome);
    }

    [Fact]
    public void ABuiltInTypesQualifiers_AreAdded()
    {
        var copy = Copy(new LinkTypeDefined(Guid.NewGuid(), Start, "contains", "contains", "contained by", [Year], []));

        Assert.Equal((LinkTypeCopyOutcome.Adds, "Adds its qualifiers", true), (copy.Outcome, copy.Status, copy.IsBuiltIn));
        Apply(copy);
        Assert.Equal([Year], _target.LinkTypes.FieldsOf("contains"));
    }

    [Fact]
    public void ABuiltInTypesQualifiersHere_AreUpdated()
    {
        var here = new LinkTypeDefined(Guid.NewGuid(), Start, "contains", "contains", "contained by", [], []);
        _target.Apply(here with { Fields = [new FieldDefinition("source", "Source", FieldKind.Text)] });

        var copy = Copy(here with { Id = Guid.NewGuid(), Fields = [Year] });

        Assert.Equal((LinkTypeCopyOutcome.Updates, "Updates its qualifiers"), (copy.Outcome, copy.Status));
    }

    [Fact]
    public void TheAliasesOfAType_AreAdded_EachWay()
    {
        var copy = Copy(Wrote(), Alias(WroteId.ToString(), "penned"), Alias(WroteId.ToString(), "by", isReverse: true));

        Assert.Equal("Adds it, with 2 aliases", copy.Status);
        Assert.Equal(3, copy.Changes.Count);
        Apply(copy);
        Assert.Equal(
            [("penned", false), ("by", true)],
            _target.Aliases.All.Select(alias => (alias.Phrase, alias.Verb.IsReverse)));
        Assert.All(_target.Aliases.All, alias => Assert.True(alias.IsUsed));
    }

    [Fact]
    public void TheAliasesOfABuiltInType_AreAdded_WithoutADefinition()
    {
        var copy = Copy(Alias("relates-to", "about"));

        Assert.Equal((LinkTypeCopyOutcome.Same, "Adds 1 alias", (LinkTypeDefined?)null), (copy.Outcome, copy.Status, copy.Definition));
        var alias = Assert.Single(copy.Aliases);
        Assert.Equal(("relates-to", "about", Start.AddMinutes(10)), (alias.LinkType, alias.Phrase, alias.At));
    }

    [Fact]
    public void AnAliasHere_OnTheSameWay_IsNotAddedTwice()
    {
        _target.Apply(Alias("relates-to", "ABOUT"));

        var copy = Copy(Alias("relates-to", "about"));

        Assert.Equal("Already the same here", copy.Status);
        Assert.Empty(copy.Changes);
    }

    [Fact]
    public void AnAliasRemovedHere_IsAddedBack()
    {
        var here = Alias("relates-to", "about");
        _target.Apply(here);
        _target.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(2), [here.Id]));

        var copy = Copy(here);

        Apply(copy);
        Assert.Equal("about", Assert.Single(_target.Aliases.All).Phrase);
    }

    [Fact]
    public void AnAliasReadByAnotherTypeHere_IsLeftOut()
    {
        _target.Apply(Alias("contains", "about"));
        _target.Apply(Alias("contains", "has"));

        var copy = Copy(Alias("relates-to", "about"), Alias("relates-to", "has"), Alias("relates-to", "on"));

        Assert.Equal("Adds 1 alias; “about”, “has” left out: another link type here reads them", copy.Status);
        Assert.Equal(["about", "has"], copy.AliasesTaken);
    }

    [Fact]
    public void AnAliasHere_OnTheOtherWay_IsLeftOut()
    {
        _target.Apply(Alias("contains", "in", isReverse: true));

        var copy = Copy(Alias("contains", "in"));

        Assert.Equal("Already the same here; “in” left out: another link type here reads it", copy.Status);
    }

    [Fact]
    public void AnAliasNotUsedThere_IsNotCopied()
    {
        var copy = Copy(Alias("relates-to", "about"), Alias("contains", "about"));

        Assert.Equal("relates-to", copy.Type.Id);
    }

    [Fact]
    public void AnUpdatedType_WithAnAlias_SaysBoth()
    {
        _target.Apply(Wrote("authored", "authored by"));

        Assert.Equal("Updates it, with 1 alias", Copy(Wrote(), Alias(WroteId.ToString(), "penned")).Status);
    }

    [Fact]
    public void LinksHere_KeepReadingTheTypeCopied()
    {
        var (dune, frank) = (new Node(Guid.NewGuid(), "generic", "Dune", Start), new Node(Guid.NewGuid(), "generic", "Frank Herbert", Start));
        _target.Add(dune);
        _target.Add(frank);
        _target.Add(new Link(Guid.NewGuid(), WroteId.ToString(), frank.Id, dune.Id, Start));

        Apply(Copy(Wrote()));

        Assert.Equal("wrote", _target.Types.VerbOf(Assert.Single(_target.Links), frank.Id)!.Name);
    }
}
