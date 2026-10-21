using System.Security.Cryptography;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Core.Storage;

/// <summary>What a folder chosen as vault holds.</summary>
public enum FolderKind
{
    /// <summary>Missing or empty: a new vault can be created in it.</summary>
    Empty,

    /// <summary>Holds a vault header.</summary>
    Vault,

    /// <summary>Holds other files: not used, so nothing of the user's is mixed with the vault.</summary>
    Other,
}

/// <summary>
/// The layout of a vault folder, and the creation of a new one. The only place the names of
/// its files are written.
/// </summary>
public static class VaultFolder
{
    public const string HeaderFile = "kofrenot-vault.json";
    public const string KeysFolder = "keys";
    public const string ChangesFolder = "changes";
    public const string ChangeExtension = ".kfc";
    public const string ContentFolder = "content";
    public const string ContentExtension = ".kfn";
    public const string DatasetExtension = ".kfd";
    public const string ThumbnailExtension = ".kft";

    public static FolderKind Inspect(string folder)
    {
        if (File.Exists(HeaderOf(folder)))
        {
            return FolderKind.Vault;
        }

        return Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() ? FolderKind.Other : FolderKind.Empty;
    }

    /// <summary>
    /// Creates a vault in an empty folder: its keys (an identity sealed by the pass phrase, a new
    /// key of epoch 0), then its header. Returns the recovery key of the identity, to show once.
    /// </summary>
    /// <exception cref="IOException">The folder is not empty.</exception>
    public static (VaultAccess Access, RecoveryKey Recovery) Create(string folder, string passPhrase, int iterations = KeyWrap.DefaultIterations) =>
        Create(folder, header => VaultKeys.Create(folder, header, RandomNumberGenerator.GetBytes(SecretBox.KeySize), passPhrase, iterations));

    /// <summary>
    /// Creates a vault in an empty folder for an identity kept outside it (a server account's): its
    /// first key wrapped for that identity, then its header.
    /// </summary>
    /// <exception cref="IOException">The folder is not empty.</exception>
    public static VaultAccess Create(string folder, Identity identity, UnlockedIdentity unlocked) =>
        Create(folder, header => VaultKeys.Create(folder, header, identity, unlocked));

    private static T Create<T>(string folder, Func<VaultHeader, T> createKeys)
    {
        if (Inspect(folder) != FolderKind.Empty)
        {
            throw new IOException("A vault can only be created in an empty folder");
        }

        var header = new VaultHeader(VaultFormat.Current, Guid.NewGuid());
        var created = createKeys(header);
        Directory.CreateDirectory(ChangesOf(folder));

        // The header last: a folder holding it is a vault, whose keys are there.
        header.Write(HeaderOf(folder));
        return created;
    }

    /// <summary>
    /// The files of a folder and their total size, sub-folders included (a vault: header, changes,
    /// contents, thumbnails); none for a missing folder. Reads the folder listing only, but walks
    /// every sub-folder: call it off the UI thread.
    /// </summary>
    public static FolderSize SizeOf(string folder)
    {
        var directory = new DirectoryInfo(folder);
        if (!directory.Exists)
        {
            return new FolderSize(0, 0);
        }

        var files = directory.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
        return new FolderSize(files.Count, files.Sum(file => file.Length));
    }

    public static string HeaderOf(string folder) => Path.Combine(folder, HeaderFile);

    internal static string KeysOf(string folder) => Path.Combine(folder, KeysFolder);

    internal static string ChangesOf(string folder) => Path.Combine(folder, ChangesFolder);

    /// <summary><c>changes/3f/3f9c…e1.kfc</c>: sub-folders by the first two characters, so none holds too many files.</summary>
    internal static string ChangeFileOf(string folder, Guid change) => SpreadFileOf(ChangesOf(folder), change, ChangeExtension);

    /// <summary><c>content/3f/3f9c…e1.kfn</c>: the content of a note version, named by its change.</summary>
    internal static string ContentFileOf(string folder, Guid version) => SpreadFileOf(Path.Combine(folder, ContentFolder), version, ContentExtension);

    /// <summary><c>content/b2/b2d4…07.kfd</c>: the file of a dataset version, named by its change.</summary>
    internal static string DatasetFileOf(string folder, Guid version) => SpreadFileOf(Path.Combine(folder, ContentFolder), version, DatasetExtension);

    /// <summary><c>content/b2/b2d4…07.kft</c>: the thumbnail of a dataset version, named by its change.</summary>
    internal static string ThumbnailFileOf(string folder, Guid version) => SpreadFileOf(Path.Combine(folder, ContentFolder), version, ThumbnailExtension);

    private static string SpreadFileOf(string parent, Guid id, string extension)
    {
        var name = id.ToString("N");
        return Path.Combine(parent, name[..2], name + extension);
    }
}

/// <summary>How many files a folder holds, and their size in bytes.</summary>
public sealed record FolderSize(int Files, long Bytes)
{
    /// <summary>"4.2 MB in 87 files", in the current culture unless another is given.</summary>
    public string Describe(IFormatProvider? culture = null) => $"{ByteSize.Format(Bytes, culture)} in {Files} {(Files == 1 ? "file" : "files")}";
}
