using Kofrenot.Core.Sharing;
using ViewState = Kofrenot.Core.Views.ViewState;

namespace Kofrenot.Core.Tests.Session;

/// <summary>
/// The user's answers, set by the test: a pass phrase typed is handed to the session's check,
/// and refused (or failing, its message kept in <see cref="Refusals"/>), the user gives up.
/// </summary>
internal sealed class ScriptedQuestions : ISessionQuestions
{
    public const string FirstPassPhrase = "first pass phrase";

    public List<string> Told { get; } = [];

    /// <summary>What was asked, in order: the question's name, or a confirmation's message.</summary>
    public List<string> Asked { get; } = [];

    public List<string> Refusals { get; } = [];

    public List<RecoveryKey> RecoveryKeys { get; } = [];

    /// <summary>The answer to every confirmation.</summary>
    public bool Agrees { get; set; } = true;

    /// <summary>Typed to unlock, create, upgrade, or for the account's pass phrase.</summary>
    public string PassPhrase { get; set; } = FirstPassPhrase;

    /// <summary>Typed as a new pass phrase.</summary>
    public string NewPassPhrase { get; set; } = "second pass phrase";

    public bool Remember { get; set; }

    public string Name { get; set; } = "Personal";

    /// <summary>Every pass phrase or recovery key question is cancelled.</summary>
    public bool Cancels { get; set; }

    /// <summary>Unlocking chooses the recovery key, then types <see cref="RecoveryKey"/>.</summary>
    public bool UsesRecoveryKey { get; set; }

    public RecoveryKey? RecoveryKey { get; set; }

    public Func<CreationForm, bool> Creation { get; set; } = _ => true;

    public Func<KnowledgeGraph, Node, ViewState, ViewEdit?> View { get; set; } = (_, _, _) => null;

    /// <summary>Who the vault is shared with; null cancels.</summary>
    public (string Email, MemberRole Role)? ShareWith { get; set; }

    public void Tell(string message) => Told.Add(message);

    public bool Confirm(string message, string yes, string no = "Cancel")
    {
        Asked.Add(message);
        return Agrees;
    }

    public UnlockAnswer? AskPassPhrase(string place, Func<string, bool> accept, string? hint = null)
    {
        if (UsesRecoveryKey)
        {
            Asked.Add(nameof(AskPassPhrase));
            return new UnlockAnswer(Remember, UseRecoveryKey: true);
        }

        return Typed(nameof(AskPassPhrase), accept, PassPhrase) ? new UnlockAnswer(Remember, UseRecoveryKey: false) : null;
    }

    public NewVault? AskNewPassPhrase(string place, string name, Func<string, bool> accept) =>
        Typed(nameof(AskNewPassPhrase), accept, PassPhrase) ? new NewVault(Name, Remember) : null;

    public NewVault? AskNewOnServer(string place, bool accountHasPassPhrase, Func<string, bool> accept) =>
        Typed(nameof(AskNewOnServer), accept, PassPhrase) ? new NewVault(Name, Remember) : null;

    public bool AskToUpgrade(string place, Func<string, bool> accept) => Typed(nameof(AskToUpgrade), accept, PassPhrase);

    public bool AskToChange(string place, Func<string, bool> accept) => Typed(nameof(AskToChange), accept, NewPassPhrase);

    public bool AskAccount(string place, bool exists, string hint, Func<string, bool> accept) => Typed(nameof(AskAccount), accept, PassPhrase);

    public bool AskToChangeAccount(string place, Func<string, bool> accept) => Typed(nameof(AskToChangeAccount), accept, NewPassPhrase);

    public void ShowRecoveryKey(string place, RecoveryKey key) => RecoveryKeys.Add(key);

    public bool? AskRecoveryKey(string place, Func<RecoveryKey, bool> accept)
    {
        Asked.Add(nameof(AskRecoveryKey));
        return !Cancels && RecoveryKey is { } key && accept(key) ? Remember : null;
    }

    public ShareChoice? AskShare(string place, Func<string, Task<(Lookup Result, ShareCandidate? Candidate)>> find)
    {
        Asked.Add(nameof(AskShare));
        return ShareWith is { } share && find(share.Email).GetAwaiter().GetResult() is (_, { } candidate) ? new ShareChoice(candidate, share.Role) : null;
    }

    public bool AskCreation(CreationForm form, string? file)
    {
        Asked.Add(nameof(AskCreation));
        return Creation(form);
    }

    public ViewEdit? AskView(KnowledgeGraph graph, Node view, ViewState loaded)
    {
        Asked.Add(nameof(AskView));
        return View(graph, view, loaded);
    }

    private bool Typed(string question, Func<string, bool> accept, string typed)
    {
        Asked.Add(question);
        if (Cancels)
        {
            return false;
        }

        try
        {
            if (accept(typed))
            {
                return true;
            }

            Refusals.Add("refused");
        }
        catch (IOException ex)
        {
            Refusals.Add(ex.Message);
        }

        return false;
    }
}
