using System.Globalization;

namespace Kofrenot.Core.Server;

/// <summary>How a server vault's sync stands, from fine to needing the user.</summary>
public enum SyncLevel
{
    /// <summary>It went through, and nothing waits.</summary>
    Synced,

    /// <summary>Not synced yet, or files wait for the next sync.</summary>
    Waiting,

    /// <summary>The server cannot be reached: kofrenot works offline and tries again.</summary>
    Offline,

    /// <summary>It needs the user: signed out, the vault gone from the server, or a failure.</summary>
    Problem,
}

/// <summary>Where a server vault's sync stands.</summary>
/// <param name="Synced">When it last went through; null when not yet since kofrenot started.</param>
/// <param name="Failure">Why the last one failed; null when it went through.</param>
/// <param name="Waiting">Files of the mirror the store does not have yet.</param>
public sealed record SyncStatus(DateTimeOffset? Synced, ServerFailure? Failure, int Waiting)
{
    /// <summary>How often the server vaults are synced, and tried again while offline.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

    public SyncLevel Level => Failure switch
    {
        null when Synced is not null && Waiting == 0 => SyncLevel.Synced,
        null => SyncLevel.Waiting,
        ServerFailure.Offline => SyncLevel.Offline,
        _ => SyncLevel.Problem,
    };

    /// <summary>"synced", "offline, 3 files to upload", "sign in to sync"...: within a sentence.</summary>
    public string Describe()
    {
        var state = Failure switch
        {
            null when Synced is null => "not synced yet",
            null => "synced",
            ServerFailure.Offline => "offline",
            ServerFailure.SignInNeeded => "sign in to sync",
            ServerFailure.Refused or ServerFailure.NotFound => "no longer on the server, or no longer shared with you",
            _ => "sync failed",
        };
        return Waiting > 0 ? $"{state}, {Files} to upload" : state;
    }

    /// <summary>"Synced 17:42", "Offline · 2 to upload", "Sign in to sync"...: a few words, on their own.</summary>
    public string Summary
    {
        get
        {
            var state = Failure switch
            {
                null when Synced is null => "Not synced yet",
                null => $"Synced {Time(Synced.Value)}",
                ServerFailure.Offline => "Offline",
                ServerFailure.SignInNeeded => "Sign in to sync",
                ServerFailure.Refused or ServerFailure.NotFound => "Not on the server",
                _ => "Sync failed",
            };
            return Waiting > 0 && Failure is not (ServerFailure.Refused or ServerFailure.NotFound) ? $"{state} · {Waiting} to upload" : state;
        }
    }

    /// <summary>What happened, and what kofrenot or the user does next.</summary>
    public string Details
    {
        get
        {
            var retry = $"kofrenot tries again every {Every.TotalSeconds:0} seconds.";
            var state = Failure switch
            {
                null when Synced is null => "Not synced yet since kofrenot started.",
                null => $"Synced with the server at {Time(Synced.Value)}.",
                ServerFailure.Offline => $"The server cannot be reached: you keep working on this computer, and {retry}",
                ServerFailure.SignInNeeded => "Signed out of the server: sign in (Settings > Server) to sync.",
                ServerFailure.Refused or ServerFailure.NotFound => "The server no longer has this vault, or no longer shares it with you.",
                _ => $"The last sync failed (the log window says why); {retry}",
            };
            var last = Failure is not null && Synced is { } synced ? $" Last synced at {Time(synced)}." : string.Empty;
            var waiting = Waiting > 0 ? $" {Files} {(Waiting == 1 ? "waits" : "wait")} to be uploaded." : string.Empty;
            return state + last + waiting;
        }
    }

    private string Files => $"{Waiting} {(Waiting == 1 ? "file" : "files")}";

    private static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
}
