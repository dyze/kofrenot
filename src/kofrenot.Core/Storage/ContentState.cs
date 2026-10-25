using Kofrenot.Core.Server;

namespace Kofrenot.Core.Storage;

/// <summary>Where a note's or dataset's content stands on this device.</summary>
public enum ContentLevel
{
    /// <summary>Its file is here: it can be read.</summary>
    Here,

    /// <summary>Not here: not synced yet (a folder vault), or not asked for yet (a server vault).</summary>
    NotHere,

    /// <summary>Being downloaded from the server.</summary>
    Downloading,

    /// <summary>Its download failed (<see cref="ContentState.Failure"/>): tried again at the next sync, or when the user asks.</summary>
    Failed,
}

/// <summary>Where a version's content stands (<see cref="Vault.StateOf"/>).</summary>
/// <param name="Failure">Why its download failed; null unless <see cref="ContentLevel.Failed"/>.</param>
public sealed record ContentState(ContentLevel Level, ServerFailure? Failure = null)
{
    public static readonly ContentState Here = new(ContentLevel.Here);

    public static readonly ContentState NotHere = new(ContentLevel.NotHere);

    public static readonly ContentState Downloading = new(ContentLevel.Downloading);

    /// <summary>"Downloading…", "Not downloaded: offline"...: a few words, on a card.</summary>
    public string Summary => (Level, Failure) switch
    {
        (ContentLevel.Here, _) => string.Empty,
        (ContentLevel.Downloading, _) => "Downloading…",
        (ContentLevel.Failed, ServerFailure.Offline) => "Not downloaded: offline.",
        (ContentLevel.Failed, ServerFailure.SignInNeeded) => "Not downloaded: sign in.",
        (ContentLevel.Failed, _) => "Not downloaded.",
        _ => "Not synced to this computer yet.",
    };

    /// <summary>What is shown instead of the content of <paramref name="what"/> ("this note", "this file"), and what happens next.</summary>
    public string Describe(string what) => (Level, Failure) switch
    {
        (ContentLevel.Here, _) => string.Empty,
        (ContentLevel.Downloading, _) => $"Downloading {what} from the server…",
        (ContentLevel.Failed, ServerFailure.Offline) => $"{Capital(what)} cannot be downloaded: the server cannot be reached. kofrenot tries again at the next sync.",
        (ContentLevel.Failed, ServerFailure.SignInNeeded) => $"{Capital(what)} cannot be downloaded: signed out of the server. Sign in (Settings > Server) to get it.",
        (ContentLevel.Failed, ServerFailure.Refused or ServerFailure.NotFound) => $"{Capital(what)} cannot be downloaded: the server refused it.",
        (ContentLevel.Failed, _) => $"{Capital(what)} could not be downloaded (the log window says why). kofrenot tries again at the next sync.",
        _ => $"{Capital(what)} is not synced to this computer yet.",
    };

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
