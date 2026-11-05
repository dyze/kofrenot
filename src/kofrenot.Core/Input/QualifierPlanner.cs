using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The qualifiers typed after a link sentence, as changes: on a new link, the values typed; on an existing one,
/// a qualifier absent is added, one different changed, one typed without value taken out. They are the link
/// type's (<see cref="QualifierText"/>); a value that does not fit refuses the plan.
/// </summary>
internal sealed class QualifierPlanner(KnowledgeGraph graph, LinkType type, TimeProvider clock)
{
    private readonly IReadOnlyList<FieldDefinition> _fields = graph.LinkTypes.FieldsOf(type.Id);

    /// <summary>The new link with its qualifiers; a qualifier typed without value is left out.</summary>
    public LinkPlan Add(LinkPlan plan, (Link Link, List<Change> Changes) added, IReadOnlyList<QualifierInput> typed)
    {
        var (link, changes) = added;
        var shown = new List<QualifierChange>();
        foreach (var (field, input) in Fields(typed).Where(qualifier => !qualifier.Input.IsRemoval))
        {
            var value = FieldValue.Parse(field.Kind, input.Value, field.Choices);
            if (!value.IsValid)
            {
                return Invalid(plan, field, value.Error!);
            }

            changes.Add(new FieldSet(Guid.NewGuid(), clock.GetUtcNow(), link, type.Id, field.Id, field.Name, field.Kind, value.Value, []));
            shown.Add(new QualifierChange(field.Name, Old: null, FieldValue.Display(field.Kind, value.Value!)));
        }

        return plan with { Changes = changes, Qualifiers = shown };
    }

    /// <summary>The qualifiers of <paramref name="link"/> as typed; nothing when they are so already.</summary>
    public LinkPlan Update(LinkPlan plan, Link link, IReadOnlyList<QualifierInput> typed)
    {
        var current = NodeFields.OfLink(graph, link);
        var planner = new FieldPlanner(graph, clock);
        var changes = new List<Change>();
        var shown = new List<QualifierChange>();
        foreach (var (field, input) in Fields(typed))
        {
            var shownField = current.First(candidate => !candidate.IsOwn && candidate.Field == field.Id);
            var written = planner.Set(link, shownField, input.Value);
            if (written.Problem == FieldProblem.Invalid)
            {
                return Invalid(plan with { Existing = link }, field, written.Error!);
            }

            if (written.CanApply)
            {
                changes.AddRange(written.Changes);
                var value = ((FieldSet)written.Changes[0]).Value;
                shown.Add(new QualifierChange(field.Name, shownField.HasValue ? shownField.Display : null, value is null ? null : FieldValue.Display(field.Kind, value)));
            }
        }

        return changes.Count == 0
            ? plan with { Problem = LinkProblem.QualifiersUnchanged, Existing = link }
            : plan with { Existing = link, Changes = changes, Qualifiers = shown };
    }

    // The qualifiers typed, each with its field: the last value typed for a name wins.
    private IEnumerable<(FieldDefinition Field, QualifierInput Input)> Fields(IReadOnlyList<QualifierInput> typed) =>
        typed
            .Select(input => (Field: _fields.FirstOrDefault(field => NameKey.Of(field.Name) == NameKey.Of(input.Name)), Input: input))
            .Where(qualifier => qualifier.Field is not null)
            .GroupBy(qualifier => qualifier.Field!.Id)
            .Select(same => (same.Last().Field!, same.Last().Input));

    private static LinkPlan Invalid(LinkPlan plan, FieldDefinition field, string error) =>
        plan with { Problem = LinkProblem.QualifierInvalid, QualifierError = $"{field.Name}: {error}", Changes = [], Qualifiers = [] };
}
