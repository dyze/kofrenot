using Kofrenot.Core.Server;

namespace Kofrenot.Core.Session;

/// <summary>A store of the server not listed here yet, with its vault's name when the account keeps it.</summary>
public sealed record StoreChoice(ServerStore Store, string? Name)
{
    /// <summary>Its name, else its id ("Vault 10a07177").</summary>
    public string Title => Name ?? $"Vault {Store.Id.ToString()[..8]}";
}
