using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Imports;

/// <summary>
/// Plans importing nodes of another vault into this one (<see cref="VaultImportPlan"/>), writing
/// nothing: the nodes a query found there, with their subtrees when asked. Each finds its node here
/// by its id (imported before), else by its name among the nodes of its first type (to check); it is
/// then new (added with its id there), updated (only what differs), to check, unchanged or not
/// imported. The node and link types missing here are copied with their ids. Views and mappings are
/// not imported.
/// </summary>
public sealed class VaultImportPlanner(KnowledgeGraph target, VaultContent targetContent, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="sourceName">The other vault's name, as the reasons say it.</param>
    /// <param name="hits">The nodes the query found in <paramref name="source"/>.</param>
    /// <param name="subtreeLinkType">Each node's subtree by this link type is imported too; null: the nodes only.</param>
    public VaultImportPlan Plan(KnowledgeGraph source, VaultContent sourceContent, string sourceName, IReadOnlyList<Node> hits, string? subtreeLinkType = null)
    {
        var at = _clock.GetUtcNow();
        var nodes = NodesOf(source, hits, subtreeLinkType);
        var ids = nodes.Select(node => node.Id).ToHashSet();
        var typeCopies = new TypeCopyPlanner(target, _clock)
            .Plan(source.NodeTypes.All.Where(custom => nodes.Any(node => source.HasType(node.Id, custom.Type.Id))).ToList());
        var taken = typeCopies.Where(copy => copy.Outcome == TypeCopyOutcome.NameTaken).Select(copy => copy.Type.Type.Id).ToHashSet();
        var writes = new VaultNodeWrites(source, sourceContent, sourceName, target, targetContent, at);
        var items = WithoutDuplicates(nodes.Select(node => PlanNode(source, node, ids, taken, writes)).ToList());

        var linkTypeCopies = new LinkTypeCopyPlanner(target, _clock).Plan(source)
            .Where(copy => !copy.IsBuiltIn && copy.Outcome is LinkTypeCopyOutcome.Adds or LinkTypeCopyOutcome.NameTaken)
            .ToDictionary(copy => copy.Type.Id);
        var links = new VaultLinkPlanner(source, target, sourceContent, linkTypeCopies).Plan(items);
        var chosenTypes = links.Items.Where(item => item.IsChosen).SelectMany(item => item.Types).ToHashSet();
        var addedTypes = typeCopies.Where(copy => copy.Outcome == TypeCopyOutcome.Adds).ToList();
        return new VaultImportPlan(
            links.Items,
            addedTypes.Where(copy => chosenTypes.Contains(copy.Type.Type.Id)).Select(copy => copy.Type.Type.Name).ToList(),
            links.TypesAdded.Select(type => new LinkVerb(linkTypeCopies[type].Type, IsReverse: false).Name).ToList(),
            links.LeftOut,
            links.Problems)
        {
            Target = target,
            At = at,
            NodeTypeChanges = addedTypes.Select(copy => copy.Change!).ToList(),
            LinkTypeChanges = linkTypeCopies.Values.Where(copy => copy.Outcome == LinkTypeCopyOutcome.Adds).ToDictionary(copy => copy.Type.Id, copy => copy.Changes),
            Links = links.Links,
        };
    }

    // The nodes found, then the nodes under them by the link type, each once.
    private static List<Node> NodesOf(KnowledgeGraph source, IReadOnlyList<Node> hits, string? subtreeLinkType)
    {
        var nodes = hits.DistinctBy(node => node.Id).Select(hit => source.FindNode(hit.Id)!).ToList();
        if (subtreeLinkType is null)
        {
            return nodes;
        }

        var seen = nodes.Select(node => node.Id).ToHashSet();
        var tree = new TreeLinks(source, subtreeLinkType);
        nodes.AddRange(hits.SelectMany(hit => tree.Subtree(hit.Id)).Where(seen.Add).Select(id => source.FindNode(id)!).ToList());
        return nodes;
    }

    private VaultImportItem PlanNode(KnowledgeGraph source, Node node, IReadOnlySet<Guid> ids, IReadOnlySet<string> takenTypes, VaultNodeWrites writes)
    {
        var types = source.TypeIdsOf(node.Id);
        var item = new VaultImportItem(node, RecordOutcome.NotImported, null, null, []) { Types = types };
        if (new[] { TypeCatalog.View, TypeCatalog.Mapping }.FirstOrDefault(type => source.HasType(node.Id, type.Id)) is { } left)
        {
            return item with { Reason = $"a {left.Name.ToLowerInvariant()} is not imported" };
        }

        if (types.FirstOrDefault(takenTypes.Contains) is { } taken)
        {
            return item with { Reason = $"its type “{source.Types.NodeTypeName(taken)}” has the name of another type here: rename one first" };
        }

        var found = Find(source, node, ids);
        if (found.Kind == FoundKind.Ambiguous)
        {
            return item with { Reason = found.Reason };
        }

        var asNew = found.Kind == FoundKind.Found ? null : writes.New(node);
        var update = found.Node is { } here ? writes.Update(node, here) : null;
        if ((asNew?.Missing ?? update?.Missing) is { } missing)
        {
            return item with { Found = found.Node, Reason = missing };
        }

        item = item with { Found = found.Node, AsNew = asNew?.Write ?? NodeWrite.None, AsUpdate = update?.Write ?? NodeWrite.None };
        return found.Kind switch
        {
            FoundKind.New => item with { Outcome = RecordOutcome.New, Details = asNew!.Details },
            // Found by its name: to check only when updating it would change it.
            FoundKind.ToCheck when update!.Details.Count == 0 => item with { Outcome = RecordOutcome.Unchanged, Details = ["same as a node here, found by its name"] },
            FoundKind.ToCheck => item with { Outcome = RecordOutcome.ToCheck, Details = ["same name as a node here", .. update.Details] },
            _ => item with { Outcome = update!.Details.Count == 0 ? RecordOutcome.Unchanged : RecordOutcome.Updated, Details = update.Details },
        };
    }

    // The same node (imported before), else one of the same name and first type, not imported from there under another id.
    private Found Find(KnowledgeGraph source, Node node, IReadOnlySet<Guid> ids)
    {
        if (target.FindNode(node.Id) is { } same)
        {
            return new Found(FoundKind.Found, same);
        }

        var type = source.TypeIdsOf(node.Id).FirstOrDefault() ?? TypeCatalog.Generic.Id;
        var named = target.NodesNamed(node.Name).Where(here => !ids.Contains(here.Id) && target.HasType(here.Id, type)).ToList();
        return named switch
        {
            [] => new Found(FoundKind.New),
            [var one] => new Found(FoundKind.ToCheck, one),
            _ => new Found(FoundKind.Ambiguous, Reason: $"{named.Count} nodes of this name are here"),
        };
    }

    // Two nodes found by their name to one node here: neither is imported.
    private static List<VaultImportItem> WithoutDuplicates(List<VaultImportItem> items)
    {
        var shared = items.Where(item => item.Outcome != RecordOutcome.NotImported && item.Found is { } found && found.Id != item.Source.Id)
            .GroupBy(item => item.Found!.Id)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => (Item: item, Other: group.First(other => other != item))))
            .ToDictionary(pair => pair.Item, pair => pair.Other);
        return items.Select(item => shared.TryGetValue(item, out var other)
            ? item with { Outcome = RecordOutcome.NotImported, Reason = $"same node here as “{other.Source.Name}”", Details = [] }
            : item).ToList();
    }
}
