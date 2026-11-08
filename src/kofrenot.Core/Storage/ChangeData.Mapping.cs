using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

internal sealed partial record ChangeData
{
    /// <summary>
    /// <c>"mapping": { "node": "…", "source": "json", "nodeType": "…", "name": ["title_fr", "title"], "match": "…", "otherNames": ["…"],
    /// "fields": [{ "key": "isbn", "field": "…" }], "links": [{ "key": "authors", "nodeType": "…", "linkType": "wrote", "reverse": true }],
    /// "replaces": ["…"] }</c>; no <c>match</c> to find nodes by their names only (since format 12).
    /// </summary>
    internal sealed record MappingData(
        Guid Node,
        string? Source,
        string? NodeType,
        List<string>? Name,
        string? Match,
        List<string>? OtherNames,
        List<FieldRuleData?>? Fields,
        List<LinkRuleData?>? Links,
        List<Guid>? Replaces)
    {
        public static MappingData From(MappingEdited mapping)
        {
            var definition = mapping.Definition;
            return new(
                mapping.Node,
                definition.Source,
                definition.NodeType,
                [.. definition.NameKeys],
                definition.Match,
                [.. definition.OtherNames],
                [.. definition.Fields.Select(rule => new FieldRuleData(rule.Key, rule.Field))],
                [.. definition.Links.Select(rule => new LinkRuleData(rule.Key, rule.NodeType, rule.LinkType, rule.IsReverse ? true : null))],
                [.. mapping.Replaces]);
        }

        /// <summary>The change, or null when incomplete: no node, an unknown source, no type or name key, or a rule incomplete.</summary>
        public MappingEdited? ToChange(Guid id, DateTimeOffset at)
        {
            if (Node == Guid.Empty || Source != MappingDefinition.Json || string.IsNullOrEmpty(NodeType) || Replaces is null
                || Name is not { Count: > 0 } names || names.Any(string.IsNullOrEmpty) || Match is { Length: 0 }
                || (OtherNames ?? []).Any(string.IsNullOrEmpty))
            {
                return null;
            }

            var fields = (Fields ?? []).Select(rule => rule is { Key.Length: > 0, Field.Length: > 0 } ? new FieldRule(rule.Key, rule.Field) : null).ToList();
            var links = (Links ?? []).Select(rule => rule is { Key.Length: > 0, NodeType.Length: > 0, LinkType.Length: > 0 }
                ? new LinkRule(rule.Key, rule.NodeType, rule.LinkType, rule.Reverse == true)
                : null).ToList();
            if (fields.Contains(null) || links.Contains(null))
            {
                return null;
            }

            var definition = new MappingDefinition(Source, NodeType, names, Match, OtherNames ?? [], fields!, links!);
            return new MappingEdited(id, at, Node, definition, Replaces);
        }
    }

    /// <summary><c>{ "key": "isbn", "field": "…" }</c></summary>
    internal sealed record FieldRuleData(string? Key, string? Field);

    /// <summary><c>{ "key": "authors", "nodeType": "…", "linkType": "wrote", "reverse": true }</c>; no <c>reverse</c> for a link from the imported node.</summary>
    internal sealed record LinkRuleData(string? Key, string? NodeType, string? LinkType, bool? Reverse = null);
}
