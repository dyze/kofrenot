using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Session;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Platform;

/// <summary>A pass phrase the session asked for and did not get: where, and its hint.</summary>
public sealed record PassPhraseNeeded(string Place, string? Hint);

/// <summary>
/// The session's questions, answered before it asks them: Android has no dialog that waits for
/// an answer. A pass phrase asked is noted in <see cref="Needed"/>: a screen asks it, checks it
/// (<see cref="VaultSession.UnlockAheadAsync"/>), then opens the vault again. The phone only
/// reads, so every other question is declined.
/// </summary>
public sealed class PreparedAnswers : ISessionQuestions
{
    public List<string> Told { get; } = [];

    public PassPhraseNeeded? Needed { get; private set; }

    public void Tell(string message) => Told.Add(message);

    public bool Confirm(string message, string yes, string no = "Cancel") => false;

    public UnlockAnswer? AskPassPhrase(string place, Func<string, bool> accept, string? hint = null)
    {
        Needed = new PassPhraseNeeded(place, hint);
        return null;
    }

    public NewVault? AskNewPassPhrase(string place, string name, Func<string, bool> accept) => null;

    public NewVault? AskNewOnServer(string place, bool accountHasPassPhrase, Func<string, bool> accept) => null;

    public bool AskToUpgrade(string place, Func<string, bool> accept) => false;

    public bool AskToChange(string place, Func<string, bool> accept) => false;

    public bool AskAccount(string place, bool exists, string hint, Func<string, bool> accept) => false;

    public bool AskToChangeAccount(string place, Func<string, bool> accept) => false;

    public void ShowRecoveryKey(string place, RecoveryKey key)
    {
    }

    public bool? AskRecoveryKey(string place, Func<RecoveryKey, bool> accept) => null;

    public ShareChoice? AskShare(string place, Func<string, Task<(Lookup Result, ShareCandidate? Candidate)>> find) => null;

    public bool AskCreation(CreationForm form, string? file) => false;

    public ViewEdit? AskView(KnowledgeGraph graph, Node view, ViewState loaded) => null;
}
