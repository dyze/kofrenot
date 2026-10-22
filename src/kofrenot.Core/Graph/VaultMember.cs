namespace Kofrenot.Core.Graph;

/// <summary>What a member of a shared vault may do: read, also write, or also share (its creator).</summary>
public enum MemberRole
{
    Reader,
    Writer,
    Owner,
}

/// <summary>
/// A member of a shared server vault: their server account, the identity the vault's keys are
/// wrapped for and its public key (so a rotation never asks the server for it, and the server
/// cannot swap it), the email they were found by, and their role.
/// </summary>
public sealed record VaultMember(Guid Account, Guid Identity, byte[] PublicKey, string Email, MemberRole Role);
