namespace Kofrenot.Core.Storage;

/// <summary>What a folder chosen as database holds.</summary>
public enum FolderKind
{
    /// <summary>Missing or empty: a new database can be created in it.</summary>
    Empty,

    /// <summary>Holds a database header.</summary>
    Database,

    /// <summary>Holds other files: not used, so nothing of the user's is mixed with the database.</summary>
    Other,
}

/// <summary>
/// The layout of a database folder, and the creation of a new one. The only place the names of
/// its files are written.
/// </summary>
public static class DatabaseFolder
{
    public const string HeaderFile = "kofrenot-db.json";
    public const string ChangesFolder = "changes";
    public const string ChangeExtension = ".kfc";

    public static FolderKind Inspect(string folder)
    {
        if (File.Exists(HeaderOf(folder)))
        {
            return FolderKind.Database;
        }

        return Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() ? FolderKind.Other : FolderKind.Empty;
    }

    /// <summary>Creates a database in an empty folder: its header, with a new master key.</summary>
    /// <exception cref="IOException">The folder is not empty.</exception>
    public static (DatabaseHeader Header, byte[] MasterKey) Create(string folder, string passPhrase, int iterations = KeyVault.DefaultIterations)
    {
        if (Inspect(folder) != FolderKind.Empty)
        {
            throw new IOException("A database can only be created in an empty folder");
        }

        var id = Guid.NewGuid();
        var (wrapped, masterKey) = KeyVault.Create(passPhrase, id.ToByteArray(), iterations);
        var header = new DatabaseHeader(DatabaseHeader.CurrentFormat, id, wrapped);
        Directory.CreateDirectory(ChangesOf(folder));
        header.Write(HeaderOf(folder));
        return (header, masterKey);
    }

    public static string HeaderOf(string folder) => Path.Combine(folder, HeaderFile);

    internal static string ChangesOf(string folder) => Path.Combine(folder, ChangesFolder);

    /// <summary><c>changes/3f/3f9c…e1.kfc</c>: sub-folders by the first two characters, so none holds too many files.</summary>
    internal static string ChangeFileOf(string folder, Guid change)
    {
        var name = change.ToString("N");
        return Path.Combine(ChangesOf(folder), name[..2], name + ChangeExtension);
    }
}
