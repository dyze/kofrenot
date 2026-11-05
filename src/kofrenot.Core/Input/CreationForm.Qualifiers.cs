using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A qualifier of the link type chosen on the creation screen: <see cref="Text"/> is what is typed for it
/// (empty: left as it is), <see cref="Current"/> its value on the link updated, shown; null when it has none.
/// </summary>
public sealed record QualifierLine(FieldDefinition Field, string Text, string? Current);

/// <summary>
/// The qualifiers on the creation screen's link step, all optional: those of the link type chosen that are
/// typed (an image is chosen in the link's details). Filled from the input box, changed here.
/// </summary>
public sealed partial class CreationForm
{
    /// <summary>The qualifiers of the link type chosen, with what is typed; none for a node, or a type without.</summary>
    public IReadOnlyList<QualifierLine> Qualifiers
    {
        get
        {
            if (_request is not LinkRequest request)
            {
                return [];
            }

            var current = Plan is LinkPlan { Existing: { } existing } ? NodeFields.OfLink(_graph, existing) : [];
            return _graph.LinkTypes.FieldsOf(request.Verb.Type.Id)
                .Where(qualifier => qualifier.Kind.IsSmall())
                .Select(qualifier => new QualifierLine(
                    qualifier,
                    request.QualifiersTyped.LastOrDefault(typed => NameKey.Of(typed.Name) == NameKey.Of(qualifier.Name))?.Value ?? string.Empty,
                    current.FirstOrDefault(shown => !shown.IsOwn && shown.Field == qualifier.Id && shown.HasValue)?.Display))
                .ToList();
        }
    }

    /// <summary>A qualifier typed on the screen; empty leaves it as it is.</summary>
    /// <exception cref="InvalidOperationException">Set on a node request.</exception>
    public void SetQualifier(FieldDefinition field, string text)
    {
        var request = _request as LinkRequest ?? throw new InvalidOperationException("A node has no qualifiers.");
        var others = request.QualifiersTyped.Where(typed => NameKey.Of(typed.Name) != NameKey.Of(field.Name));
        var value = string.Join(' ', InputParser.Words(text));
        Replan(request with { Qualifiers = value.Length == 0 ? [.. others] : [.. others, new QualifierInput(field.Name, value)] }, _choices);
    }
}
