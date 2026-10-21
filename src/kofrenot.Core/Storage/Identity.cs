using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

namespace Kofrenot.Core.Storage;

/// <summary>An identity's private key (PKCS#8, P-256), once unlocked. Secret: never stored in plain, never logged.</summary>
public sealed record UnlockedIdentity(Guid Id, byte[] PrivateKey);

/// <summary>
/// A key pair (ECDH P-256) the vault keys are wrapped for (<see cref="EpochKeys"/>): its public
/// key, and its private key sealed twice, by the pass phrase (<see cref="KeyWrap"/>) and by the
/// recovery key (HKDF-SHA256, then AES-256-GCM, "KFR1"). Nothing in it is secret.
/// </summary>
public sealed record Identity(Guid Id, byte[] PublicKey, WrappedKey ByPassPhrase, byte[] ByRecoveryKey)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static ReadOnlySpan<byte> RecoveryMagic => "KFR1"u8;

    /// <summary>A new identity, unlocked, and its recovery key, to show the user once.</summary>
    public static (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) Create(string passPhrase, int iterations = KeyWrap.DefaultIterations)
    {
        using var keys = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var unlocked = new UnlockedIdentity(Guid.NewGuid(), keys.ExportPkcs8PrivateKey());
        var recovery = RecoveryKey.New();
        var identity = new Identity(
            unlocked.Id,
            keys.ExportSubjectPublicKeyInfo(),
            KeyWrap.Seal(unlocked.PrivateKey, passPhrase, unlocked.Id.ToByteArray(), iterations),
            new SecretBox(BoxKeyOf(recovery, unlocked.Id)).Seal(RecoveryMagic, unlocked.PrivateKey, unlocked.Id.ToByteArray()));
        return (identity, unlocked, recovery);
    }

    /// <summary>The private key, or null when the pass phrase is wrong.</summary>
    public UnlockedIdentity? Unlock(string passPhrase) =>
        KeyWrap.Unlock(ByPassPhrase, passPhrase, Id.ToByteArray()) is { } privateKey ? new UnlockedIdentity(Id, privateKey) : null;

    /// <summary>The private key, or null when the recovery key is not this identity's.</summary>
    public UnlockedIdentity? Unlock(RecoveryKey recovery)
    {
        try
        {
            return new UnlockedIdentity(Id, new SecretBox(BoxKeyOf(recovery, Id)).Open(RecoveryMagic, ByRecoveryKey, Id.ToByteArray()));
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>The same identity, its private key sealed by another pass phrase; the recovery key still opens it.</summary>
    public Identity WithPassPhrase(UnlockedIdentity unlocked, string passPhrase, int iterations = KeyWrap.DefaultIterations) =>
        unlocked.Id == Id
            ? this with { ByPassPhrase = KeyWrap.Seal(unlocked.PrivateKey, passPhrase, Id.ToByteArray(), iterations) }
            : throw new ArgumentException("Another identity", nameof(unlocked));

    /// <exception cref="InvalidDataException">Damaged.</exception>
    /// <exception cref="IOException">Cannot be read.</exception>
    public static Identity Read(string path) => FromJson(File.ReadAllBytes(path));

    /// <summary>An identity from its JSON (a file, or a server account's data).</summary>
    /// <exception cref="InvalidDataException">Damaged.</exception>
    public static Identity FromJson(byte[] json)
    {
        try
        {
            return JsonSerializer.Deserialize<Identity>(json, Json) is { } identity && IsComplete(identity)
                ? identity
                : throw new InvalidDataException("The identity is damaged");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The identity is damaged", ex);
        }
    }

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, Json);

    internal void Write(string path, bool overwrite) => AtomicFile.Write(path, ToJson(), overwrite);

    private static byte[] BoxKeyOf(RecoveryKey recovery, Guid identity) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, recovery.Bytes, SecretBox.KeySize, identity.ToByteArray(), "kofrenot identity recovery"u8.ToArray());

    // JSON can leave out any field, whatever the types say.
    private static bool IsComplete([NotNullWhen(true)] Identity? identity) =>
        identity is { PublicKey: not null, ByRecoveryKey: not null, ByPassPhrase: { Salt: not null, Sealed: not null, Iterations: > 0 } } && identity.Id != Guid.Empty;
}
