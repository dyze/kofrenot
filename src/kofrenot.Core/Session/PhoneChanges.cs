using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Session;

/// <summary>
/// The changes kofrenot for Android writes (<see cref="ClientMode.Phone"/>): those of the vault's
/// nodes. Those that define the vault (the user's node and link types, aliases, the vault's name,
/// a member removed, which rotates its key) are left to kofrenot on Windows, so the phone cannot
/// leave a vault in a state another kofrenot cannot fix.
/// </summary>
public static class PhoneChanges
{
    public static bool Allows(Change change, KnowledgeGraph graph) => change switch
    {
        NodeTypeDefined or LinkTypeDefined or AliasAdded or VaultNamed or MemberRemoved => false,
        ChangesRemoved removal => !RemovesDefinitions(removal, graph),
        _ => true,
    };

    // A removal of a node type, a link type or an alias; one of nodes, links and pins is allowed.
    private static bool RemovesDefinitions(ChangesRemoved removal, KnowledgeGraph graph) =>
        graph.NodeTypes.DefinedBy(removal.Removes).Count > 0
        || graph.LinkTypes.NamedBy(removal.Removes).Count > 0
        || graph.Aliases.All.Any(alias => removal.Removes.Contains(alias.Id));
}
