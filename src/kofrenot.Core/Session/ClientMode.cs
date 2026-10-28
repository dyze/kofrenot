namespace Kofrenot.Core.Session;

/// <summary>What a kofrenot may change in a vault.</summary>
public enum ClientMode
{
    /// <summary>Anything its role allows (kofrenot on Windows).</summary>
    Full,

    /// <summary>
    /// The vault's nodes, as its role allows (kofrenot for Android): nodes, links, names, colours,
    /// pins, types and fields of a node, content, members invited. What defines the vault (the
    /// user's node and link types, aliases, its name, keys, a member removed) is left to kofrenot
    /// on Windows (<see cref="PhoneChanges"/>).
    /// </summary>
    Phone,
}
