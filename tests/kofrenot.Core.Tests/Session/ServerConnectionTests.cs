using Kofrenot.Core.Tests.Server;

namespace Kofrenot.Core.Tests.Session;

public sealed class ServerConnectionTests : IDisposable
{
    private const string Email = "ada@example.com";

    private readonly SessionFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task WithoutAnAccount_ThereIsNoClient_AndNothingToRefresh()
    {
        using var connection = _fixture.Connection();

        await connection.RefreshAsync();

        Assert.Null(connection.Account);
        Assert.Null(connection.Client);
        Assert.False(connection.IsSignedIn);
        Assert.Null(connection.Identity);
        Assert.Empty(_fixture.Http.Requests);
    }

    [Fact]
    public async Task SigningIn_SetsTheAccount_KeepsTheSession_AndTellsItOnTheUiThread()
    {
        using var connection = _fixture.Connection();
        var changed = 0;
        connection.Changed += () => changed++;

        await connection.SignInAsync(StubHandler.Server, Email, "secret password");

        Assert.True(connection.IsSignedIn);
        Assert.Equal(new ServerAccount(StubHandler.Server, Email), _fixture.Settings.Server);
        Assert.Equal("access-1", _fixture.Sessions.Recall(StubHandler.Server)?.AccessToken);
        Assert.Equal(0, changed);
        _fixture.Ui.RunAll();
        Assert.True(changed > 0);
    }

    [Fact]
    public async Task TheSession_IsKeptAcrossRestarts()
    {
        using (var first = _fixture.Connection())
        {
            await first.SignInAsync(StubHandler.Server, Email, "secret password");
        }

        using var second = _fixture.Connection();

        Assert.True(second.IsSignedIn);
        Assert.Equal(StubHandler.Server, second.Account?.Server);
    }

    [Fact]
    public async Task SigningUp_SetsTheAccount_SignedOut()
    {
        using var connection = _fixture.Connection();

        await connection.SignUpAsync(StubHandler.Server, Email, "secret password");

        Assert.Equal(Email, connection.Account?.Email);
        Assert.False(connection.IsSignedIn);
    }

    [Fact]
    public async Task SigningOut_ForgetsTheSession()
    {
        using var connection = _fixture.Connection();
        await connection.SignInAsync(StubHandler.Server, Email, "secret password");

        connection.SignOut();

        Assert.False(connection.IsSignedIn);
        Assert.Null(_fixture.Sessions.Recall(StubHandler.Server));
    }

    [Fact]
    public async Task AnotherEmail_IsANewAccount_SignedOutFirst()
    {
        using var connection = _fixture.Connection();
        await connection.SignInAsync(StubHandler.Server, Email, "secret password");
        var before = connection.Client;

        await connection.SignUpAsync(StubHandler.Server, "grace@example.com", "secret password");

        Assert.NotSame(before, connection.Client);
        Assert.Equal("grace@example.com", connection.Account?.Email);
        Assert.False(connection.IsSignedIn);
        Assert.Contains(_fixture.Log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("Server account set"));
    }

    [Fact]
    public async Task AServerOlderThan02_IsSaidTooOld_Once()
    {
        _fixture.Version = null;
        using var connection = _fixture.Connection();
        await connection.SignInAsync(StubHandler.Server, Email, "secret password");
        _fixture.Ui.RunAll();

        await connection.RefreshAsync();

        Assert.Equal(KofreVersion.TooOld(null), connection.TooOld);
        Assert.Equal(0, _fixture.Ui.Waiting);
        Assert.Single(_fixture.Log.Entries, entry => entry.Message.Contains("Server too old"));
    }

    [Fact]
    public async Task ARecentServer_IsNotTooOld_AndItsAccountDataIsRead()
    {
        using var connection = _fixture.Connection();

        await connection.SignInAsync(StubHandler.Server, Email, "secret password");

        Assert.Null(connection.TooOld);
        Assert.Contains(_fixture.Http.Requests, request => request.Path == "/v1/accounts/me/data");
    }

    [Fact]
    public async Task AServerThatCannotBeReached_LeavesWhatWasKnown()
    {
        using var connection = _fixture.Connection();
        await connection.SignInAsync(StubHandler.Server, Email, "secret password");
        _fixture.Offline = true;

        await connection.RefreshAsync();

        Assert.True(connection.IsSignedIn);
        Assert.Null(connection.TooOld);
        Assert.Contains(_fixture.Log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("cannot be read"));
    }
}
