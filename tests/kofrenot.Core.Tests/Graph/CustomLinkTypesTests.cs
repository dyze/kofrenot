namespace Kofrenot.Core.Tests.Graph;

public sealed class CustomLinkTypesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _herbert = new(Guid.NewGuid(), "generic", "Frank Herbert", Start);
    private readonly Node _dune = new(Guid.NewGuid(), "generic", "Dune", Start);

    public CustomLinkTypesTests()
    {
        _graph.Add(_herbert);
        _graph.Add(_dune);
    }

    [Fact]
    public void ALinkType_IsAddedAfterTheBuiltInOnes_AndReadByTheInputBox()
    {
        var wrote = Define("wrote", "written by");

        Assert.True(_graph.Apply(wrote));

        var type = _graph.Types.FindLinkType(wrote.LinkType)!;
        Assert.Equal([TypeCatalog.RelatesTo, TypeCatalog.Contains, TypeCatalog.IsA, type], _graph.Types.LinkTypes);
        Assert.Equal(["wrote"], type.Phrases);
        Assert.Equal(["written by"], type.ReversePhrases);
        Assert.Equal(new LinkRequest("Dune", type.Reverse!, "Frank Herbert"), InputParser.Parse("Dune written by Frank Herbert", _graph.Types));
        var custom = Assert.Single(_graph.LinkTypes.All);
        Assert.Equal((type, false, (LinkType?)null), (custom.Type, custom.IsConflict, custom.TakenBy));
        Assert.Equal([wrote.Id], custom.Definitions);
        Assert.Same(custom, _graph.LinkTypes.Find(wrote.LinkType));
    }

    [Fact]
    public void ALinkTypeAppliedTwice_ChangesNothing()
    {
        var knows = Define("knows");
        _graph.Apply(knows);

        Assert.False(_graph.Apply(knows));
        Assert.Single(_graph.LinkTypes.All);
    }

    [Fact]
    public void ARename_IsShown_AndTheOldNameNoLongerRead()
    {
        var wrote = Define("wrote", "written by");
        var link = Link(wrote.LinkType, _herbert, _dune);
        _graph.Apply(wrote);
        _graph.Add(link);

        _graph.Apply(Rename(wrote, "authored", "authored by", [wrote.Id]));

        Assert.Equal("authored by", _graph.Types.LinkName(link, _dune.Id));
        Assert.IsType<NodeRequest>(InputParser.Parse("Herbert wrote Dune", _graph.Types));
        Assert.Equal(2, _graph.LinkTypes.All.Single().Definitions.Count);
    }

    [Fact]
    public void RenamesMadeApart_AreAConflict_TheLatestShown()
    {
        var wrote = Define("wrote", "written by");
        _graph.Apply(wrote);
        var first = Rename(wrote, "authored", "authored by", [wrote.Id]);
        var second = Rename(wrote, "penned", "penned by", [wrote.Id], minutes: 2);
        _graph.Apply(second);
        _graph.Apply(first);

        var custom = _graph.LinkTypes.All.Single();

        Assert.True(custom.IsConflict);
        Assert.Equal([first.Id, second.Id], custom.Names.Select(name => name.Id));
        Assert.Equal("penned", custom.Type.Name);
    }

    [Fact]
    public void APhraseAddedTwice_IsReadAsTheOlderType()
    {
        var older = Define("knows");
        var newer = Define("knows", minutes: 1);
        _graph.Apply(newer);
        _graph.Apply(older);

        var (first, second) = (_graph.LinkTypes.All[0], _graph.LinkTypes.All[1]);

        Assert.Equal((older.LinkType, (LinkType?)null), (first.Type.Id, first.TakenBy));
        Assert.Equal(first.Type, second.TakenBy);
        Assert.Equal(older.LinkType, Assert.IsType<LinkRequest>(InputParser.Parse("Ann knows Bob", _graph.Types)).Verb.Type.Id);
    }

    [Fact]
    public void APhraseOfABuiltInType_IsTakenByIt()
    {
        _graph.Apply(Define("contains", "part of"));

        Assert.Equal(TypeCatalog.Contains, _graph.LinkTypes.All.Single().TakenBy);
    }

    [Fact]
    public void ANameOfABuiltInType_IsIgnored()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "contains", "holds", null, []));

        Assert.Empty(_graph.LinkTypes.All);
        Assert.Same(TypeCatalog.BuiltIn, _graph.LinkTypes.Catalog);
    }

    [Fact]
    public void ARemovedType_IsShownByItsId_AndItsAliasesNoLongerUsed()
    {
        var wrote = Define("wrote", "written by");
        var link = Link(wrote.LinkType, _herbert, _dune);
        var alias = new AliasAdded(Guid.NewGuid(), Start, wrote.LinkType, IsReverse: false, "penned");
        _graph.Apply(wrote);
        _graph.Apply(alias);
        _graph.Add(link);
        Assert.Equal("penned", _graph.Aliases.All.Single().Phrase);

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [wrote.Id]));

        Assert.Empty(_graph.LinkTypes.All);
        Assert.Null(_graph.Types.FindLinkType(wrote.LinkType));
        Assert.Equal(wrote.LinkType, _graph.Types.LinkName(link, _herbert.Id));
        Assert.Empty(_graph.Aliases.All);
    }

    [Fact]
    public void LinksOfASymmetricType_CountOnce_EvenBeforeTheirTypeArrives()
    {
        var knows = Define("knows");
        _graph.Add(Link(knows.LinkType, _herbert, _dune));
        _graph.Add(Link(knows.LinkType, _dune, _herbert, minutes: 1));
        Assert.Equal(2, _graph.LinksOf(_dune.Id).Count);

        _graph.Apply(knows);

        Assert.Single(_graph.LinksOf(_dune.Id));
        Assert.NotNull(_graph.FindLink(knows.LinkType, _dune.Id, _herbert.Id));

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(2), [knows.Id]));

        Assert.Equal(2, _graph.LinksOf(_dune.Id).Count);
    }

    [Fact]
    public void ARemovedLink_StaysHidden_WhenItsTypeArrives()
    {
        var knows = Define("knows");
        var link = Link(knows.LinkType, _herbert, _dune);
        _graph.Add(link);
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start, [link.Id]));

        _graph.Apply(knows);

        Assert.Empty(_graph.LinksOf(_dune.Id));
    }

    [Fact]
    public void ARemovalOfOtherChanges_KeepsTheTypes()
    {
        var knows = Define("knows");
        _graph.Apply(knows);

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start, [_dune.Id]));

        Assert.Single(_graph.LinkTypes.All);
    }

    [Fact]
    public void AnAlias_OfAUserType_IsRead()
    {
        var wrote = Define("wrote", "written by");
        _graph.Apply(wrote);

        _graph.Apply(new AliasAdded(Guid.NewGuid(), Start, wrote.LinkType, IsReverse: true, "by"));

        var type = _graph.Types.FindLinkType(wrote.LinkType)!;
        Assert.Equal(["written by", "by"], type.ReversePhrases);
        Assert.Contains(_graph.Aliases.ByVerb(), verb => verb.Verb == type.Reverse && verb.Aliases.Single().Phrase == "by");
    }

    private static LinkTypeDefined Define(string name, string? reverse = null, int minutes = 0)
    {
        var id = Guid.NewGuid();
        return new LinkTypeDefined(id, Start.AddMinutes(minutes), id.ToString(), name, reverse, []);
    }

    private static LinkTypeDefined Rename(LinkTypeDefined type, string name, string? reverse, IReadOnlyList<Guid> replaces, int minutes = 1) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), type.LinkType, name, reverse, replaces);

    private static Link Link(string type, Node from, Node to, int minutes = 0) =>
        new(Guid.NewGuid(), type, from.Id, to.Id, Start.AddMinutes(minutes));
}
