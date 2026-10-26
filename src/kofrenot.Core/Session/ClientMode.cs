namespace Kofrenot.Core.Session;

/// <summary>What a kofrenot may change in a vault.</summary>
public enum ClientMode
{
    /// <summary>Anything its role allows (kofrenot on Windows).</summary>
    Full,

    /// <summary>
    /// Notes' and datasets' content only: a note saved, a file imported (kofrenot for Android).
    /// Everything else (nodes, links, pins, the vault itself, members, keys) is left to kofrenot on Windows.
    /// </summary>
    Content,
}
