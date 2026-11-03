using Kofrenot.Core.Tests.Views;

namespace Kofrenot.Core.Tests.Browse;

public sealed class ImportDestinationsTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly Guid _vault = Guid.NewGuid();
    private readonly TreeGraph _tree = new();

    private string FilePath => Path.Combine(_temp.Sub("data"), "import-destinations.json");

    [Fact]
    public void NoFile_MeansNone() =>
        Assert.Null(Destinations().Of(_vault));

    [Fact]
    public void TheLastDestinationSet_IsKept_InTheFile_PerVault()
    {
        var (first, second) = (new ImportDestination(Guid.NewGuid(), Guid.NewGuid()), new ImportDestination(Guid.NewGuid(), Guid.NewGuid()));
        var destinations = Destinations();

        destinations.Set(_vault, first);
        destinations.Set(_vault, second);

        Assert.Equal(second, destinations.Of(_vault));
        Assert.Equal(second, Destinations().Of(_vault));
        Assert.Null(destinations.Of(Guid.NewGuid()));
    }

    [Fact]
    public void None_ForgetsTheDestination()
    {
        var destinations = Destinations();
        destinations.Set(_vault, new ImportDestination(Guid.NewGuid(), Guid.NewGuid()));

        destinations.Set(_vault, null);
        destinations.Set(_vault, null);

        Assert.Null(Destinations().Of(_vault));
    }

    [Fact]
    public void Resolve_GivesTheNodeUnderTheViewsLinkType()
    {
        var (root, invoices) = (_tree.Add("Projects"), _tree.Add("Invoices"));
        var view = View("Work", new TreeDefinition(root.Id, TypeCatalog.IsA.Id));

        var resolved = ImportDestinations.Resolve(_tree.Graph, new ImportDestination(view.Id, invoices.Id));

        Assert.Equal(new ParentLink(invoices, TypeCatalog.IsA.Id), resolved?.Under);
        Assert.Equal("Work › Invoices", resolved?.Label);
    }

    [Fact]
    public void Resolve_IsNull_WithoutADestination_ItsNodeOrView_OrForAList()
    {
        var root = _tree.Add("Projects");
        var tree = View("Work", new TreeDefinition(root.Id, TypeCatalog.Contains.Id));
        var list = View("Notes", new FlatDefinition(TypeCatalog.Note.Id));

        Assert.Null(ImportDestinations.Resolve(_tree.Graph, null));
        Assert.Null(ImportDestinations.Resolve(_tree.Graph, new ImportDestination(tree.Id, Guid.NewGuid())));
        Assert.Null(ImportDestinations.Resolve(_tree.Graph, new ImportDestination(Guid.NewGuid(), root.Id)));
        Assert.Null(ImportDestinations.Resolve(_tree.Graph, new ImportDestination(list.Id, root.Id)));
    }

    [Fact]
    public void TreeViews_AreThoseWithTheirRoot_ByName()
    {
        var root = _tree.Add("Projects");
        var work = View("work", new TreeDefinition(root.Id, TypeCatalog.Contains.Id));
        var archive = View("Archive", new TreeDefinition(root.Id, TypeCatalog.Contains.Id));
        View("Gone", new TreeDefinition(Guid.NewGuid(), TypeCatalog.Contains.Id));
        View("Notes", new FlatDefinition(TypeCatalog.Note.Id));
        _tree.Add("No definition", TypeCatalog.View.Id);

        Assert.Equal([archive, work], ImportDestinations.TreeViews(_tree.Graph));
    }

    public void Dispose() => _temp.Dispose();

    private ImportDestinations Destinations() => new(FilePath);

    private Node View(string name, ViewDefinition definition)
    {
        var view = _tree.Add(name, TypeCatalog.View.Id);
        _tree.Graph.Apply(new ViewEdited(Guid.NewGuid(), TreeGraph.Start, view.Id, definition, []));
        return view;
    }
}
