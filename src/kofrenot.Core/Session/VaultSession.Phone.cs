using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// kofrenot for Android (<see cref="ClientMode.Phone"/>): it changes the vault's nodes and makes no
/// change that defines the vault (<see cref="PhoneChanges"/>), so it cannot leave a vault in a state
/// another kofrenot cannot fix. A vault that needs a write before it can be used (an upgrade, a
/// hand-over to the account), a vault created, moved or deleted, and the account's pass phrase
/// are left to kofrenot on Windows.
/// </summary>
public sealed partial class VaultSession
{
    private const string OnWindowsMessage = "This change is made in kofrenot on Windows.";

    /// <summary>What this client may change.</summary>
    public ClientMode Mode { get; }

    /// <summary>This client is kofrenot for Android (<see cref="ClientMode.Phone"/>).</summary>
    public bool IsPhone => Mode == ClientMode.Phone;

    /// <summary>True, the user told, when this client is the phone, which leaves this change (to a vault, an account) to Windows.</summary>
    private bool RefusesChanges(ISessionQuestions ask)
    {
        if (!IsPhone)
        {
            return false;
        }

        _log.Info("Action", "Change refused: made in kofrenot on Windows");
        ask.Tell(OnWindowsMessage);
        return true;
    }

    /// <summary>True, the user told, when this client is the phone and one of <paramref name="changes"/> defines the vault.</summary>
    private bool RefusesChanges(ISessionQuestions ask, Vault vault, IReadOnlyList<Change> changes) =>
        changes.Any(change => !PhoneChanges.Allows(change, vault.Graph)) && RefusesChanges(ask);

    /// <summary>False, the user told, when this client is the phone and the vault needs another write first.</summary>
    private bool CanUseHere(ISessionQuestions ask, string folder, Vault vault)
    {
        var why = !IsPhone ? null
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
