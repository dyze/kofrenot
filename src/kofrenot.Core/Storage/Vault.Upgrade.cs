using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The vault's format, and its upgrade: every write is in <see cref="VaultFormat.Current"/>, so
/// writing to a vault of an older format raises it, and the kofrenot of that format no longer
/// open it. Nothing is written until the user agrees (<see cref="AgreeToUpgrade"/>).
/// </summary>
public sealed partial class Vault
{
    private bool _upgradeAgreed;

    /// <summary>The highest format of the vault's files read or written: its header, change files and snapshot.</summary>
    public int Format { get; private set; }

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

    /// <exception cref="InvalidOperationException">The upgrade a write makes was not agreed.</exception>
    private void EnsureCanWrite()
    {
        if (NeedsUpgrade && !_upgradeAgreed)
        {
            throw new InvalidOperationException($"The vault is of format {Format}: writing to it raises it to {VaultFormat.Current}, which the user has not agreed to");
        }
    }

    private void Written(int format) => Format = Math.Max(Format, format);
}
