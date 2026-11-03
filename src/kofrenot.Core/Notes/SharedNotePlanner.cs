using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Notes;

/// <summary>
/// A note made from text shared by another app, as previewed: its name, its text, whether the name
/// is taken (under <see cref="Under"/> when given, else in the vault), and the node it goes under.
/// </summary>
public sealed record SharedNote(string Name, string Text, bool IsHomonym, ParentLink? Under = null);

/// <summary>A shared note accepted: the changes adding its node (and its link under its parent), and its first version.</summary>
public sealed record SharedNotePlan(IReadOnlyList<Change> Changes, Node Node, NoteToSave Note);

/// <summary>
/// Turns text shared by another app (a message, a link) into a new note: named after its first
/// line, holding the whole text, linked under the import destination when there is one. The
/// preview says what will be added; nothing is before <see cref="Plan"/>.
/// </summary>
public sealed class SharedNotePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    public const int MaxName = 80;
    public const string DefaultName = "Shared note";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public SharedNote Preview(string text, ParentLink? under = null)
    {
        var name = NameOf(text);
        var taken = under is null
            ? graph.NodesNamed(name).Count > 0
            : new TreeLinks(graph, under.LinkType).ChildrenOf(under.Parent.Id).Any(child => NameKey.Of(child.Name) == NameKey.Of(name));
        return new SharedNote(name, text, taken, under);
    }

    /// <summary>The note's node, its link, and its first version (new ids at each call: call it once, when accepted).</summary>
    public SharedNotePlan Plan(SharedNote note)
    {
        var now = _clock.GetUtcNow();
        var node = new Node(Guid.NewGuid(), TypeCatalog.Note.Id, note.Name, now);
        List<Change> changes = [new NodeAdded(Guid.NewGuid(), now, node)];
        if (note.Under is { } under)
        {
            changes.Add(new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), under.LinkType, under.Parent.Id, node.Id, now)));
        }

        return new SharedNotePlan(changes, node, new NoteToSave(new NoteEdited(Guid.NewGuid(), now, node.Id, []), note.Text));
    }

    /// <summary>The first line with text, without a heading's '#', cut to <see cref="MaxName"/> characters; <see cref="DefaultName"/> when there is none.</summary>
    public static string NameOf(string text)
    {
        var line = text.Split('\n').Select(line => line.Trim().TrimStart('#').Trim()).FirstOrDefault(line => line.Length > 0);
        return line is null ? DefaultName
            : line.Length > MaxName ? line[..(MaxName - 1)].TrimEnd() + "…"
            : line;
    }
}
