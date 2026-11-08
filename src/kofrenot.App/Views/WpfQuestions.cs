using System.Windows;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Session;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>The session's questions, as kofrenot's dialogs over <paramref name="window"/>, or over the main window once it is closed.</summary>
public sealed class WpfQuestions(Window window) : ISessionQuestions
{
    private Window Owner => DialogOwner.Of(window);

    public void Tell(string message) => MessageDialog.Inform(Owner, message);

    public bool Confirm(string message, string yes, string no = "Cancel") => MessageDialog.Confirm(Owner, message, yes, no);

    public UnlockAnswer? AskPassPhrase(string place, Func<string, bool> accept, string? hint = null) => PassphraseDialog.Ask(Owner, place, accept, hint);

    public NewVault? AskNewPassPhrase(string place, string name, Func<string, bool> accept) => PassphraseDialog.AskNew(Owner, place, name, accept);

    public NewVault? AskNewOnServer(string place, bool accountHasPassPhrase, Func<string, bool> accept) =>
        PassphraseDialog.AskNewOnServer(Owner, place, accountHasPassPhrase, accept);

    public bool AskToUpgrade(string place, Func<string, bool> accept) => PassphraseDialog.AskToUpgrade(Owner, place, accept);

    public bool AskToChange(string place, Func<string, bool> accept) => PassphraseDialog.AskToChange(Owner, place, accept);

    public bool AskAccount(string place, bool exists, string hint, Func<string, bool> accept) => PassphraseDialog.AskAccount(Owner, place, exists, hint, accept);

    public bool AskToChangeAccount(string place, Func<string, bool> accept) => PassphraseDialog.AskToChangeAccount(Owner, place, accept);

    public void ShowRecoveryKey(string place, RecoveryKey key) => RecoveryKeyDialog.Show(Owner, place, key);

    public bool? AskRecoveryKey(string place, Func<RecoveryKey, bool> accept) => RecoveryKeyDialog.Ask(Owner, place, accept);

    public ShareChoice? AskShare(string place, Func<string, Task<(Lookup Result, ShareCandidate? Candidate)>> find) => ShareDialog.Ask(Owner, place, find);

    public bool AskCreation(CreationForm form, string? file) => CreationDialog.Ask(Owner, form, file);

    public ViewEdit? AskView(KnowledgeGraph graph, Node view, ViewState loaded) => ViewDialog.Ask(Owner, graph, view, loaded);
}
