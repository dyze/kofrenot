using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// A client that edits content only (<see cref="ClientMode.Content"/>, kofrenot for Android): it
/// saves notes and imports files, and makes no other change, so it cannot leave a vault in a state
/// another kofrenot cannot fix. A vault that needs a write before it can be used (an upgrade, a
/// hand-over to the account) is left to kofrenot on Windows.
/// </summary>
public sealed partial class VaultSession
{
    private const string ContentOnlyMessage = "This kofrenot edits notes and files only: make this change in kofrenot on Windows.";

    /// <summary>What this client may change.</summary>
    public ClientMode Mode { get; }

    /// <summary>This client changes notes' and datasets' content only (<see cref="ClientMode.Content"/>).</summary>
    public bool EditsContentOnly => Mode == ClientMode.Content;

    /// <summary>True, the user told, when this client does not make a change other than to content.</summary>
    private bool RefusesChanges(ISessionQuestions ask)
    {
        if (!EditsContentOnly)
        {
            return false;
        }

        _log.Info("Action", "Change refused: this kofrenot edits notes and files only");
        ask.Tell(ContentOnlyMessage);
        return true;
    }

    /// <summary>False, the user told, when this client edits content only and the vault needs another write first.</summary>
    private bool CanUseHere(ISessionQuestions ask, string folder, Vault vault)
    {
        var why = !EditsContentOnly ? null
            : vault.NeedsUpgrade ? "It needs an upgrade: open it once in kofrenot on Windows first."
            : !NeedsAttach(folder) ? null
            : _server.Identity is null ? "Choose your server account's pass phrase in kofrenot on Windows first."
            : "It still opens with its own pass phrase: open it once in kofrenot on Windows first.";
        if (why is null)
        {
            return true;
        }

        _log.Warn(Category, $"Vault {vault.Id} not opened: it needs a change this kofrenot does not make");
        ask.Tell($"This vault cannot be opened here.\n\n{why}");
        return false;
    }
}
