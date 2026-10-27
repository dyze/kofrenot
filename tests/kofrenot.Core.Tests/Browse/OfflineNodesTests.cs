namespace Kofrenot.Core.Tests.Browse;

public sealed class OfflineNodesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => Path.Combine(_temp.Sub("data"), "offline.json");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void NoFile_MeansNone() =>
        Assert.Empty(Store().Of(_vault));

    [Fact]
    public void ANodeKept_IsListedOnce_PerVault_AndKeptInTheFile()
    {
        var (dune, herbert) = (Guid.NewGuid(), Guid.NewGuid());
        var store = Store();

        store.Set(_vault, dune, kept: true);
        store.Set(_vault, herbert, kept: true);
        store.Set(_vault, dune, kept: true);

        Assert.Equal([herbert, dune], store.Of(_vault));
        Assert.True(Store().Has(_vault, dune));
        Assert.Empty(store.Of(Guid.NewGuid()));
    }

    [Fact]
    public void ANodeNoLongerKept_LeavesTheList()
    {
        var node = Guid.NewGuid();
        var store = Store();
        store.Set(_vault, node, kept: true);

        store.Set(_vault, node, kept: false);

        Assert.False(Store().Has(_vault, node));
    }

    private OfflineNodes Store() => new(FilePath);
}
