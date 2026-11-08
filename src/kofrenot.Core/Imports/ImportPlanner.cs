using System.Globalization;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Imports;

/// <summary>
/// Plans importing a file's records through a mapping (<see cref="RecordPlan"/>), writing nothing:
/// each record is named by the first of the name keys it has, finds its node (<see cref="NodeFinder"/>),
/// and is new, updated (only what differs), to check, unchanged or not imported. An import never
/// clears a value nor removes a link. Two records of the file for one node are both left out.
/// </summary>
public sealed class ImportPlanner(KnowledgeGraph graph)
{
    public RecordPlan Plan(MappingDefinition mapping, RecordFile file)
    {
        if (ImportRules.Of(graph, mapping) is not { } rules)
        {
            return RecordPlan.Refused("The mapping's node type is gone: choose another one in Edit….");
        }

        var finder = new NodeFinder(graph, rules);
        var items = file.Records.Select(record => PlanRecord(record, rules, finder)).ToList();
        return new RecordPlan(WithoutDuplicates(items, rules), rules.Warnings);
    }

    private RecordItem PlanRecord(ImportRecord record, ImportRules rules, NodeFinder finder)
    {
        var name = rules.NameKeys.Select(record.FirstOf).FirstOrDefault(value => value is not null);
        if (name is null)
        {
            return new RecordItem(record, RecordOutcome.NotImported, null, null, $"no {string.Join(" nor ", rules.NameKeys.Select(key => $"“{key}”"))}", []);
        }

        var problems = new List<string>();
        var values = ValuesOf(record, rules, problems);
        var links = LinksOf(record, rules, problems);
        var matchValue = rules.Match is { } match ? values.FirstOrDefault(value => value.Field.Id == match.Id)?.Value : null;
        var otherNames = values.Where(value => rules.OtherNames.Contains(value.Field)).Select(value => (value.Field, value.Value)).ToList();
        var found = finder.Find(matchValue, name, otherNames);
        var item = new RecordItem(record, RecordOutcome.New, name, null, null, [])
        {
            NodeType = rules.Type.Id,
            Values = values,
            Links = links,
            ValueProblems = problems,
        };

        return found.Kind switch
        {
            FoundKind.New => item with { Details = NewDetails(values, links, rules) },
            FoundKind.Ambiguous => item with { Outcome = RecordOutcome.NotImported, Reason = found.Reason },
            _ => Differences(item, found, rules),
        };
    }

    private RecordItem Differences(RecordItem item, Found found, ImportRules rules)
    {
        var node = found.Node!;
        var details = new List<string>();
        if (found.By is { } by)
        {
            details.Add($"found by {by}");
        }

        IReadOnlyList<Guid>? renameOver = null;
        if (item.Name != node.Name)
        {
            renameOver = graph.NamesOf(node.Id).Select(name => name.Id).ToList();
            details.Add($"name: {node.Name} → {item.Name}");
        }

        var current = graph.FieldsOf(node.Id).Where(field => field.Owner == rules.Type.Id).ToDictionary(field => field.Field);
        var updates = new List<ValueToSet>();
        foreach (var value in item.Values)
        {
            var versions = current.GetValueOrDefault(value.Field.Id);
            if (versions?.Shown.Value == value.Value && versions.IsConflict != true)
            {
                continue;
            }

            updates.Add(value with { Replaces = versions?.Current.Select(version => version.Id).ToList() ?? [] });
            var old = versions?.Shown.Value is { } shown ? FieldValue.Display(value.Field.Kind, shown) : "(empty)";
            details.Add($"{value.Field.Name}: {old} → {FieldValue.Display(value.Field.Kind, value.Value)}");
        }

        var missing = item.Links.Where(link => !IsLinked(node, link)).ToList();
        details.AddRange(missing.Select(link => LinkDetail(link, rules)));
        var outcome = found.Kind == FoundKind.ToCheck ? RecordOutcome.ToCheck
            : renameOver is null && updates.Count == 0 && missing.Count == 0 ? RecordOutcome.Unchanged
            : RecordOutcome.Updated;
        return item with { Outcome = outcome, Node = node, Details = details, RenameOver = renameOver, Updates = updates, MissingLinks = missing };
    }

    private bool IsLinked(Node node, LinkToAdd link)
    {
        if (link.Target.Node is not { } target)
        {
            return false;
        }

        var (from, to) = link.Rule.IsReverse ? (target, node.Id) : (node.Id, target);
        return graph.FindLink(link.Rule.LinkType, from, to) is not null;
    }

    // Each field's first value, as its kind writes it; one that does not fit, and the others, listed.
    private static List<ValueToSet> ValuesOf(ImportRecord record, ImportRules rules, List<string> problems)
    {
        var values = new List<ValueToSet>();
        foreach (var rule in rules.Fields)
        {
            var raw = record.ValuesOf(rule.Key);
            if (raw.Count == 0 || values.Any(value => value.Field.Id == rule.Field.Id))
            {
                continue;
            }

            var input = FieldValue.Parse(rule.Field.Kind, raw[0], rule.Field.Choices, CultureInfo.InvariantCulture);
            if (input.IsValid)
            {
                values.Add(new ValueToSet(rule.Field, input.Value!, []));
            }
            else
            {
                problems.Add($"{rule.Key} “{raw[0]}”: {input.Error}");
            }

            if (raw.Count > 1)
            {
                problems.Add($"{rule.Key}: {raw.Count - 1} more value(s), {rule.Field.Name} holds one");
            }
        }

        return values;
    }

    // Each value a node's name: found among the nodes of the rule's type, else added; several found, listed.
    private List<LinkToAdd> LinksOf(ImportRecord record, ImportRules rules, List<string> problems)
    {
        var links = new List<LinkToAdd>();
        foreach (var rule in rules.Links)
        {
            foreach (var name in record.ValuesOf(rule.Rule.Key).DistinctBy(NameKey.Of))
            {
                var named = graph.NodesNamed(name).Where(node => graph.HasType(node.Id, rule.Rule.NodeType)).ToList();
                if (named.Count > 1)
                {
                    problems.Add($"{rule.Rule.Key} “{name}”: {named.Count} nodes have this name");
                    continue;
                }

                links.Add(new LinkToAdd(rule.Rule, new LinkTarget(rule.Rule.NodeType, name, named.FirstOrDefault()?.Id)));
            }
        }

        return links;
    }

    private static List<string> NewDetails(IReadOnlyList<ValueToSet> values, IReadOnlyList<LinkToAdd> links, ImportRules rules) =>
        [.. values.Select(value => $"{value.Field.Name}: {FieldValue.Display(value.Field.Kind, value.Value)}"), .. links.Select(link => LinkDetail(link, rules))];

    private static string LinkDetail(LinkToAdd link, ImportRules rules) =>
        $"{rules.Links.First(rule => rule.Rule == link.Rule).Verb}: + {link.Target.Name}{(link.Target.Node is null ? " (new)" : string.Empty)}";

    // Two records for one node found, or for one new node (same match value, or same name): neither is imported.
    private static List<RecordItem> WithoutDuplicates(List<RecordItem> items, ImportRules rules)
    {
        var groups = items.Where(item => item.Outcome != RecordOutcome.NotImported)
            .SelectMany(item => KeysOf(item, rules).Select(key => (Key: key, Item: item)))
            .GroupBy(pair => pair.Key, pair => pair.Item)
            .Where(group => group.Count() > 1)
            .ToList();
        var firstOther = new Dictionary<RecordItem, int>();
        foreach (var group in groups)
        {
            foreach (var item in group)
            {
                firstOther.TryAdd(item, group.First(other => other != item).Record.Number);
            }
        }

        return items.Select(item => firstOther.TryGetValue(item, out var other)
            ? item with { Outcome = RecordOutcome.NotImported, Reason = $"same node as record {other}", Details = [] }
            : item).ToList();
    }

    private static IEnumerable<string> KeysOf(RecordItem item, ImportRules rules)
    {
        if (item.Node is { } node)
        {
            yield return $"node:{node.Id}";
            yield break;
        }

        yield return $"name:{NameKey.Of(item.Name!)}";
        if (rules.Match is { } match && item.Values.FirstOrDefault(value => value.Field.Id == match.Id) is { } value)
        {
            yield return $"match:{value.Value}";
        }
    }
}
