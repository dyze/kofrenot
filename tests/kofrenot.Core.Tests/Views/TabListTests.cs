namespace Kofrenot.Core.Tests.Views;

public sealed class TabListTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly TreeGraph _tree = new();
    private readonly Guid _vault = Guid.NewGuid();
    private readonly ViewTabs _open;
    private readonly TabList _tabs;

    public TabListTests()
    {
        _open = new ViewTabs(Path.Combine(_temp.Sub("data"), "tabs.json"));
        _tabs = new TabList(_open);
    }

    [Fact]
    public void TheSearch_IsTheFirstTab_ThenTheViewsOpen_KnownAsViews()
    {
        var (memos, files, dune) = (_tree.Add("Memos", TypeCatalog.View.Id), _tree.Add("Files", TypeCatalog.View.Id), _tree.Add("Dune"));
        _open.Open(_vault, memos.Id);
        _open.Open(_vault, Guid.NewGuid());
        _open.Open(_vault, dune.Id);
        _open.Open(_vault, files.Id);

        var tabs = _tabs.Of(_tree.Graph, _vault);

        Assert.Equal([Tab.Search, new Tab(memos), new Tab(files)], tabs);
        Assert.Equal(("Search", true), (tabs[0].Name, tabs[0].IsSearch));
        Assert.Equal(("Memos", false), (tabs[1].Name, tabs[1].IsSearch));
    }

    [Fact]
    public void TheViewsNotOpen_AreOfferedByName()
    {
        var (memos, files, archive) = (_tree.Add("Memos", TypeCatalog.View.Id), _tree.Add("Files", TypeCatalog.View.Id), _tree.Add("archive", TypeCatalog.View.Id));
        _tree.Add("Dune");
        _open.Open(_vault, memos.Id);

        Assert.Equal([archive, files], _tabs.Closed(_tree.Graph, _vault));
    }

    [Fact]
    public void AView_IsDefinedByItsLatestVersion()
    {
        var files = _tree.Add("Files", TypeCatalog.View.Id);
        Assert.Null(TabList.DefinitionOf(_tree.Graph, files));

        var first = new ViewEdited(Guid.NewGuid(), TreeGraph.Start, files.Id, new FlatDefinition(TypeCatalog.Note.Id), []);
        var conflict = new ViewEdited(Guid.NewGuid(), TreeGraph.Start.AddMinutes(1), files.Id, new FlatDefinition(TypeCatalog.Dataset.Id), []);
        _tree.Apply([conflict, first]);

        Assert.Equal(conflict.Definition, TabList.DefinitionOf(_tree.Graph, files));
    }

    public void Dispose() => _temp.Dispose();
}
