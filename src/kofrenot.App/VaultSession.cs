using System.Windows;
using Kofrenot.Core;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Platform;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

/// <summary>
/// The vault kofrenot works on: the folder of the settings, opened with its remembered key or
/// its pass phrase (created when the folder is empty), watched for the changes of other apps.
/// </summary>
public sealed class VaultSession : IDisposable
{
    private const string Category = "Vault";

    private readonly SettingsStore _settings;
    private readonly AppFiles _files;
    private readonly KeyMemory _keys;
    private readonly ILog _log;
    private VaultWatcher? _watcher;

    public VaultSession(SettingsStore settings, AppFiles files, KeyMemory keys, ILog log)
    {
        _settings = settings;
        _files = files;
        _keys = keys;
        _log = log;
    }

    /// <summary>Raised on the UI thread when the vault is opened or closed, or has new changes.</summary>
    public event Action? Changed;

    /// <summary>The open vault, or null (none chosen, locked, or cannot be opened).</summary>
    public Vault? Current { get; private set; }

    public string? Folder => _settings.VaultFolder;

    /// <summary>Whether the open vault's key is remembered on this computer.</summary>
    public bool IsRemembered => Current is { } vault && _keys.Has(vault.Id);

    /// <summary>Opens the vault of the settings, asking its pass phrase unless it is remembered. Does nothing when one is open.</summary>
    public void Open(Window owner)
    {
        if (Current is not null || Folder is not { } folder)
        {
            return;
        }

        Current = OpenFolder(owner, folder);
        if (Current is null)
        {
            return;
        }

        try
        {
            _watcher = new VaultWatcher(folder, Refresh);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            _log.Warn(Category, $"The vault folder cannot be watched ({ex.Message}): changes of other apps are read when the window is shown");
        }

        Changed?.Invoke();
    }

    /// <summary>Uses another folder: the current vault is closed, then the new one opened.</summary>
    public void SwitchTo(Window owner, string folder)
    {
        Close();
        _settings.SetVaultFolder(folder);
        _log.Info("Settings", $"Vault folder: {folder}");
        Open(owner);
    }

    /// <summary>Reads the changes other apps have added.</summary>
    public void Refresh()
    {
        if (Current?.Refresh() > 0)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Shows the creation screen for a request over <paramref name="owner"/>, then adds what it
    /// says. False when no vault is open, or the user cancels, or it cannot be written.
    /// </summary>
    /// <param name="choices">The homonyms already chosen (by the input box); the types default to generic.</param>
    public bool Create(Window owner, InputRequest request, AddChoices? choices = null)
    {
        if (Current is not { } vault)
        {
            return false;
        }

        if (CreationDialog.Ask(owner, new CreationForm(vault.Graph, request, choices)) is not { } plan)
        {
            _log.Info("Action", "Creation cancelled");
            return false;
        }

        try
        {
            Add(vault, plan);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Action", "Cannot add to the vault", ex);
            MessageDialog.Inform(owner, $"Cannot add to the vault.\n\n{ex.Message}");
            return false;
        }
    }

    private void Add(Vault vault, AddPlan plan)
    {
        try
        {
            foreach (var change in plan.Changes)
            {
                vault.Append(change);
            }
        }
        finally
        {
            Changed?.Invoke();
        }

        _log.Info("Action", plan switch
        {
            LinkPlan link => $"Link added ({link.Type.Id}, {(link.Left.IsNew ? 1 : 0) + (link.Right?.IsNew == true ? 1 : 0)} new nodes)",
            NodePlan node => $"Node added ({node.Type.Id}{(node.NeedsConfirmation ? ", a duplicate" : string.Empty)})",
            _ => "Added",
        });
    }

    /// <summary>Forgets the open vault's remembered key: its pass phrase is asked at the next start.</summary>
    public void Forget()
    {
        if (Current is { } vault)
        {
            _keys.Forget(vault.Id);
        }
    }

    public void Close()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (Current is not null)
        {
            Current = null;
            _log.Info(Category, "Vault closed");
            Changed?.Invoke();
        }
    }

    public void Dispose() => _watcher?.Dispose();

    private Vault? OpenFolder(Window owner, string folder)
    {
        switch (VaultFolder.Inspect(folder))
        {
            case FolderKind.Empty:
                return Create(owner, folder);
            case FolderKind.Vault:
                return Unlock(owner, folder);
            default:
                _log.Warn(Category, $"Not a vault folder: {folder}");
                MessageDialog.Inform(owner, $"{folder} holds other files.\n\nChoose an empty folder for a new vault, or the folder of a kofrenot vault.");
                return null;
        }
    }

    private Vault? Create(Window owner, string folder)
    {
        (VaultHeader Header, byte[] Key)? created = null;
        var remember = PassphraseDialog.Ask(owner, create: true, folder, passPhrase =>
        {
            created = VaultFolder.Create(folder, passPhrase);
            return true;
        });
        if (remember is null || created is not { } vault)
        {
            _log.Info("Action", "Vault creation cancelled");
            return null;
        }

        _log.Info("Action", $"Vault {vault.Header.Id} created in {folder}");
        return Open(owner, folder, vault.Header, vault.Key, remember.Value);
    }

    private Vault? Unlock(Window owner, string folder)
    {
        VaultHeader header;
        try
        {
            header = VaultHeader.Read(VaultFolder.HeaderOf(folder));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _log.Error(Category, $"Cannot read the vault in {folder}", ex);
            MessageDialog.Inform(owner, $"The vault in {folder} cannot be opened.\n\n{ex.Message}");
            return null;
        }

        if (_keys.Recall(header.Id) is { } remembered)
        {
            _log.Info(Category, $"Vault {header.Id} unlocked with the remembered pass phrase");
            return Open(owner, folder, header, remembered, remember: false);
        }

        byte[]? key = null;
        var remember = PassphraseDialog.Ask(owner, create: false, folder, passPhrase =>
        {
            key = header.Unlock(passPhrase);
            if (key is null)
            {
                _log.Warn(Category, $"Wrong pass phrase for vault {header.Id}");
            }

            return key is not null;
        });
        if (remember is null || key is null)
        {
            _log.Info("Action", "Unlock cancelled");
            return null;
        }

        _log.Info("Action", $"Vault {header.Id} unlocked");
        return Open(owner, folder, header, key, remember.Value);
    }

    private Vault? Open(Window owner, string folder, VaultHeader header, byte[] key, bool remember)
    {
        try
        {
            if (remember)
            {
                _keys.Remember(header.Id, key);
            }

            return Vault.Open(folder, header, key, _files.Cache, _log);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _log.Error(Category, $"Cannot open the vault in {folder}", ex);
            MessageDialog.Inform(owner, $"The vault in {folder} cannot be opened.\n\n{ex.Message}");
            return null;
        }
    }
}
