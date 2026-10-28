using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// Reads another registered vault without switching to it: to copy its node types into the open
/// one. It is opened from its folder (a server vault: as last synced), unlocked as when it is
/// opened, and never written to.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>
    /// The user's node types of <paramref name="vault"/>, another registered vault; null when it
    /// cannot be read (the user is told) or its unlock is cancelled.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="vault"/> is the open vault.</exception>
    public IReadOnlyList<CustomNodeType>? NodeTypesOf(ISessionQuestions ask, RegisteredVault vault)
    {
        if (vault.Location == _settings.SelectedVault)
        {
            throw new ArgumentException("The open vault's node types are in its graph", nameof(vault));
        }

        var folder = vault.Location.Folder;
        try
        {
            var header = VaultHeader.Read(VaultFolder.HeaderOf(folder));
            if (UnlockKeys(ask, vault, folder, header) is not { } unlocked)
            {
                return null;
            }

            if (unlocked.Remember)
            {
                _keys.Remember(unlocked.Access);
            }

            var read = Vault.Open(folder, header, unlocked.Access.Keys, _files.Cache, _log);
            _log.Info("Action", $"Node types read from vault {header.Id}: {read.Graph.NodeTypes.All.Count}");
            return read.Graph.NodeTypes.All;
        }
        catch (NewerVaultFormatException ex)
        {
            _log.Warn(Category, $"Vault in {folder} not read: format {ex.Format}");
            ask.Tell("This vault was written by a newer kofrenot: update kofrenot to read it.");
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _log.Error(Category, $"Cannot read the vault in {folder}", ex);
            ask.Tell($"The vault in {folder} cannot be read.\n\n{ex.Message}");
            return null;
        }
    }
}
