namespace Kofrenot.Core.Tests.Storage;

public class ContentStateTests
{
    [Theory]
    [InlineData(ContentLevel.Here, null, "")]
    [InlineData(ContentLevel.NotHere, null, "Not synced to this computer yet.")]
    [InlineData(ContentLevel.Downloading, null, "Downloading…")]
    [InlineData(ContentLevel.Failed, ServerFailure.Offline, "Not downloaded: offline.")]
    [InlineData(ContentLevel.Failed, ServerFailure.SignInNeeded, "Not downloaded: sign in.")]
    [InlineData(ContentLevel.Failed, ServerFailure.Failed, "Not downloaded.")]
    public void Summary_SaysItInAFewWords(ContentLevel level, ServerFailure? failure, string expected) =>
        Assert.Equal(expected, new ContentState(level, failure).Summary);

    [Theory]
    [InlineData(ContentLevel.Here, null, "")]
    [InlineData(ContentLevel.NotHere, null, "This note is not synced to this computer yet.")]
    [InlineData(ContentLevel.Downloading, null, "Downloading this note from the server…")]
    [InlineData(ContentLevel.Failed, ServerFailure.Offline, "This note cannot be downloaded: the server cannot be reached. kofrenot tries again at the next sync.")]
    [InlineData(ContentLevel.Failed, ServerFailure.SignInNeeded, "This note cannot be downloaded: signed out of the server. Sign in (Settings > Server) to get it.")]
    [InlineData(ContentLevel.Failed, ServerFailure.Refused, "This note cannot be downloaded: the server refused it.")]
    [InlineData(ContentLevel.Failed, ServerFailure.NotFound, "This note cannot be downloaded: the server refused it.")]
    [InlineData(ContentLevel.Failed, ServerFailure.Failed, "This note could not be downloaded (the log window says why). kofrenot tries again at the next sync.")]
    public void Describe_SaysWhatHappensNext(ContentLevel level, ServerFailure? failure, string expected) =>
        Assert.Equal(expected, new ContentState(level, failure).Describe("this note"));
}
