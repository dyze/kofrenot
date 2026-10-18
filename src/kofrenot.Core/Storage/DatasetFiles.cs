using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The files of dataset versions, one each (<c>content/</c>), written once and never changed:
/// sealed in chunks with the master key ("KFD1", <see cref="ChunkedBox"/>), bound to the version
/// id (the file's name). Read and written as streams, so any size fits in little memory.
/// </summary>
public sealed class DatasetFiles(string vaultFolder, ChunkedBox box)
{
    private static ReadOnlySpan<byte> Magic => "KFD1"u8;

    /// <summary>
    /// Describes a file to import: its name (without folder), size and SHA-256, reading it once;
    /// <paramref name="progress"/> is told the bytes read so far.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static DatasetFile Describe(string path, IProgress<long>? progress = null)
    {
        using var file = File.OpenRead(path);
        using var hashing = new HashingStream(file, progress);
        hashing.CopyTo(Stream.Null);
        return new DatasetFile(Path.GetFileName(path), hashing.Count, hashing.Sha256());
    }

    /// <summary>The file of this version is in the vault (the sync tool may copy it after its change).</summary>
    public bool Exists(Guid version) => File.Exists(VaultFolder.DatasetFileOf(vaultFolder, version));

    /// <summary>
    /// Writes the file of <paramref name="version"/>, read from <paramref name="content"/> to its
    /// end. Nothing is written when the content is not the file described by the version (changed
    /// since it was described). <paramref name="progress"/> is told the bytes read so far.
    /// </summary>
    /// <exception cref="IOException">The file cannot be written, or the content is not the one described.</exception>
    public void Write(DatasetEdited version, Stream content, IProgress<long>? progress = null)
    {
        var path = VaultFolder.DatasetFileOf(vaultFolder, version.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, output =>
        {
            using var hashing = new HashingStream(content, progress);
            box.Seal(Magic, hashing, output, version.Id.ToByteArray());
            if (hashing.Count != version.File.Size || hashing.Sha256() != version.File.Sha256)
            {
                throw new IOException("The file changed while it was imported");
            }
        }, overwrite: false);
    }

    /// <summary>The content of a version, decrypted while it is read.</summary>
    /// <exception cref="IOException">Its file is not there yet (file or folder not found), or cannot be read.</exception>
    /// <exception cref="InvalidDataException">Damaged (also while reading), or another key or id.</exception>
    public Stream OpenRead(Guid version) =>
        box.Open(Magic, File.OpenRead(VaultFolder.DatasetFileOf(vaultFolder, version)), version.ToByteArray());

    /// <summary>
    /// Writes the content of a version to <paramref name="target"/>, replacing it. A failure leaves
    /// <paramref name="target"/> as it was.
    /// </summary>
    /// <exception cref="IOException">The target cannot be written, or the version's file is not there.</exception>
    /// <exception cref="InvalidDataException">The version's file is damaged.</exception>
    public void Export(Guid version, string target)
    {
        using var content = OpenRead(version);
        AtomicFile.Write(target, content.CopyTo, overwrite: true);
    }
}
