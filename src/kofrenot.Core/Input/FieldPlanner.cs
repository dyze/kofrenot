using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a field's value cannot be written as chosen.</summary>
public enum FieldProblem
{
    None,

    /// <summary>The value does not fit the field's kind (<see cref="FieldPlan.Error"/>).</summary>
    Invalid,

    /// <summary>The field has this value already, with no conflict.</summary>
    Unchanged,

    /// <summary>A field of the holder's own with no name.</summary>
    NameEmpty,

    /// <summary>The node or link has a field of its own with this name already.</summary>
    NameTaken,
}

/// <summary>Writes the values of a node's fields; <see cref="Changes"/> is empty when it cannot (<see cref="Problem"/>).</summary>
public sealed record FieldPlan(FieldProblem Problem, string? Error, IReadOnlyList<Change> Changes)
{
    public bool CanApply => Changes.Count > 0;

    /// <summary>Why it cannot be written; empty when it can, or nothing changes.</summary>
    public string Status => Problem switch
    {
        FieldProblem.Invalid => Error!,
        FieldProblem.NameEmpty => "Name the field.",
        FieldProblem.NameTaken => "It has a field of its own with this name already.",
        _ => string.Empty,
    };

    internal static FieldPlan Refused(FieldProblem problem, string? error = null) => new(problem, error, []);
}

/// <summary>
/// Turns what is typed in a node's fields, or a link's qualifiers (<see cref="FieldHolder"/>), into the changes that write it (<see cref="FieldSet"/>).
/// A value replaces every current one, so it resolves a conflict. A field of the node's own exists
/// while it has a value: cleared, it goes. Two fields of the same name are merged only on demand,
/// keeping one value.
/// </summary>
public sealed class FieldPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    /// <summary>The kinds a field of a holder's own can have, typed: a choice needs its options, from a type.</summary>
    public static IReadOnlyList<FieldKind> OwnKinds { get; } = FieldKinds.Small.Where(kind => kind != FieldKind.Choice).ToList();

    /// <summary>Why <paramref name="file"/> cannot be an image field's: null when it can (one kofrenot shows, small enough to decode).</summary>
    public static string? WhyNotImage(DatasetFile file) =>
        DatasetType.Of(file).Viewer != DatasetViewer.Image ? $"“{file.Name}” is not an image kofrenot shows: choose a PNG, JPEG, GIF, BMP, TIFF or WebP."
        : file.Size > DatasetType.MaxImageSize ? $"“{file.Name}” is too large for an image field: {ByteSize.Format(DatasetType.MaxImageSize)} at most."
        : null;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The value typed for a field: empty clears it.</summary>
    public FieldPlan Set(FieldHolder holder, NodeField field, string text)
    {
        if (text.Trim().Length == 0)
        {
            return Clear(holder, field);
        }

        var input = FieldValue.Parse(field.Kind, text, field.Choices);
        return input.IsValid ? Write(holder, field, input.Value) : FieldPlan.Refused(FieldProblem.Invalid, input.Error);
    }

    /// <summary>A checkbox checked or not.</summary>
    public FieldPlan Check(FieldHolder holder, NodeField field, bool isChecked) => Write(holder, field, FieldValue.Of(isChecked));

    public FieldPlan Clear(FieldHolder holder, NodeField field) => Write(holder, field, value: null);

    /// <summary>An image chosen for an image field: its file is written with the change (<c>VaultSession.SaveImage</c>).</summary>
    public FieldPlan SetImage(FieldHolder holder, NodeField field, DatasetFile file)
    {
        if (field.Kind != FieldKind.Image)
        {
            throw new ArgumentException("Not an image field", nameof(field));
        }

        return WhyNotImage(file) is { } why ? FieldPlan.Refused(FieldProblem.Invalid, why) : Write(holder, field, value: null, file);
    }

    /// <summary>
    /// An image dataset's file as an image field's: nothing copied, the field shows that version's
    /// content (<see cref="FieldSet.Content"/>), kept as every version is.
    /// </summary>
    public FieldPlan SetImageFrom(FieldHolder holder, NodeField field, DatasetEdited image)
    {
        var plan = SetImage(holder, field, image.File);
        return plan.Changes is [FieldSet value] ? plan with { Changes = [value with { Content = image.Id }] } : plan;
    }

    /// <summary>Keeps one of the values of a conflict (<see cref="FieldVersions.Current"/>); an image keeps its file, written already.</summary>
    public FieldPlan Keep(FieldHolder holder, NodeField field, FieldSet version) =>
        version.File is null
            ? Write(holder, field, version.Value)
            : new FieldPlan(FieldProblem.None, Error: null, [version with { Id = Guid.NewGuid(), At = _clock.GetUtcNow(), Replaces = field.Current, Content = version.Content ?? version.Id }]);

    /// <summary>A field of the holder's own, with its first value.</summary>
    public FieldPlan AddOwn(FieldHolder holder, string nameText, FieldKind kind, string text)
    {
        var name = nameText.Trim();
        if (name.Length == 0)
        {
            return FieldPlan.Refused(FieldProblem.NameEmpty);
        }

        if (!OwnKinds.Contains(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of an own field");
        }

        if (NodeFields.Of(graph, holder).Any(field => field.IsOwn && NameKey.Of(field.Name) == NameKey.Of(name)))
        {
            return FieldPlan.Refused(FieldProblem.NameTaken);
        }

        var input = FieldValue.Parse(kind, text);
        if (!input.IsValid)
        {
            return FieldPlan.Refused(FieldProblem.Invalid, input.Error);
        }

        var change = new FieldSet(Guid.NewGuid(), _clock.GetUtcNow(), holder, FieldSet.Own, Guid.NewGuid().ToString(), name, kind, input.Value, []);
        return new FieldPlan(FieldProblem.None, Error: null, [change]);
    }

    /// <summary>
    /// Merges two fields of the same name into <paramref name="kept"/>, with <paramref name="value"/>
    /// (one of theirs): the other one is cleared, so a field of the holder's own goes.
    /// </summary>
    public FieldPlan Merge(FieldHolder holder, NodeField kept, NodeField dropped, string value)
    {
        if (!FieldValue.IsValid(kept.Kind, value) || (kept.Kind == FieldKind.Choice && !kept.Choices.Contains(value)))
        {
            return FieldPlan.Refused(FieldProblem.Invalid, $"“{FieldValue.Display(dropped.Kind, value)}” does not fit {kept.Label}.");
        }

        var now = _clock.GetUtcNow();
        Change[] changes = [ValueOf(holder, kept, value, now), ValueOf(holder, dropped, value: null, now)];
        return new FieldPlan(FieldProblem.None, Error: null, changes);
    }

    private FieldPlan Write(FieldHolder holder, NodeField field, string? value, DatasetFile? file = null) =>
        field.Value == value && field.Image?.File == file && field.Values is not { IsConflict: true }
            ? FieldPlan.Refused(FieldProblem.Unchanged)
            : new FieldPlan(FieldProblem.None, Error: null, [ValueOf(holder, field, value, _clock.GetUtcNow(), file)]);

    private static FieldSet ValueOf(FieldHolder holder, NodeField field, string? value, DateTimeOffset at, DatasetFile? file = null) =>
        new(Guid.NewGuid(), at, holder, field.Owner, field.Field, field.Name, field.Kind, value, field.Current, file);
}
