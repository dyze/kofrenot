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
    public void Next_GoesThroughTheTabs_BothWays_WrappingAround()
    {
        var (memos, files) = (_tree.Add("Memos", TypeCatalog.View.Id), _tree.Add("Files", TypeCatalog.View.Id));
        _open.Open(_vault, memos.Id);
        _open.Open(_vault, files.Id);

        Assert.Equal(new Tab(memos), _tabs.Next(_tree.Graph, _vault, current: null, step: 1));
        Assert.Equal(new Tab(files), _tabs.Next(_tree.Graph, _vault, memos.Id, step: 1));
        Assert.Equal(Tab.Search, _tabs.Next(_tree.Graph, _vault, files.Id, step: 1));
        Assert.Equal(new Tab(files), _tabs.Next(_tree.Graph, _vault, current: null, step: -1));
        Assert.Equal(Tab.Search, _tabs.Next(_tree.Graph, _vault, memos.Id, step: -1));
    }

    [Fact]
    public void Next_FromATabGone_StartsFromTheSearch()
    {
        var memos = _tree.Add("Memos", TypeCatalog.View.Id);
        _open.Open(_vault, memos.Id);

        Assert.Equal(new Tab(memos), _tabs.Next(_tree.Graph, _vault, Guid.NewGuid(), step: 1));
    }

    [Fact]
    public void Next_WithTheSearchAlone_StaysOnIt() =>
        Assert.Equal(Tab.Search, _tabs.Next(_tree.Graph, _vault, current: null, step: -1));

    public void Dispose() => _temp.Dispose();
}
