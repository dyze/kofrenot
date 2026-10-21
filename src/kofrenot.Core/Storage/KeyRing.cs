namespace Kofrenot.Core.Storage;

/// <summary>
/// The keys of a vault, by epoch: new files are sealed with the latest one, a file is opened with
/// the one of its epoch. Before format 6 a vault had one master key: epoch 0.
/// </summary>
public sealed class KeyRing
{
    private readonly Dictionary<int, byte[]> _keys;

    /// <exception cref="ArgumentException">No key, a negative epoch, or a key of the wrong size.</exception>
    public KeyRing(IReadOnlyDictionary<int, byte[]> keys)
    {
        if (keys.Count == 0)
        {
            throw new ArgumentException("A key ring needs a key", nameof(keys));
        }

        if (keys.Any(key => key.Key < 0 || key.Value.Length != SecretBox.KeySize))
        {
            throw new ArgumentException($"Epochs are 0 or more, keys {SecretBox.KeySize} bytes", nameof(keys));
        }

        _keys = keys.ToDictionary();
        CurrentEpoch = _keys.Keys.Max();
    }

    /// <summary>A ring of one key, of epoch 0: a new vault's, or a format 5 vault's master key.</summary>
    public static KeyRing Of(byte[] key) => new(new Dictionary<int, byte[]> { [0] = key });

    /// <summary>The latest epoch: new files are sealed with its key.</summary>
    public int CurrentEpoch { get; }

    public byte[] CurrentKey => _keys[CurrentEpoch];

    /// <exception cref="InvalidDataException">No key of this epoch: the file cannot be opened.</exception>
    public byte[] KeyOf(int epoch) =>
        _keys.TryGetValue(epoch, out var key) ? key : throw new InvalidDataException($"No key of epoch {epoch}");
}
