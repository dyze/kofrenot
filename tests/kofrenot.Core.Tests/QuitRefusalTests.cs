namespace Kofrenot.Core.Tests;

public sealed class QuitRefusalTests
{
    [Fact]
    public void NothingUnderWay_MayQuit() =>
        Assert.Null(QuitRefusal.Reason(unsavedNote: null, importsRunning: 0));

    [Fact]
    public void AnUnsavedNote_IsNamed()
    {
        var reason = QuitRefusal.Reason("Dune", importsRunning: 0);

        Assert.Equal("kofrenot cannot quit now.\n\nThe note \"Dune\" is not saved: save it (Ctrl+S).\n\nThen quit again.", reason);
    }

    [Fact]
    public void OneImport_IsAFile()
    {
        var reason = QuitRefusal.Reason(unsavedNote: null, importsRunning: 1);

        Assert.Equal("kofrenot cannot quit now.\n\nA file is being imported: wait until it ends.\n\nThen quit again.", reason);
    }

    [Fact]
    public void SeveralImports_AreCounted_AfterTheNote()
    {
        var reason = QuitRefusal.Reason("Dune", importsRunning: 2);

        Assert.Equal(
            "kofrenot cannot quit now.\n\nThe note \"Dune\" is not saved: save it (Ctrl+S).\n2 files are being imported: wait until they end.\n\nThen quit again.",
            reason);
    }
}
