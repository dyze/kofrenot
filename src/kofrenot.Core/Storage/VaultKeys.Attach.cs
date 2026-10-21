using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The epochs of a vault's keys, and its handover to another identity: a server vault moves to
/// its account's identity, whatever opened it before (a format 5 header, the vault's own identity).
/// </summary>
public static partial class VaultKeys
{
    /// <summary>The vault's latest key epoch; 0 for a vault without <c>keys/</c> (format 5: its master key).</summary>
    public static int LatestEpoch(string folder) => EpochsOf(folder).DefaultIfEmpty(0).Max();

    /// <summary>Whether <paramref name="access"/> opens the files written last: it holds the latest epoch's key.</summary>
    public static bool IsCurrent(string folder, VaultAccess access) => access.Keys.CurrentEpoch >= LatestEpoch(folder);

    /// <summary>Whether the latest epoch's key is wrapped for <paramref name="identity"/>.</summary>
    public static bool IsAttached(string folder, Guid identity) => File.Exists(WrapOf(folder, LatestEpoch(folder), identity));

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

        if (!IsCurrent(folder, access))
        {
            throw new InvalidOperationException("The vault has keys newer than the ones it was opened with");
        }

        foreach (var epoch in access.Keys.Epochs)
        {
            WrapFor(folder, access.Header.Id, epoch, access.Keys.KeyOf(epoch), identity);
        }

        var next = access.Keys.CurrentEpoch + 1;
        var key = AttachKey(unlocked, access.Header.Id, next);
        WrapFor(folder, access.Header.Id, next, key, identity);
        return new VaultAccess(access.Header, access.Keys.With(next, key), unlocked);
    }

    /// <summary>Gives a new vault its first key, wrapped for <paramref name="identity"/> (a server account's): no identity of its own.</summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    internal static VaultAccess Create(string folder, VaultHeader header, Identity identity, UnlockedIdentity unlocked)
    {
        var key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
        WrapFor(folder, header.Id, 0, key, identity);
        return new VaultAccess(header, KeyRing.Of(key), unlocked);
    }

    // Derived from the identity's private key, not random: two devices attaching the vault at once
    // write the same key, whichever of their files the server keeps; what opened it before cannot.
    private static byte[] AttachKey(UnlockedIdentity unlocked, Guid vault, int epoch) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, unlocked.PrivateKey, SecretBox.KeySize, vault.ToByteArray(), Encoding.ASCII.GetBytes($"kofrenot vault key, epoch {epoch}"));
}
