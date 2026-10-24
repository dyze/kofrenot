using System.Globalization;

namespace Kofrenot.Core.Tests.Server;

public sealed class SyncStatusTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 24, 17, 42, 0, TimeSpan.Zero);
    private static readonly string Time = At.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    [Theory]
    [InlineData(true, null, 0, SyncLevel.Synced)]
    [InlineData(true, null, 2, SyncLevel.Waiting)]
    [InlineData(false, null, 0, SyncLevel.Waiting)]
    [InlineData(true, ServerFailure.Offline, 0, SyncLevel.Offline)]
    [InlineData(false, ServerFailure.SignInNeeded, 0, SyncLevel.Problem)]
    [InlineData(true, ServerFailure.NotFound, 0, SyncLevel.Problem)]
    [InlineData(true, ServerFailure.Failed, 1, SyncLevel.Problem)]
    public void Level_GoesFromFine_ToNeedingTheUser(bool synced, ServerFailure? failure, int waiting, SyncLevel level)
    {
        Assert.Equal(level, new SyncStatus(synced ? At : null, failure, waiting).Level);
    }

    [Fact]
    public void Synced_SaysWhen()
    {
        var status = new SyncStatus(At, null, 0);

        Assert.Equal($"Synced {Time}", status.Summary);
        Assert.Equal($"Synced with the server at {Time}.", status.Details);
        Assert.Equal("synced", status.Describe());
    }

    [Fact]
    public void NotSyncedYet_SaysSo()
    {
        var status = new SyncStatus(null, null, 0);

        Assert.Equal("Not synced yet", status.Summary);
        Assert.Equal("Not synced yet since kofrenot started.", status.Details);
        Assert.Equal("not synced yet", status.Describe());
    }

    [Fact]
    public void Offline_CountsTheFilesWaiting_AndSaysItTriesAgain()
    {
        var status = new SyncStatus(At, ServerFailure.Offline, 2);

        Assert.Equal("Offline · 2 to upload", status.Summary);
        Assert.Equal(
            $"The server cannot be reached: you keep working on this computer, and kofrenot tries again every 30 seconds. Last synced at {Time}. 2 files wait to be uploaded.",
            status.Details);
        Assert.Equal("offline, 2 files to upload", status.Describe());
    }

    [Fact]
    public void OneFileWaiting_IsSingular()
    {
        var status = new SyncStatus(null, ServerFailure.Failed, 1);

        Assert.Equal("Sync failed · 1 to upload", status.Summary);
        Assert.EndsWith(" 1 file waits to be uploaded.", status.Details);
        Assert.StartsWith("The last sync failed", status.Details);
        Assert.Equal("sync failed, 1 file to upload", status.Describe());
    }

    [Fact]
    public void SignedOut_SaysWhereToSignIn()
    {
        var status = new SyncStatus(null, ServerFailure.SignInNeeded, 0);

        Assert.Equal("Sign in to sync", status.Summary);
        Assert.Equal("Signed out of the server: sign in (Settings > Server) to sync.", status.Details);
        Assert.Equal("sign in to sync", status.Describe());
    }

    [Theory]
    [InlineData(ServerFailure.Refused)]
    [InlineData(ServerFailure.NotFound)]
    public void AVaultGoneFromTheServer_CountsNoFileToUpload(ServerFailure failure)
    {
        var status = new SyncStatus(At, failure, 3);

        Assert.Equal("Not on the server", status.Summary);
        Assert.StartsWith("The server no longer has this vault", status.Details);
        Assert.Equal("no longer on the server, or no longer shared with you, 3 files to upload", status.Describe());
    }
}
