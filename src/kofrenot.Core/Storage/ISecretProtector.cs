namespace Kofrenot.Core.Storage;

/// <summary>
/// Protects a secret so only the current user of this computer can read it again (Windows: DPAPI,
/// in <c>App</c>).
/// </summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] secret);

    /// <exception cref="System.Security.Cryptography.CryptographicException">Another user or computer, or damaged.</exception>
    byte[] Unprotect(byte[] protectedSecret);
}
