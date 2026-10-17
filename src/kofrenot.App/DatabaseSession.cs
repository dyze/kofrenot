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
/// The database kofrenot works on: the folder of the settings, opened with its remembered key or
/// its pass phrase (created when the folder is empty), watched for the changes of other apps.
/// </summary>
public sealed class DatabaseSession : IDisposable
{
    private const string Category = "Database";

    private readonly SettingsStore _settings;
    private readonly AppFiles _files;
    private readonly KeyMemory _keys;
    private readonly ILog _log;
    private DatabaseWatcher? _watcher;

    public DatabaseSession(SettingsStore settings, AppFiles files, KeyMemory keys, ILog log)
    {
        _settings = settings;
        _files = files;
        _keys = keys;
        _log = log;
    }

    /// <summary>Raised on the UI thread when the database is opened or closed, or has new changes.</summary>
    public event Action? Changed;

    /// <summary>The open database, or null (none chosen, locked, or cannot be opened).</summary>
    public Database? Current { get; private set; }

    public string? Folder => _settings.DatabaseFolder;

    /// <summary>Whether the open database's key is remembered on this computer.</summary>
    public bool IsRemembered => Current is { } database && _keys.Has(database.Id);

    /// <summary>Opens the database of the settings, asking its pass phrase unless it is remembered. Does nothing when one is open.</summary>
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
            _watcher = new DatabaseWatcher(folder, Refresh);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            _log.Warn(Category, $"The database folder cannot be watched ({ex.Message}): changes of other apps are read when the window is shown");
        }

        Changed?.Invoke();
    }

    /// <summary>Uses another folder: the current database is closed, then the new one opened.</summary>
    public void SwitchTo(Window owner, string folder)
    {
        Close();
        _settings.SetDatabaseFolder(folder);
        _log.Info("Settings", $"Database folder: {folder}");
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
    /// says. False when no database is open, or the user cancels, or it cannot be written.
    /// </summary>
    /// <param name="choices">The homonyms already chosen (by the input box); the types default to generic.</param>
    public bool Create(Window owner, InputRequest request, AddChoices? choices = null)
    {
        if (Current is not { } database)
        {
            return false;
        }

        if (CreationDialog.Ask(owner, new CreationForm(database.Graph, request, choices)) is not { } plan)
        {
            _log.Info("Action", "Creation cancelled");
            return false;
        }

        try
        {
            Add(database, plan);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Action", "Cannot add to the database", ex);
            MessageDialog.Inform(owner, $"Cannot add to the database.\n\n{ex.Message}");
            return false;
        }
    }

    private void Add(Database database, AddPlan plan)
    {
        try
        {
            foreach (var change in plan.Changes)
            {
                database.Append(change);
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

    /// <summary>Forgets the open database's remembered key: its pass phrase is asked at the next start.</summary>
    public void Forget()
    {
        if (Current is { } database)
        {
            _keys.Forget(database.Id);
        }
    }

    public void Close()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (Current is not null)
        {
            Current = null;
            _log.Info(Category, "Database closed");
            Changed?.Invoke();
        }
    }

    public void Dispose() => _watcher?.Dispose();

    private Database? OpenFolder(Window owner, string folder)
    {
        switch (DatabaseFolder.Inspect(folder))
        {
            case FolderKind.Empty:
                return Create(owner, folder);
            case FolderKind.Database:
                return Unlock(owner, folder);
            default:
                _log.Warn(Category, $"Not a database folder: {folder}");
                MessageDialog.Inform(owner, $"{folder} holds other files.\n\nChoose an empty folder for a new database, or the folder of a kofrenot database.");
                return null;
        }
    }

    private Database? Create(Window owner, string folder)
    {
        (DatabaseHeader Header, byte[] Key)? created = null;
        var remember = PassphraseDialog.Ask(owner, create: true, folder, passPhrase =>
        {
            created = DatabaseFolder.Create(folder, passPhrase);
            return true;
        });
        if (remember is null || created is not { } database)
        {
            _log.Info("Action", "Database creation cancelled");
            return null;
        }

        _log.Info("Action", $"Database {database.Header.Id} created in {folder}");
        return Open(owner, folder, database.Header, database.Key, remember.Value);
    }

    private Database? Unlock(Window owner, string folder)
    {
        DatabaseHeader header;
        try
        {
            header = DatabaseHeader.Read(DatabaseFolder.HeaderOf(folder));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _log.Error(Category, $"Cannot read the database in {folder}", ex);
            MessageDialog.Inform(owner, $"The database in {folder} cannot be opened.\n\n{ex.Message}");
            return null;
        }

        if (_keys.Recall(header.Id) is { } remembered)
        {
            _log.Info(Category, $"Database {header.Id} unlocked with the remembered pass phrase");
            return Open(owner, folder, header, remembered, remember: false);
        }

        byte[]? key = null;
        var remember = PassphraseDialog.Ask(owner, create: false, folder, passPhrase =>
        {
            key = header.Unlock(passPhrase);
            if (key is null)
            {
                _log.Warn(Category, $"Wrong pass phrase for database {header.Id}");
            }

            return key is not null;
        });
        if (remember is null || key is null)
        {
            _log.Info("Action", "Unlock cancelled");
            return null;
        }

        _log.Info("Action", $"Database {header.Id} unlocked");
        return Open(owner, folder, header, key, remember.Value);
    }

    private Database? Open(Window owner, string folder, DatabaseHeader header, byte[] key, bool remember)
    {
        try
        {
            if (remember)
            {
                _keys.Remember(header.Id, key);
            }

            return Database.Open(folder, header, key, _files.Cache, _log);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _log.Error(Category, $"Cannot open the database in {folder}", ex);
            MessageDialog.Inform(owner, $"The database in {folder} cannot be opened.\n\n{ex.Message}");
            return null;
        }
    }
}
