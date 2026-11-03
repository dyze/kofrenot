namespace Kofrenot.Core.Tests.Views;

public sealed class UnlinkedNodesTests
{
    private readonly TreeGraph _tree = new();

    [Fact]
    public void Find_GivesTheNodesNoDirectedLinkLeadsTo_NewestFirst()
    {
        var older = Add("Scan", minutes: 1);
        var newer = Add("Invoice", minutes: 5);
        var child = Add("Child");
        _tree.Contains(older, child);

        var list = new UnlinkedNodes(_tree.Graph).Find("");

        Assert.Equal([newer, older], list.Shown);
        Assert.Equal(2, list.Count);
        Assert.False(list.IsCut);
    }

    [Fact]
    public void ASymmetricLink_MakesNoParent()
    {
        var friend = Add("Friend");
        var other = Add("Other", minutes: 1);
        _tree.Link(TypeCatalog.RelatesTo.Id, other, friend);

        Assert.Equal([other, friend], new UnlinkedNodes(_tree.Graph).Find("").Shown);
    }

    [Fact]
    public void ViewsAndTheRootsOfTreeViews_AreLeftOut()
    {
        var root = Add("Projects");
        var listed = Add("Loose");
        var view = Add("Projects view", type: TypeCatalog.View.Id);
        _tree.Graph.Apply(new ViewEdited(Guid.NewGuid(), TreeGraph.Start, view.Id, new TreeDefinition(root.Id, TypeCatalog.Contains.Id), []));
        var list = Add("Notes view", type: TypeCatalog.View.Id);
        _tree.Graph.Apply(new ViewEdited(Guid.NewGuid(), TreeGraph.Start, list.Id, new FlatDefinition(TypeCatalog.Note.Id), []));

        Assert.Equal([listed], new UnlinkedNodes(_tree.Graph).Find("").Shown);
    }

    [Fact]
    public void Find_KeepsTheMatchingNames()
    {
        var invoice = Add("Invoice March");
        Add("Scan");

        Assert.Equal([invoice], new UnlinkedNodes(_tree.Graph).Find("mar").Shown);
    }

    [Fact]
    public void AtMostMaxShown_AreShown_AllCounted()
    {
        for (var index = 0; index < UnlinkedNodes.MaxShown + 5; index++)
        {
            Add($"File {index}", minutes: index);
        }

        var list = new UnlinkedNodes(_tree.Graph).Find(" ");

        Assert.Equal(UnlinkedNodes.MaxShown, list.Shown.Count);
        Assert.Equal(UnlinkedNodes.MaxShown + 5, list.Count);
        Assert.True(list.IsCut);
        Assert.Equal($"File {UnlinkedNodes.MaxShown + 4}", list.Shown[0].Name);
    }

    [Fact]
    public void PutUnder_LinksEachNodeUnderTheTarget_ByItsLinkType()
    {
        var target = Add("Invoices");
        var (scan, photo) = (Add("Scan"), Add("Photo"));

        var changes = new UnlinkedNodes(_tree.Graph).PutUnder([scan, photo], new ParentLink(target, TypeCatalog.IsA.Id));

        Assert.Equal(
            [(TypeCatalog.IsA.Id, target.Id, scan.Id), (TypeCatalog.IsA.Id, target.Id, photo.Id)],
            changes.Cast<LinkAdded>().Select(added => (added.Link.Type, added.Link.From, added.Link.To)));
    }

    [Fact]
    public void PutUnder_LeavesOutTheTarget_ANodeUnderItAlready_AndANodeAboveIt()
    {
        var target = Add("Invoices");
        var already = Add("Already");
        _tree.Contains(target, already);
        var above = Add("Above");
        _tree.Contains(above, target);

        Assert.Empty(new UnlinkedNodes(_tree.Graph).PutUnder([target, already, above], new ParentLink(target, TypeCatalog.Contains.Id)));
    }

    private Node Add(string name, int minutes = 0, string? type = null)
    {
        var node = new Node(Guid.NewGuid(), type ?? TypeCatalog.Generic.Id, name, TreeGraph.Start.AddMinutes(minutes));
        _tree.Graph.Apply(new NodeAdded(Guid.NewGuid(), node.Created, node));
        return node;
    }
}
