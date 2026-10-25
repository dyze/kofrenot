using System.Windows;
using System.Windows.Threading;
using Kofrenot.Core;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

/// <summary>
/// The vault kofrenot works on: the one selected among the registered vaults, opened with its
/// remembered key or its pass phrase (created when the folder is empty), watched for the changes
/// of other apps. One vault is open at a time.
/// </summary>
public sealed partial class VaultSession : IDisposable
{
    private const string Category = "Vault";

    private readonly SettingsStore _settings;
    private readonly AppFiles _files;
    private readonly KeyMemory _keys;
    private readonly VaultNameMemory _names;
    private readonly ServerConnection _server;
    private readonly SyncService _sync;
    private readonly ILog _log;
    private readonly HashSet<Guid> _importing = [];
    private readonly WpfThumbnailMaker _thumbnails = new();
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private VaultWatcher? _watcher;
    private string? _keptName;

    // Set while a locked vault is unlocked: its pass phrase is asked, even when remembered.
    private bool _askPassPhrase;

    public VaultSession(SettingsStore settings, AppFiles files, KeyMemory keys, VaultNameMemory names, ServerConnection server, SyncService sync, ILog log)
    {
        _settings = settings;
        _files = files;
        _keys = keys;
        _names = names;
        _server = server;
        _sync = sync;
        _log = log;
        _upgrades = new VaultUpgrades(files.VaultUpgrades, log);
        _sync.Changed += AfterSync;
    }

    /// <summary>Raised on the UI thread when the vault is opened or closed, or has new changes.</summary>
    public event Action? Changed;

    /// <summary>The open vault, or null (none chosen, locked, or cannot be opened).</summary>
    public Vault? Current { get; private set; }

    /// <summary>A file is being imported into this dataset (<see cref="SaveDataset"/>).</summary>
    public bool IsImporting(Guid dataset) => _importing.Contains(dataset);

    /// <summary>How many files are being imported (<see cref="SaveDataset"/>).</summary>
    public int ImportsRunning => _importing.Count;

    /// <summary>
    /// The format of the vault when it was written by a newer kofrenot: it stays closed until
    /// kofrenot is upgraded. Null otherwise.
    /// </summary>
    public int? NewerFormat { get; private set; }

    /// <summary>
    /// Opens the vault of the settings, asking its pass phrase unless it is remembered, then its
    /// upgrade when it needs one. Does nothing when one is open, waits for its upgrade (<see cref="Upgrade"/>), or is locked (<see cref="Unlock(Window)"/>).
    /// </summary>
    public void Open(Window owner)
    {
        if (Current is not null || _notUpgraded is not null || IsLocked || Folder is not { } folder)
        {
            return;
        }

        NewerFormat = null;
        if (OpenFolder(owner, folder) is { } vault)
        {
            Use(owner, folder, vault);
        }

        Changed?.Invoke();
    }

    /// <summary>Makes an unlocked vault the current one, once its upgrade is agreed when it needs one, and watches its folder.</summary>
    private void Use(Window owner, string folder, Vault vault)
    {
        if (!AgreesToUpgrade(owner, vault))
        {
            return;
        }

        // A server vault still opening with its own pass phrase moves to its account's, when the
        // server can be asked; else at a later opening.
        if (_server.IsSignedIn && _server.TooOld is null)
        {
            AttachToAccount(owner, folder, vault);
        }

        Current = vault;
        KeepName();
        _ = SettleMembers(owner);
        try
        {
            _watcher = new VaultWatcher(folder, Refresh);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            _log.Warn(Category, $"The vault folder cannot be watched ({ex.Message}): changes of other apps are read when the window is shown");
        }
    }

    /// <summary>Reads the changes other apps have added. A change written by a newer kofrenot closes the vault.</summary>
    public void Refresh()
    {
        try
        {
            if (Current?.Refresh() > 0)
            {
                KeepName();
                Changed?.Invoke();
            }
        }
        catch (NewerVaultFormatException ex)
        {
            NeedsNewerKofrenot(ex);
            Close();
        }
    }

    private void NeedsNewerKofrenot(NewerVaultFormatException ex)
    {
        NewerFormat = ex.Format;
        _log.Warn(Category, ex.Message);
    }

    /// <summary>
    /// Shows the creation screen for a request over <paramref name="owner"/>, then adds what it
    /// says. The plan added; null when no vault is open, or the user cancels, or it cannot be written.
    /// </summary>
    /// <param name="choices">The homonyms already chosen (by the input box); the types default to generic.</param>
    public AddPlan? Create(Window owner, InputRequest request, AddChoices? choices = null) =>
        Current is { } vault ? Create(owner, vault, new CreationForm(vault.Graph, request, choices)) : null;

    /// <summary>As <see cref="Create(Window, InputRequest, AddChoices?)"/>, for one new node whose name is typed on the creation screen.</summary>
    /// <param name="type">The new node's type, fixed; chosen on the screen when null.</param>
    /// <param name="under">In a tree: the node it is added under.</param>
    /// <param name="file">A file dropped for a new dataset, chosen on the screen; its name is the node's to begin with.</param>
    public AddPlan? CreateNode(Window owner, NodeType? type = null, ParentLink? under = null, string? file = null) =>
        Current is { } vault ? Create(owner, vault, CreationForm.NewNode(vault.Graph, type, under, file is null ? null : Path.GetFileName(file)), file) : null;

    private AddPlan? Create(Window owner, Vault vault, CreationForm form, string? file = null)
    {
        if (!CanWrite(owner))
        {
            return null;
        }

        if (!CreationDialog.Ask(owner, form, file))
        {
            _log.Info("Action", "Creation cancelled");
            return null;
        }

        var notes = form.NotesToSave();
        var withNotes = notes.Count > 0 ? $", {notes.Count} with content" : string.Empty;
        var added = Append(owner, vault, [.. form.Plan.Changes, .. form.LinksToSave(), .. form.ViewsToSave()], form.Plan switch
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
        if (Current is not { } vault || !CanWrite(owner) || !_importing.Add(version.Node))
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

    /// <summary>
    /// Exports a dataset version's file to <paramref name="target"/>, downloaded first when it is not
    /// here, off the UI thread. False when it could not (the user is told).
    /// </summary>
    public async Task<bool> ExportDataset(Window owner, DatasetEdited version, string target)
    {
        if (Current is not { } vault || !await Fetch(owner, vault, [version], "export"))
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

    /// <summary>
    /// Downloads the content of versions that is not here yet (a server vault), for an action of the
    /// user: a download that failed before is tried again. False when one could not be (the user is told).
    /// </summary>
    private async Task<bool> Fetch(Window owner, Vault vault, IReadOnlyList<ContentEdited> versions, string action)
    {
        foreach (var version in versions)
        {
            if (!await vault.FetchAsync(version, retry: true))
            {
                var state = vault.StateOf(version.Id);
                _log.Warn("Action", $"Cannot {action}: content {version.Id} not here ({state.Level})");
                MessageDialog.Inform(owner, $"Cannot {action}.\n\n{state.Describe(version is DatasetEdited ? "the file" : "the note")}");
                return false;
            }
        }

        return true;
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

    /// <summary>
    /// Shows a view's name and what it shows, to edit them, then saves a new version and the
    /// rename. False when no vault is open, the user cancels, nothing changed, or it cannot be
    /// written (the user is told).
    /// </summary>
    public bool EditView(Window owner, Node view)
    {
        if (Current is not { } vault)
        {
            return false;
        }

        var planner = new ViewPlanner(vault.Graph);
        var loaded = planner.Load(view);
        if (ViewDialog.Ask(owner, vault.Graph, view, loaded) is not { } edit)
        {
            _log.Info("Action", "View edit cancelled");
            return false;
        }

        var version = planner.Save(view, edit.Definition, loaded);
        Change?[] planned = [edit.Rename, version];
        var changes = planned.OfType<Change>().ToList();
        var action = version is null
            ? "View renamed"
            : (loaded.IsConflict ? $"View edited (a conflict of {loaded.Versions.Count} versions resolved)" : "View edited")
                + (edit.Rename is null ? string.Empty : " and renamed");
        return changes.Count > 0 && Current == vault && Append(owner, vault, changes, action);
    }

    /// <summary>
    /// Gives a node another type. A view asks first what it shows, and its first version is written
    /// with the type. False when no vault is open, the user cancels, or it cannot be written (the user is told).
    /// </summary>
    public bool Retype(Window owner, NodeRetyped retype)
    {
        if (Current is not { } vault || vault.Graph.FindNode(retype.Node) is not { } node)
        {
            return false;
        }

        IReadOnlyList<Change> changes = [retype];
        if (retype.Type == TypeCatalog.View.Id)
        {
            var view = node with { Type = retype.Type };
            var planner = new ViewPlanner(vault.Graph);
            var loaded = planner.Load(view);
            if (ViewDialog.Ask(owner, vault.Graph, view, loaded) is not { } edit)
            {
                _log.Info("Action", "Node type change cancelled");
                return false;
            }

            Change?[] planned = [retype, planner.Save(view, edit.Definition, loaded), edit.Rename];
            changes = planned.OfType<Change>().ToList();
        }

        var resolved = retype.Replaces.Count > 1 ? $", a conflict of {retype.Replaces.Count} types resolved" : string.Empty;
        return Current == vault && Append(owner, vault, changes, $"Node type changed ({node.Type} → {retype.Type}{resolved})");
    }

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
        if (Current is not { } vault || !CanWrite(owner) || !await Fetch(owner, vault, plan.Contents.Select(copy => copy.From).ToList(), "copy"))
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
        if (!CanWrite(owner))
        {
            return false;
        }

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
            KeepName();
            Changed?.Invoke();
        }
    }

    /// <summary>A server vault's notes and datasets are downloaded when needed, with the account's client while signed in to its server.</summary>
    private ContentFetcher? FetcherOf(string folder) =>
        Selected?.Location.Store is { } store
            ? new ContentFetcher(folder, store.Id, () => _server.Client is { Session: not null } client && _server.Account?.Server == store.Server ? client : null, _log)
            : null;

    /// <summary>Once its downloads are over, what shows the vault reads its content again.</summary>
    private void AfterFetch(Vault vault) => _dispatcher.BeginInvoke(() =>
    {
        if (Current == vault && !vault.IsFetching)
        {
            Changed?.Invoke();
        }
    });

    public void Close()
    {
        _watcher?.Dispose();
        _watcher = null;
        _keptName = null;
        _access = null;
        if (Current is not null || _notUpgraded is not null)
        {
            Current = null;
            _notUpgraded = null;
            _log.Info(Category, "Vault closed");
            Changed?.Invoke();
        }
    }

    public void Dispose() => _watcher?.Dispose();

    private Vault? OpenFolder(Window owner, string folder)
    {
        switch (VaultFolder.Inspect(folder))
        {
            // A vault opened before, out of reach (a file share offline): never a new vault there.
            case FolderKind.Empty when Selected?.Id is not null:
                _log.Warn(Category, $"Vault {Selected.Id} cannot be reached in {folder}");
                MessageDialog.Inform(owner, $"The vault cannot be reached in {folder}.\n\nCheck that the folder, or the file share it is on, is available.");
                return null;
            // A server vault not downloaded yet: never a new vault in its mirror (CreateVault makes those).
            case FolderKind.Empty when Selected?.Location is { IsOnServer: true } location:
                _log.Warn(Category, $"Server vault {location.Store!.Id} not downloaded yet");
                MessageDialog.Inform(owner, $"This vault is not downloaded yet ({_sync.StatusOf(location.Store).Describe()}).\n\nkofrenot downloads it once the server can be reached and you are signed in (Settings > Server).");
                return null;
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
        (VaultAccess Access, RecoveryKey Recovery)? created = null;
        var answer = PassphraseDialog.AskNew(owner, folder, new VaultLocation(folder).FolderName, passPhrase =>
        {
            created = VaultFolder.Create(folder, passPhrase);
            return true;
        });
        if (answer is null || created is not { } vault)
        {
            _log.Info("Action", "Vault creation cancelled");
            return null;
        }

        _log.Info("Action", $"Vault {vault.Access.Header.Id} created in {folder}");
        RecoveryKeyDialog.Show(owner, folder, vault.Recovery);
        var opened = Open(owner, folder, vault.Access, answer.Remember);
        if (opened is not null)
        {
            NameNew(owner, opened, answer.Name);
        }

        return opened;
    }

    /// <summary>Writes a new vault's first change: the name chosen on its creation, so every device shows it.</summary>
    private void NameNew(Window owner, Vault vault, string name)
    {
        if (new VaultNamePlanner(vault.Graph).Plan(name).Change is { } change)
        {
            Append(owner, vault, [change], "Vault named");
        }
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
            NeedsNewerKofrenot(ex);
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _log.Error(Category, $"Cannot read the vault in {folder}", ex);
            MessageDialog.Inform(owner, $"The vault in {folder} cannot be opened.\n\n{ex.Message}");
            return null;
        }

        return UnlockKeys(owner, folder, header) is { } unlocked ? Open(owner, folder, unlocked.Access, unlocked.Remember) : null;
    }

    private Vault? Open(Window owner, string folder, VaultAccess access, bool remember)
    {
        try
        {
            Identify(access.Header);
            if (remember)
            {
                _keys.Remember(access);
            }

            var vault = Vault.Open(folder, access.Header, access.Keys, _files.Cache, _log, FetcherOf(folder));
            vault.ContentFetched += () => AfterFetch(vault);
            _access = access;
            return vault;
        }
        catch (NewerVaultFormatException ex)
        {
            NeedsNewerKofrenot(ex);
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
