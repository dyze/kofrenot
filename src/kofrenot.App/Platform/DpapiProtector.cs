using System.Security.Cryptography;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Protects the remembered master keys with Windows' DPAPI, for the current user: another user,
/// or a copy of the file on another computer, cannot read them.
/// </summary>
public sealed class DpapiProtector : ISecretProtector
{
    // Ties the protected data to kofrenot: another program of the user cannot pass it off as its own.
    private static readonly byte[] Entropy = "kofrenot remembered key"u8.ToArray();

    public byte[] Protect(byte[] secret) => ProtectedData.Protect(secret, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedSecret) => ProtectedData.Unprotect(protectedSecret, Entropy, DataProtectionScope.CurrentUser);
}
