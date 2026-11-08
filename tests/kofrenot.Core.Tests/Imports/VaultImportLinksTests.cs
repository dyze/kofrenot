namespace Kofrenot.Core.Tests.Imports;

/// <summary>The links of nodes imported from another vault: added between nodes here, with their qualifiers and types, or left out.</summary>
public sealed class VaultImportLinksTests
{
    private static readonly FieldDefinition Role = new("role", "Role", FieldKind.Text);

    private readonly TwoVaults _vaults = new();

    private KnowledgeGraph Source => _vaults.Source;

    private KnowledgeGraph Target => _vaults.Target;

    [Fact]
    public void ALinkBetweenNodesImported_OrToTheSameNodeHere_IsAdded_OthersLeftOut()
    {
        var (books, dune, herbert, lost) = (TwoVaults.Add(Source, "Books"), TwoVaults.Add(Source, "Dune"), TwoVaults.Add(Source, "Herbert"), TwoVaults.Add(Source, "Lost"));
        TwoVaults.Add(Target, "Herbert", id: herbert.Id);
        TwoVaults.Link(Source, books, dune);
        TwoVaults.Link(Source, dune, herbert, TypeCatalog.RelatesTo.Id);
        TwoVaults.Link(Source, books, lost);

        var plan = _vaults.Plan([books, dune]);
        _vaults.Write(plan);

        Assert.Equal(["contains: + Dune"], plan.Items[0].Details);
        Assert.Equal(["contained by: + Books", "relates to: + Herbert"], plan.Items[1].Details);
        Assert.Equal(["1 link left out: its other node is not here"], plan.Summary);
        Assert.NotNull(Target.FindLink(TypeCatalog.Contains.Id, books.Id, dune.Id));
        Assert.NotNull(Target.FindLink(TypeCatalog.RelatesTo.Id, dune.Id, herbert.Id));
        Assert.Equal(2, Target.Links.Count);
    }

    [Fact]
    public void ALinkHereAlready_IsNotAddedTwice_ANewOne_UpdatesAnUnchangedNode()
    {
        var (books, dune, herbert) = (TwoVaults.Add(Source, "Books"), TwoVaults.Add(Source, "Dune"), TwoVaults.Add(Source, "Herbert"));
        TwoVaults.Link(Source, books, dune);
        _vaults.Write(_vaults.Plan([books, dune, herbert]));
        Assert.Equal(3, _vaults.Plan([books, dune, herbert]).Of(RecordOutcome.Unchanged).Count);

        TwoVaults.Link(Source, herbert, dune, TypeCatalog.RelatesTo.Id);
        var plan = _vaults.Plan([herbert]);
        _vaults.Write(plan);

        Assert.Equal(["relates to: + Dune"], Assert.Single(plan.Of(RecordOutcome.Updated)).Details);
        Assert.Equal(2, Target.Links.Count);
    }

    [Fact]
    public void ALinkToANodeSkipped_IsLeftOut_AndToANodeAddedAsNew_FollowsIt()
    {
        var (books, dune) = (TwoVaults.Add(Source, "Books"), TwoVaults.Add(Source, "Dune"));
        TwoVaults.Link(Source, books, dune);
        TwoVaults.Set(Source, dune, TwoVaults.Published, "1965", owner: FieldSet.Own);
        var here = TwoVaults.Add(Target, "Dune");
        var plan = _vaults.Plan([books, dune]);
        Assert.Equal(RecordOutcome.ToCheck, plan.Items[1].Outcome);

        var skipped = plan.WriteFor(item => item.Source == dune ? RecordChoice.Skip : RecordChoice.Import);
        var asNew = plan.WriteFor(item => item.Source == dune ? RecordChoice.AddAsNew : RecordChoice.Import);
        var updated = plan.WriteFor(_ => RecordChoice.Import);

        Assert.Empty(skipped.Changes.OfType<LinkAdded>());
        Assert.Equal(dune.Id, asNew.Changes.OfType<LinkAdded>().Single().Link.To);
        Assert.Equal(here.Id, updated.Changes.OfType<LinkAdded>().Single().Link.To);
    }

    [Fact]
    public void AUserLinkType_IsCopied_WithTheLink_AndItsQualifiers()
    {
        Source.Apply(new LinkTypeDefined(Guid.NewGuid(), TwoVaults.Start, "wrote", "wrote", "written by", [Role], []));
        var (herbert, dune) = (TwoVaults.Add(Source, "Herbert"), TwoVaults.Add(Source, "Dune"));
        var link = TwoVaults.Link(Source, herbert, dune, "wrote");
        TwoVaults.Set(Source, link, Role, "author", owner: "wrote");

        var plan = _vaults.Plan([herbert, dune]);
        _vaults.Write(plan);

        Assert.Equal(["“wrote”"], plan.LinkTypesAdded.Select(name => $"“{name}”"));
        Assert.Equal(["Also adds link type “wrote”"], plan.Summary);
        Assert.Equal(["wrote: + Dune"], plan.Items[0].Details);
        var added = Target.FindLink("wrote", herbert.Id, dune.Id)!;
        Assert.Equal("author", Assert.Single(Target.QualifiersOf(added)).Shown.Value);
        Assert.Equal("written by", Target.Types.LinkName(added, dune.Id));
    }

    [Fact]
    public void ALinkTypeWhoseNameIsTaken_LeavesItsLinksOut_AndSaysWhy()
    {
        Source.Apply(new LinkTypeDefined(Guid.NewGuid(), TwoVaults.Start, "wrote", "wrote", null, [], []));
        Target.Apply(new LinkTypeDefined(Guid.NewGuid(), TwoVaults.Start, "authored", "wrote", null, [], []));
        var (herbert, dune, lem, solaris) = (TwoVaults.Add(Source, "Herbert"), TwoVaults.Add(Source, "Dune"), TwoVaults.Add(Source, "Lem"), TwoVaults.Add(Source, "Solaris"));
        TwoVaults.Link(Source, herbert, dune, "wrote");
        TwoVaults.Link(Source, lem, solaris, "wrote");

        var plan = _vaults.Plan([herbert, dune, lem, solaris]);

        Assert.Equal(2, plan.LinksLeftOut);
        Assert.Equal("2 links left out: their other node is not here", plan.Summary[0]);
        Assert.Single(plan.Problems);
        Assert.StartsWith("Links “wrote” left out:", plan.Problems[0], StringComparison.Ordinal);
        Assert.Empty(plan.WriteFor(_ => RecordChoice.Import).Changes.OfType<LinkAdded>());
    }

    [Fact]
    public void ALinkOfATypeUnknownThere_IsLeftOut()
    {
        var (herbert, dune) = (TwoVaults.Add(Source, "Herbert"), TwoVaults.Add(Source, "Dune"));
        TwoVaults.Link(Source, herbert, dune, "gone");

        Assert.Equal(1, _vaults.Plan([herbert, dune]).LinksLeftOut);
    }

    [Fact]
    public void AQualifierImage_NotThere_IsLeftOut_AndTold()
    {
        var (ann, bob, shown, here) = LinkWithImages();

        var plan = _vaults.Plan([ann, bob]);
        var write = _vaults.Write(plan);

        Assert.Equal(["Ann · Photo: its image is not on this computer"], plan.Problems);
        var added = Target.FindLink("met", ann.Id, bob.Id)!;
        Assert.Equal(["photo2", "role"], Target.QualifiersOf(added).Select(value => value.Field).Order());
        Assert.Equal(here.Id, Assert.Single(write.Contents).From.Id);
        Assert.DoesNotContain(Target.QualifiersOf(added), value => value.Shown.Id == shown.Id);
    }

    [Fact]
    public void AQualifierImage_OfAServerVault_IsDownloadedOnImport()
    {
        _vaults.CanDownload = true;
        var (ann, bob, _, _) = LinkWithImages();

        var plan = _vaults.Plan([ann, bob]);

        Assert.Empty(plan.Problems);
        Assert.Equal(2, plan.WriteFor(_ => RecordChoice.Import).Contents.Count);
    }

    // A link with a role and two images, the first not there.
    private (Node Ann, Node Bob, FieldSet Role, FieldSet Here) LinkWithImages()
    {
        var photo = new FieldDefinition("photo", "Photo", FieldKind.Image);
        Source.Apply(new LinkTypeDefined(Guid.NewGuid(), TwoVaults.Start, "met", "met", null, [photo, Role], []));
        var (ann, bob) = (TwoVaults.Add(Source, "Ann"), TwoVaults.Add(Source, "Bob"));
        var link = TwoVaults.Link(Source, ann, bob, "met");
        TwoVaults.Set(Source, link, photo, null, new DatasetFile("met.png", 10, "aaa"), owner: "met");
        var role = TwoVaults.Set(Source, link, Role, "friend", owner: "met");
        var here = TwoVaults.Set(Source, link, photo with { Id = "photo2", Name = "Photo 2" }, null, new DatasetFile("again.png", 10, "bbb"), owner: "met");
        _vaults.Files.Add(here.Id);
        return (ann, bob, role, here);
    }
}
