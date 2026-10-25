using System.Windows;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Session;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>The session's questions, as kofrenot's dialogs over <paramref name="owner"/>.</summary>
public sealed class WpfQuestions(Window owner) : ISessionQuestions
{
    public void Tell(string message) => MessageDialog.Inform(owner, message);

    public bool Confirm(string message, string yes, string no = "Cancel") => MessageDialog.Confirm(owner, message, yes, no);

    public UnlockAnswer? AskPassPhrase(string place, Func<string, bool> accept, string? hint = null) => PassphraseDialog.Ask(owner, place, accept, hint);

    public NewVault? AskNewPassPhrase(string place, string name, Func<string, bool> accept) => PassphraseDialog.AskNew(owner, place, name, accept);

    public NewVault? AskNewOnServer(string place, bool accountHasPassPhrase, Func<string, bool> accept) =>
        PassphraseDialog.AskNewOnServer(owner, place, accountHasPassPhrase, accept);

    public bool AskToUpgrade(string place, Func<string, bool> accept) => PassphraseDialog.AskToUpgrade(owner, place, accept);

    public bool AskToChange(string place, Func<string, bool> accept) => PassphraseDialog.AskToChange(owner, place, accept);

    public bool AskAccount(string place, bool exists, string hint, Func<string, bool> accept) => PassphraseDialog.AskAccount(owner, place, exists, hint, accept);

    public bool AskToChangeAccount(string place, Func<string, bool> accept) => PassphraseDialog.AskToChangeAccount(owner, place, accept);

    public void ShowRecoveryKey(string place, RecoveryKey key) => RecoveryKeyDialog.Show(owner, place, key);

    public bool? AskRecoveryKey(string place, Func<RecoveryKey, bool> accept) => RecoveryKeyDialog.Ask(owner, place, accept);

    public ShareChoice? AskShare(string place, Func<string, Task<(Lookup Result, ShareCandidate? Candidate)>> find) => ShareDialog.Ask(owner, place, find);

    public bool AskCreation(CreationForm form, string? file) => CreationDialog.Ask(owner, form, file);

    public ViewEdit? AskView(KnowledgeGraph graph, Node view, ViewState loaded) => ViewDialog.Ask(owner, graph, view, loaded);
}
