using System.Security.Cryptography;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using CipherMode = Javax.Crypto.CipherMode;
using Javax.Crypto.Spec;
using Kofrenot.Core.Storage;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// Protects the remembered keys and the server session with an AES key of the Android Keystore:
/// the key never leaves it, so a copy of the app's files cannot be read elsewhere.
/// </summary>
public sealed class KeystoreProtector : ISecretProtector
{
    private const string Store = "AndroidKeyStore";
    private const string Alias = "kofrenot remembered key";
    private const string Transformation = "AES/GCM/NoPadding";
    private const int IvBytes = 12;
    private const int TagBits = 128;

    public byte[] Protect(byte[] secret)
    {
        using var cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(CipherMode.EncryptMode, Key());
        var iv = cipher.GetIV()!;
        return [.. iv, .. cipher.DoFinal(secret)!];
    }

    public byte[] Unprotect(byte[] protectedSecret)
    {
        if (protectedSecret.Length <= IvBytes)
        {
            throw new CryptographicException("The protected data is damaged");
        }

        try
        {
            using var cipher = Cipher.GetInstance(Transformation)!;
            cipher.Init(CipherMode.DecryptMode, Key(), new GCMParameterSpec(TagBits, protectedSecret[..IvBytes]));
            return cipher.DoFinal(protectedSecret[IvBytes..])!;
        }
        catch (GeneralSecurityException ex)
        {
            // Another phone, a key the Keystore lost (the phone's lock removed), or damaged data.
            throw new CryptographicException(ex.Message, ex);
        }
    }

    private static IKey Key()
    {
        var keyStore = KeyStore.GetInstance(Store)!;
        keyStore.Load(null);
        if (keyStore.GetKey(Alias, null) is { } key)
        {
            return key;
        }

        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, Store)!;
        generator.Init(new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
            .SetKeySize(256)!
            .Build());
        return generator.GenerateKey()!;
    }
}
