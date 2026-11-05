using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The vault's format, and its upgrade: every write is in <see cref="VaultFormat.Current"/>, so
/// writing to a vault of an older format raises it, and the kofrenot of that format no longer
/// open it. Nothing is written until the user agrees (<see cref="AgreeToUpgrade"/>). A vault a
/// newer kofrenot wrote to, which this one may read but not write, is read only (<see cref="IsReadOnly"/>).
/// </summary>
public sealed partial class Vault
{
    private bool _upgradeAgreed;

    /// <summary>The highest format of the vault's files read or written: its header, change files and snapshot.</summary>
    public int Format { get; private set; }

    /// <summary>The newest of the oldest formats that may write to the vault, as its header and change files say (<see cref="VaultFormat"/>).</summary>
    public int OldestWriter { get; private set; }

    /// <summary>A newer kofrenot wrote to the vault: this one reads it, skipping what it does not know, and writes nothing.</summary>
    public bool IsReadOnly => OldestWriter > VaultFormat.Current;

    /// <summary>The vault is of an older format: writing to it raises it to <see cref="VaultFormat.Current"/>.</summary>
    public bool NeedsUpgrade => Format < VaultFormat.Current;

    /// <summary>The user agreed to raise the vault's format: writes are allowed.</summary>
    public void AgreeToUpgrade()
    {
        if (NeedsUpgrade && !_upgradeAgreed)
        {
            _upgradeAgreed = true;
            _log.Info(Category, $"Vault {Id} may be upgraded from format {Format} to {VaultFormat.Current}");
        }
    }

    /// <exception cref="InvalidOperationException">The vault is read only, or the upgrade a write makes was not agreed.</exception>
    private void EnsureCanWrite()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"The vault is read only: a newer kofrenot wrote to it (oldest writer: format {OldestWriter}), this one writes format {VaultFormat.Current}");
        }

        if (NeedsUpgrade && !_upgradeAgreed)
        {
            throw new InvalidOperationException($"The vault is of format {Format}: writing to it raises it to {VaultFormat.Current}, which the user has not agreed to");
        }
    }

    private void Written(int format, int oldestWriter)
    {
        var wasReadOnly = IsReadOnly;
        Format = Math.Max(Format, format);
        OldestWriter = Math.Max(OldestWriter, oldestWriter);
        if (IsReadOnly && !wasReadOnly)
        {
            _log.Info(Category, $"Vault {Id} is read only: a newer kofrenot wrote to it (format {Format}, oldest writer: format {OldestWriter})");
        }
    }
}
