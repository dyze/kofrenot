using System.Text;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The content of note versions, one file each (<c>content/</c>), written once and never changed.
/// UTF-8 text sealed with the master key ("KFN1"), bound to the version id (the file's name).
/// </summary>
public sealed class NoteFiles(string vaultFolder, SecretBox box, ILog? log = null)
{
    private static ReadOnlySpan<byte> Magic => "KFN1"u8;

    private readonly ILog _log = log ?? NullLog.Instance;

    /// <summary>Whether a version's file is in the vault (it may arrive after its change).</summary>
    public bool Exists(Guid version) => File.Exists(VaultFolder.ContentFileOf(vaultFolder, version));

    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Write(Guid version, string content)
    {
        var path = VaultFolder.ContentFileOf(vaultFolder, version);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, box.Seal(Magic, Encoding.UTF8.GetBytes(content), version.ToByteArray()), overwrite: false);
    }

    /// <summary>
    /// The content of a version, or null when its file is not there yet (the sync tool may copy it
    /// after its change) or cannot be read (logged).
    /// </summary>
    public string? Read(Guid version)
    {
        var path = VaultFolder.ContentFileOf(vaultFolder, version);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(box.Open(Magic, File.ReadAllBytes(path), version.ToByteArray()));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"Note content {version} cannot be read: {ex.Message}");
            return null;
        }
    }
}
