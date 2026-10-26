namespace Kofrenot.Core.Tests.Session;

/// <summary>A pass phrase checked before the vault opens (kofrenot for Android): the opening then does not ask it.</summary>
public sealed class UnlockAheadTests : IDisposable
{
    private readonly FakeKofre _kofre = new();
    private readonly Device _device;

    public UnlockAheadTests() => _device = new Device(_kofre);

    private VaultSession Session => _device.Session;

    private ScriptedQuestions Ask => _device.Ask;

    public void Dispose() => _device.Dispose();

    [Fact]
    public async Task TheRightPassPhrase_OpensTheVault_WithoutAsking()
    {
        var location = ClosedFolderVault();

        Assert.Null(await Session.UnlockAheadAsync(location, ScriptedQuestions.FirstPassPhrase, remember: false));
        Session.Open(Ask);

        Assert.NotNull(Session.Current);
        Assert.DoesNotContain(nameof(ISessionQuestions.AskPassPhrase), Ask.Asked);
    }

    [Fact]
    public async Task AWrongPassPhrase_IsRefused_AndTheOpeningAsks()
    {
        var location = ClosedFolderVault();

        Assert.Equal(VaultSession.WrongPassPhrase, await Session.UnlockAheadAsync(location, "wrong", remember: false));
        Ask.Cancels = true;
        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Contains(nameof(ISessionQuestions.AskPassPhrase), Ask.Asked);
    }

    [Fact]
    public async Task WhatItUnlocks_IsUsedOnce()
    {
        var location = ClosedFolderVault();
        await Session.UnlockAheadAsync(location, ScriptedQuestions.FirstPassPhrase, remember: false);
        Session.Open(Ask);

        Session.Close();
        Ask.Cancels = true;
        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Contains(nameof(ISessionQuestions.AskPassPhrase), Ask.Asked);
    }

    [Fact]
    public async Task Remembered_TheVaultOpensAgain_WithoutAsking()
    {
        var location = ClosedFolderVault();
        await Session.UnlockAheadAsync(location, ScriptedQuestions.FirstPassPhrase, remember: true);
        Session.Open(Ask);

        Session.Close();
        Ask.Cancels = true;
        Session.Open(Ask);

        Assert.NotNull(Session.Current);
        Assert.DoesNotContain(nameof(ISessionQuestions.AskPassPhrase), Ask.Asked);
    }

    [Fact]
    public async Task AnotherVaultOpening_DropsIt()
    {
        var first = ClosedFolderVault("first");
        var second = ClosedFolderVault("second");
        await Session.UnlockAheadAsync(first, ScriptedQuestions.FirstPassPhrase, remember: false);

        Ask.Cancels = true;
        Session.SwitchTo(Ask, second);
        Session.SwitchTo(Ask, first);

        Assert.Null(Session.Current);
        Assert.Equal(2, Ask.Asked.Count(asked => asked == nameof(ISessionQuestions.AskPassPhrase)));
    }

    [Fact]
    public async Task AFolderWithoutAVault_IsLeftToItsOpening()
    {
        Assert.Null(await Session.UnlockAheadAsync(new VaultLocation(_device.Folder("nothing")), ScriptedQuestions.FirstPassPhrase, remember: false));
    }

    // A folder vault made with the first pass phrase, selected and closed.
    private VaultLocation ClosedFolderVault(string name = "vault")
    {
        var location = new VaultLocation(_device.CreateFolderVault(name));
        Session.Close();
        Ask.Asked.Clear();
        return location;
    }
}
