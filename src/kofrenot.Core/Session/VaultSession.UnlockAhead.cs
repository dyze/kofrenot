using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// A vault's pass phrase checked before the vault is opened, in the background: for a client
/// with no dialog that waits while the key is derived (kofrenot for Android). The next opening of
/// that vault uses what it unlocked, once, instead of asking.
/// </summary>
public sealed partial class VaultSession
{
    public const string WrongPassPhrase = "Wrong pass phrase.";
    private const string OlderFilesOnly = "This pass phrase opens only the vault's older files: type your server account's pass phrase.";

    // What the pass phrase typed ahead unlocked, until the next unlock takes it.
    private (string Folder, VaultAccess Access, bool Remember)? _unlockedAhead;

    /// <summary>
    /// Checks the pass phrase of the vault at <paramref name="location"/> off the UI thread. Why it
    /// was refused; null when taken, or when the vault cannot be read (its opening then tells why).
    /// </summary>
    public async Task<string?> UnlockAheadAsync(VaultLocation location, string passPhrase, bool remember)
    {
        _unlockedAhead = null;
        var folder = location.Folder;
        VaultHeader header;
        try
        {
            header = VaultHeader.Read(VaultFolder.HeaderOf(folder));
        }
        catch (Exception ex) when (ex is NewerVaultFormatException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var (account, signer) = (AccountIdentityOf(location), SignerOf(location, header.Id));
        var (access, isCurrent) = await Task.Run(() =>
            VaultKeys.Unlock(folder, header, passPhrase, _log, account, signer) is { } unlocked
                ? (unlocked, VaultKeys.IsCurrent(folder, unlocked, unlocked.Identity is { } identity ? signer?.KeyFor(identity) : null))
                : (null, false));
        if (access is null)
        {
            _log.Warn(Category, $"Wrong pass phrase for vault {header.Id}");
            return WrongPassPhrase;
        }

        if (!isCurrent)
        {
            return OlderFilesOnly;
        }

        _unlockedAhead = (folder, access, remember);
        return null;
    }

    /// <summary>What the pass phrase typed ahead unlocked in <paramref name="folder"/>, taken once; null when none.</summary>
    private (VaultAccess Access, bool Remember)? TakeUnlockedAhead(string folder, VaultHeader header)
    {
        var ahead = _unlockedAhead;
        _unlockedAhead = null;
        return ahead is { } taken && taken.Folder == folder && taken.Access.Header.Id == header.Id ? (taken.Access, taken.Remember) : null;
    }
}
