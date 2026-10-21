namespace Kofrenot.Core.Server;

/// <summary>Why a call to the server failed, in terms the app can act on.</summary>
public enum ServerFailure
{
    /// <summary>The server cannot be reached: kofrenot keeps working offline and tries again later.</summary>
    Offline,

    /// <summary>No session, or it ended (expired, password changed): the user signs in again.</summary>
    SignInNeeded,

    /// <summary>Not allowed: wrong password, account not approved or disabled, not a member, store deleted.</summary>
    Refused,

    NotFound,

    /// <summary>Already there: an email taken, a file uploaded before.</summary>
    Conflict,

    TooLarge,

    /// <summary>Too many attempts: try again in a minute.</summary>
    TooManyRequests,

    /// <summary>Anything else: a request the server did not understand, or an error of its own.</summary>
    Failed,
}

/// <summary>A failed call to the server: its <see cref="Failure"/>, and the server's own error code when it gave one.</summary>
public sealed class ServerException(ServerFailure failure, string? code = null, Exception? inner = null)
    : Exception(code is null ? failure.ToString() : $"{failure}: {code}", inner)
{
    public ServerFailure Failure { get; } = failure;

    /// <summary>The server's error code ("wrong-password", "account-not-approved"...), when it gave one.</summary>
    public string? Code { get; } = code;
}
