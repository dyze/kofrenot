using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why the fields typed for a type cannot be written.</summary>
public enum FieldDraftsProblem
{
    None,

    /// <summary>A field has no name.</summary>
    FieldEmpty,

    /// <summary>Two fields have one name.</summary>
    FieldTwice,

    /// <summary>A choice field has no option, an empty one, or one twice.</summary>
    BadChoices,
}

/// <summary>The fields typed for a node type or a link type, as written: each with an id, its name and options trimmed.</summary>
internal sealed record CheckedFields(FieldDraftsProblem Problem, string? Detail, IReadOnlyList<FieldDefinition> Fields)
{
    /// <summary>
    /// Checks the fields typed: <see cref="Detail"/> names the field at fault. A content kind is
    /// never typed (the screens offer <see cref="FieldKinds.OfTypes"/>).
    /// </summary>
    /// <exception cref="ArgumentException">A field of a content kind.</exception>
    public static CheckedFields Of(IReadOnlyList<FieldDraft> drafts)
    {
        var fields = drafts.Select(FieldOf).ToList();
        if (fields.Any(field => field.Kind.IsContent()))
        {
            throw new ArgumentException("A type of the user's has no content field", nameof(drafts));
        }

        if (fields.Any(field => field.Name.Length == 0))
        {
            return new(FieldDraftsProblem.FieldEmpty, null, fields);
        }

        if (fields.GroupBy(field => NameKey.Of(field.Name)).FirstOrDefault(same => same.Count() > 1) is { } twice)
        {
            return new(FieldDraftsProblem.FieldTwice, twice.First().Name, fields);
        }

        return fields.FirstOrDefault(field => field.Kind == FieldKind.Choice && !AreValid(field.Choices)) is { } badChoices
            ? new(FieldDraftsProblem.BadChoices, badChoices.Name, fields)
            : new(FieldDraftsProblem.None, null, fields);
    }

    private static FieldDefinition FieldOf(FieldDraft draft) => new(
        draft.Id ?? Guid.NewGuid().ToString(),
        draft.Name.Trim(),
        draft.Kind,
        draft.Kind == FieldKind.Choice ? draft.Choices.Select(choice => choice.Trim()).ToList() : []);

    private static bool AreValid(IReadOnlyList<string> choices) =>
        choices.Count > 0 && choices.All(choice => choice.Length > 0) && choices.Distinct(StringComparer.CurrentCultureIgnoreCase).Count() == choices.Count;
}
