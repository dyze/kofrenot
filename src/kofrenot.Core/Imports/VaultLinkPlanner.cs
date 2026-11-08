using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Imports;

/// <summary>
/// The links of an import from a vault: the items with the links each adds in their details (an item
/// unchanged but for a link is updated), every link of the other vault touching a node imported, the
/// link types to add, the links left out (their other node is not here, or their type cannot be added)
/// and what else is not imported.
/// </summary>
internal sealed record LinkPlan(IReadOnlyList<VaultImportItem> Items, IReadOnlyList<SourceLink> Links, IReadOnlyList<string> TypesAdded, int LeftOut, IReadOnlyList<string> Problems);

/// <summary>
/// Plans the links of an import from a vault, each node imported as chosen by default: a link is
/// added when both its nodes are here then (imported, or the same node here already) and it is not here
/// yet; its link type is copied when missing here. Its qualifiers come with it, an image only when downloaded.
/// </summary>
internal sealed class VaultLinkPlanner(KnowledgeGraph source, KnowledgeGraph target, VaultContent sourceContent, IReadOnlyDictionary<string, LinkTypeCopy> typeCopies)
{
    public LinkPlan Plan(IReadOnlyList<VaultImportItem> items)
    {
        var byNode = items.ToDictionary(item => item.Source.Id);
        var added = items.ToDictionary(item => item.Source.Id, _ => new List<string>());
        var links = new List<SourceLink>();
        var typesAdded = new List<string>();
        var problems = new List<string>();
        var leftOut = 0;
        foreach (var link in items.Where(item => item.Outcome != RecordOutcome.NotImported).SelectMany(item => source.LinksOf(item.Source.Id)).DistinctBy(link => link.Id))
        {
            links.Add(new SourceLink(link, QualifiersOf(link, problems)));
            if (Resolve(link.From) is not { } from || Resolve(link.To) is not { } to || !CanAdd(link.Type, problems))
            {
                leftOut++;
                continue;
            }

            if (target.FindLink(link.Type, from, to) is not null)
            {
                continue;
            }

            if (target.Types.FindLinkType(link.Type) is null && !typesAdded.Contains(link.Type))
            {
                typesAdded.Add(link.Type);
            }

            foreach (var end in new[] { link.From, link.To }.Where(end => byNode.TryGetValue(end, out var item) && item.Outcome != RecordOutcome.NotImported))
            {
                added[end].Add($"{source.Types.LinkName(link, end)}: + {source.FindNode(link.Other(end))!.Name}");
            }
        }

        return new LinkPlan(items.Select(item => WithLinks(item, added[item.Source.Id])).ToList(), links, typesAdded, leftOut, problems.Distinct().ToList());

        // A node of the other vault, here once imported as chosen by default: as imported, else the same node here already.
        Guid? Resolve(Guid node) =>
            byNode.TryGetValue(node, out var item) && item.LinkedAs(RecordChoice.Import) is { } here ? here
            : target.FindNode(node) is not null ? node
            : null;
    }

    private static VaultImportItem WithLinks(VaultImportItem item, List<string> links) => links.Count == 0
        ? item
        : item with { Outcome = item.Outcome == RecordOutcome.Unchanged ? RecordOutcome.Updated : item.Outcome, Details = [.. item.Details, .. links.Distinct()] };

    // A link type known here, or copied with the import; one whose name is taken here is told once.
    private bool CanAdd(string type, List<string> problems)
    {
        if (target.Types.FindLinkType(type) is not null)
        {
            return true;
        }

        if (!typeCopies.TryGetValue(type, out var copy))
        {
            return false;
        }

        if (copy.Outcome == LinkTypeCopyOutcome.NameTaken)
        {
            problems.Add($"Links “{new LinkVerb(copy.Type, IsReverse: false).Name}” left out: {copy.Status}");
            return false;
        }

        return true;
    }

    // Its qualifiers' values; an image neither there nor to download is left out, and told.
    private List<FieldSet> QualifiersOf(Link link, List<string> problems)
    {
        var values = source.QualifiersOf(link).Select(field => field.Shown).Where(value => value.Value is not null || value.File is not null).ToList();
        foreach (var value in values.Where(value => value.Image is { } image && !IsThere(image)))
        {
            problems.Add($"{source.FindNode(link.From)?.Name} · {value.Name}: its image is not on this computer");
        }

        return values.Where(value => value.Image is not { } image || IsThere(image)).ToList();
    }

    private bool IsThere(ContentEdited image) => sourceContent.Has(image) || sourceContent.CanDownload;
}
