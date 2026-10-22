using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The epochs of a vault's keys, and its handover to another identity: a server vault moves to
/// its account's identity, whatever opened it before (a format 5 header, the vault's own identity).
/// </summary>
public static partial class VaultKeys
{
    /// <summary>
    /// Hands the vault to <paramref name="identity"/> (a server account's): every key of
    /// <paramref name="access"/> wrapped for it, then a new epoch for it only. What opened the vault
    /// before (its header's key, its own identity) then opens the files written so far, not the next.
    /// Returns the access through <paramref name="unlocked"/>, with the new epoch.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="unlocked"/> is another identity.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="access"/> does not hold the latest epoch.</exception>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public static VaultAccess Attach(string folder, VaultAccess access, Identity identity, UnlockedIdentity unlocked)
    {
        if (unlocked.Id != identity.Id)
        {
            throw new ArgumentException("Another identity", nameof(unlocked));
        }

        if (!IsCurrent(folder, access, OwnKey(access)))
        {
            throw new InvalidOperationException("The vault has keys newer than the ones it was opened with");
        }

        foreach (var epoch in access.Keys.Epochs)
        {
            WrapFor(folder, access.Header.Id, epoch, access.Keys.KeyOf(epoch), identity, unlocked);
        }

        var next = access.Keys.CurrentEpoch + 1;
        var key = DerivedKey(unlocked, access.Header.Id, next);
        WrapFor(folder, access.Header.Id, next, key, identity, unlocked);
        return new VaultAccess(access.Header, access.Keys.With(next, key), unlocked);
    }

    /// <summary>
    /// Shares the vault with a member's identity: every key of <paramref name="access"/> wrapped for
    /// it, so they read all its files. Keys already wrapped for it are left as they are.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="access"/> does not hold the latest epoch, or has no identity to sign them.</exception>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public static void Share(string folder, VaultAccess access, Guid identity, byte[] publicKey)
    {
        if (!IsCurrent(folder, access, OwnKey(access)) || access.Identity is not { } owner)
        {
            throw new InvalidOperationException("The vault has keys newer than the ones it was opened with, or no identity to sign them");
        }

        foreach (var epoch in access.Keys.Epochs)
        {
            WrapFor(folder, access.Header.Id, epoch, access.Keys.KeyOf(epoch), identity, publicKey, owner);
        }
    }

    /// <summary>
    /// Rotates the vault's key (its owner, after removing a member): a new epoch, wrapped only for
    /// <paramref name="identities"/> (the members left, its owner among them). What is written next
    /// is sealed with it: a member removed reads the files written so far, not the next. Returns the
    /// access with the new epoch.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="access"/> does not hold the latest epoch, or has no identity.</exception>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public static VaultAccess Rotate(string folder, VaultAccess access, IReadOnlyCollection<(Guid Identity, byte[] PublicKey)> identities)
    {
        if (!IsCurrent(folder, access, OwnKey(access)) || access.Identity is not { } owner)
        {
            throw new InvalidOperationException("The vault has keys newer than the ones it was opened with, or no identity to rotate them");
        }

        var next = access.Keys.CurrentEpoch + 1;
        var key = DerivedKey(owner, access.Header.Id, next);
        foreach (var (identity, publicKey) in identities)
        {
            WrapFor(folder, access.Header.Id, next, key, identity, publicKey, owner);
        }

        return access with { Keys = access.Keys.With(next, key) };
    }

    /// <summary>Gives a new vault its first key, wrapped for <paramref name="identity"/> (a server account's): no identity of its own.</summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    internal static VaultAccess Create(string folder, VaultHeader header, Identity identity, UnlockedIdentity unlocked)
    {
        var key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
        WrapFor(folder, header.Id, 0, key, identity, unlocked);
        return new VaultAccess(header, KeyRing.Of(key), unlocked);
    }

    // The access's own signing key: the keys it changes are the ones it signed (none for a format 5 access).
    private static byte[]? OwnKey(VaultAccess access) => access.Identity is { } identity ? Signing.PublicKeyOf(identity) : null;

    // Derived from the identity's private key, not random: two devices attaching or rotating the
    // vault at once write the same key, whichever of their files the server keeps; what opened it
    // before, or a member removed, cannot derive it.
    private static byte[] DerivedKey(UnlockedIdentity unlocked, Guid vault, int epoch) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, unlocked.PrivateKey, SecretBox.KeySize, vault.ToByteArray(), Encoding.ASCII.GetBytes($"kofrenot vault key, epoch {epoch}"));
}

/// <summary>
/// Who must have signed a vault's key epochs for them to be used: a shared server vault's owner,
/// so a writer cannot plant a key of its own. <see cref="Self"/>: the identity opening the vault is
/// its owner; else the owner's public signing key.
/// </summary>
public sealed record KeySigner(byte[]? Owner)
{
    public static readonly KeySigner Self = new((byte[]?)null);

    /// <summary>The signing key the wraps opened by <paramref name="opener"/> must be signed with.</summary>
    public byte[] KeyFor(UnlockedIdentity opener) => Owner ?? Signing.PublicKeyOf(opener);
}
