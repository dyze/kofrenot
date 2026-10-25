using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// A client that only reads (kofrenot for Android): it never writes to a vault or to the server,
/// so it cannot leave a vault in a state another kofrenot cannot fix. A vault that needs a write
/// before it can be used (an upgrade, a hand-over to the account) is left to kofrenot on Windows.
/// </summary>
public sealed partial class VaultSession
{
    private const string ReadsOnlyMessage = "This kofrenot reads only: make this change in kofrenot on Windows.";

    /// <summary>This client never writes; set by its <see cref="SyncService"/>, which then only downloads.</summary>
    public bool ReadsOnly => _sync.DownloadsOnly;

    /// <summary>True, the user told, when this client only reads.</summary>
    private bool RefusesChanges(ISessionQuestions ask)
    {
        if (!ReadsOnly)
        {
            return false;
        }

        _log.Info("Action", "Change refused: this kofrenot reads only");
        ask.Tell(ReadsOnlyMessage);
        return true;
    }

    /// <summary>False, the user told, when this client only reads and the vault needs a write first.</summary>
    private bool CanUseHere(ISessionQuestions ask, string folder, Vault vault)
    {
        var why = !ReadsOnly ? null
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
