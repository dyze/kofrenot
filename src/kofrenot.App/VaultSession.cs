using System.Windows;
using Kofrenot.Core;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
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
    private readonly HashSet<Guid> _importing = [];
    private readonly WpfThumbnailMaker _thumbnails = new();
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

    /// <summary>A file is being imported into this dataset (<see cref="SaveDataset"/>).</summary>
    public bool IsImporting(Guid dataset) => _importing.Contains(dataset);

    /// <summary>Whether the open vault's key is remembered on this computer.</summary>
    public bool IsRemembered => Current is { } vault && _keys.Has(vault.Id);

    /// <summary>
    /// The format of the vault when it was written by a newer kofrenot: it stays closed until
    /// kofrenot is upgraded. Null otherwise.
    /// </summary>
    public int? NewerFormat { get; private set; }

    /// <summary>Opens the vault of the settings, asking its pass phrase unless it is remembered. Does nothing when one is open.</summary>
    public void Open(Window owner)
    {
        if (Current is not null || Folder is not { } folder)
        {
            return;
        }

        NewerFormat = null;
        Current = OpenFolder(owner, folder);
        if (Current is null)
        {
            Changed?.Invoke();
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

    /// <summary>Reads the changes other apps have added. A change written by a newer kofrenot closes the vault.</summary>
    public void Refresh()
    {
        try
        {
            if (Current?.Refresh() > 0)
            {
                Changed?.Invoke();
            }
        }
        catch (NewerVaultFormatException ex)
        {
            NeedsUpgrade(ex);
            Close();
        }
    }

    private void NeedsUpgrade(NewerVaultFormatException ex)
    {
        NewerFormat = ex.Format;
        _log.Warn(Category, ex.Message);
    }

    /// <summary>
    /// Shows the creation screen for a request over <paramref name="owner"/>, then adds what it
    /// says. The plan added; null when no vault is open, or the user cancels, or it cannot be written.
    /// </summary>
    /// <param name="choices">The homonyms already chosen (by the input box); the types default to generic.</param>
    public AddPlan? Create(Window owner, InputRequest request, AddChoices? choices = null)
    {
        if (Current is not { } vault)
        {
            return null;
        }

        var form = new CreationForm(vault.Graph, request, choices);
        if (!CreationDialog.Ask(owner, form))
        {
            _log.Info("Action", "Creation cancelled");
            return null;
        }

        var notes = form.NotesToSave();
        var withNotes = notes.Count > 0 ? $", {notes.Count} with content" : string.Empty;
        var added = Append(owner, vault, [.. form.Plan.Changes, .. form.ViewsToSave()], form.Plan switch
        {
            LinkPlan link => $"Link added ({link.Type.Id}, {(link.Left.IsNew ? 1 : 0) + (link.Right?.IsNew == true ? 1 : 0)} new nodes{withNotes})",
            NodePlan node => $"Node added ({node.Type.Id}{(node.NeedsConfirmation ? ", a duplicate" : string.Empty)}{withNotes})",
            _ => "Added",
        }, notes);
        if (!added)
        {
            return null;
        }

        // The nodes are there: their files follow, each in the background (the user is told if one fails).
        foreach (var dataset in form.DatasetsToSave())
        {
            _ = SaveDataset(owner, dataset.Version, () => File.OpenRead(dataset.Path));
        }

        return form.Plan;
    }

    /// <summary>
    /// Saves a dataset version: its files (an image's thumbnail, then the file, read from
    /// <paramref name="open"/>) written off the UI thread, then its change. False when no vault is
    /// open, it closed meanwhile, or a file cannot be read or written (the user is told).
    /// <paramref name="progress"/> is told the bytes sealed so far.
    /// </summary>
    public async Task<bool> SaveDataset(Window owner, DatasetEdited version, Func<Stream> open, IProgress<long>? progress = null)
    {
        if (Current is not { } vault || !_importing.Add(version.Node))
        {
            return false;
        }

        Changed?.Invoke();
        try
        {
            await Task.Run(() => vault.WriteDataset(version, open, _thumbnails, progress));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // The message names the file: shown, not logged.
            _log.Warn("Action", $"Cannot import the dataset's file ({ex.GetType().Name})");
            MessageDialog.Inform(owner, $"Cannot import the file.\n\n{ex.Message}");
            return false;
        }
        finally
        {
            _importing.Remove(version.Node);
            Changed?.Invoke();
        }

        var replaced = version.Replaces.Count > 1 ? $", a conflict of {version.Replaces.Count} versions resolved" : string.Empty;
        return Current == vault && Append(owner, vault, [version], $"Dataset imported ({version.File.Size} bytes{replaced})");
    }

    /// <summary>Exports a dataset version's file to <paramref name="target"/>, off the UI thread. False when it could not (the user is told).</summary>
    public async Task<bool> ExportDataset(Window owner, DatasetEdited version, string target)
    {
        if (Current is not { } vault)
        {
            return false;
        }

        try
        {
            await Task.Run(() => vault.Datasets.Export(version.Id, target));
            _log.Info("Action", $"Dataset exported ({version.File.Size} bytes)");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Warn("Action", $"Cannot export the dataset's file ({ex.GetType().Name})");
            MessageDialog.Inform(owner, $"Cannot export the file.\n\n{ex.Message}");
            return false;
        }
    }

    /// <summary>Renames a node. False when no vault is open, or it cannot be written (the user is told).</summary>
    public bool Rename(Window owner, NodeRenamed rename) =>
        Current is { } vault
        && Append(owner, vault, [rename], rename.Replaces.Count > 1 ? $"Node renamed (a conflict of {rename.Replaces.Count} names resolved)" : "Node renamed");

    /// <summary>
    /// Writes a change planned elsewhere (an alias, a pin), logging <paramref name="action"/>. False
    /// when no vault is open, or it cannot be written (the user is told).
    /// </summary>
    public bool Write(Window owner, Change change, string action) => Write(owner, [change], action);

    /// <summary>Writes changes planned elsewhere (a move, a deletion), in their order, as <see cref="Write(Window, Change, string)"/>.</summary>
    public bool Write(Window owner, IReadOnlyList<Change> changes, string action) =>
        Current is { } vault && Append(owner, vault, changes, action);

    /// <summary>
    /// Writes a copy: the content of its notes and datasets written again off the UI thread (a
    /// dataset may be large), then its changes. False when no vault is open, it closed meanwhile,
    /// or a file cannot be read or written (the user is told).
    /// </summary>
    public async Task<bool> Copy(Window owner, CopyPlan plan)
    {
        if (Current is not { } vault)
        {
            return false;
        }

        try
        {
            await Task.Run(() => vault.WriteCopies(plan.Contents, _thumbnails));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Warn("Action", $"Cannot copy the nodes' content ({ex.GetType().Name})");
            MessageDialog.Inform(owner, $"Cannot copy.\n\n{ex.Message}");
            return false;
        }

        return Current == vault && Append(owner, vault, plan.Changes, $"Nodes copied ({plan.Nodes.Count}, {plan.Contents.Count} with content)");
    }

    /// <summary>Saves a note version with its content. False when no vault is open, or it cannot be written (the user is told).</summary>
    public bool SaveNote(Window owner, NoteEdited version, string content) =>
        Current is { } vault
        && Append(owner, vault, [], version.Replaces.Count > 1 ? $"Note saved (a conflict of {version.Replaces.Count} versions resolved)" : "Note saved", [new NoteToSave(version, content)]);

    /// <summary>Writes the changes, then the note versions (each content file, then its change).</summary>
    private bool Append(Window owner, Vault vault, IReadOnlyList<Change> changes, string action, IReadOnlyList<NoteToSave>? notes = null)
    {
        try
        {
            foreach (var change in changes)
            {
                vault.Append(change);
            }

            foreach (var note in notes ?? [])
            {
                vault.Save(note.Version, note.Content);
            }

            _log.Info("Action", action);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Action", "Cannot write to the vault", ex);
            MessageDialog.Inform(owner, $"Cannot write to the vault.\n\n{ex.Message}");
            return false;
        }
        finally
        {
            Changed?.Invoke();
        }
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
        catch (NewerVaultFormatException ex)
        {
            NeedsUpgrade(ex);
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
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
        catch (NewerVaultFormatException ex)
        {
            NeedsUpgrade(ex);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _log.Error(Category, $"Cannot open the vault in {folder}", ex);
            MessageDialog.Inform(owner, $"The vault in {folder} cannot be opened.\n\n{ex.Message}");
            return null;
        }
    }
}
