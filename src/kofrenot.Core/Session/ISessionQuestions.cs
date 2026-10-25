using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Session;

/// <summary>What the user chose for a new vault: its name, and whether to remember its pass phrase.</summary>
public sealed record NewVault(string Name, bool Remember);

/// <summary>How the user unlocks a vault: with the pass phrase typed (remembered or not), or with the recovery key.</summary>
public sealed record UnlockAnswer(bool Remember, bool UseRecoveryKey);

/// <summary>Who to share a vault with, and their role, once the fingerprint of their key was compared.</summary>
public sealed record ShareChoice(ShareCandidate Candidate, MemberRole Role);

/// <summary>What a view's edit saves: the definition, and the rename when its name was changed.</summary>
public sealed record ViewEdit(ViewDefinition Definition, NodeRenamed? Rename);

/// <summary>
/// What the session tells the user and asks them, in the client's own windows. A pass phrase or
/// recovery key typed is handed to <c>accept</c> (in the background: deriving a key takes a
/// moment) while the question stays open; refused, or throwing an <see cref="IOException"/>
/// whose message says why, the question stays open.
/// </summary>
public interface ISessionQuestions
{
    /// <summary>Tells the user something: why an action failed, or what it did.</summary>
    void Tell(string message);

    /// <summary>Asks to confirm an action; true for <paramref name="yes"/>.</summary>
    bool Confirm(string message, string yes, string no = "Cancel");

    /// <summary>The pass phrase of a vault, to unlock it; null when cancelled.</summary>
    UnlockAnswer? AskPassPhrase(string place, Func<string, bool> accept, string? hint = null);

    /// <summary>A new vault's name and pass phrase (typed twice); null when cancelled.</summary>
    NewVault? AskNewPassPhrase(string place, string name, Func<string, bool> accept);

    /// <summary>A new server vault's name and the account's pass phrase (chosen when it has none); null when cancelled.</summary>
    NewVault? AskNewOnServer(string place, bool accountHasPassPhrase, Func<string, bool> accept);

    /// <summary>The pass phrase again, to upgrade the vault's keys; false when cancelled.</summary>
    bool AskToUpgrade(string place, Func<string, bool> accept);

    /// <summary>A new pass phrase for the vault (typed twice); false when cancelled.</summary>
    bool AskToChange(string place, Func<string, bool> accept);

    /// <summary>The server account's pass phrase: typed, or chosen when it has none; false when cancelled.</summary>
    bool AskAccount(string place, bool exists, string hint, Func<string, bool> accept);

    /// <summary>A new pass phrase for the server account (typed twice); false when cancelled.</summary>
    bool AskToChangeAccount(string place, Func<string, bool> accept);

    /// <summary>Shows a recovery key, once, for the user to keep.</summary>
    void ShowRecoveryKey(string place, RecoveryKey key);

    /// <summary>The recovery key, to unlock the vault: whether to remember what it unlocks; null when cancelled.</summary>
    bool? AskRecoveryKey(string place, Func<RecoveryKey, bool> accept);

    /// <summary>Who to share the vault with, found by email with <paramref name="find"/>; null when cancelled.</summary>
    ShareChoice? AskShare(string place, Func<string, Task<(Lookup Result, ShareCandidate? Candidate)>> find);

    /// <summary>Shows the creation screen for <paramref name="form"/> (with a dropped <paramref name="file"/>); false when cancelled.</summary>
    bool AskCreation(CreationForm form, string? file);

    /// <summary>A view's name and what it shows, edited; null when cancelled.</summary>
    ViewEdit? AskView(KnowledgeGraph graph, Node view, ViewState loaded);
}

/// <summary>Watches a vault folder for change files added by another app or the sync tool.</summary>
public interface IVaultWatcher
{
    /// <summary>
    /// Calls <paramref name="changed"/> on the UI thread once change files stop arriving for a moment.
    /// Disposing it stops.
    /// </summary>
    /// <exception cref="ArgumentException">The folder cannot be watched.</exception>
    /// <exception cref="IOException">The folder cannot be watched.</exception>
    IDisposable Watch(string folder, Action changed);
}
