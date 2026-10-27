using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Notes;

/// <summary>A note made from text shared by another app, as previewed: its name, its text, and whether the name is taken.</summary>
public sealed record SharedNote(string Name, string Text, bool IsHomonym);

/// <summary>A shared note accepted: the change adding its node, and its first version.</summary>
public sealed record SharedNotePlan(NodeAdded Node, NoteToSave Note);

/// <summary>
/// Turns text shared by another app (a message, a link) into a new note: named after its first
/// line, holding the whole text. The preview says what will be added; nothing is before <see cref="Plan"/>.
/// </summary>
public sealed class SharedNotePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    public const int MaxName = 80;
    public const string DefaultName = "Shared note";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public SharedNote Preview(string text)
    {
        var name = NameOf(text);
        return new SharedNote(name, text, graph.NodesNamed(name).Count > 0);
    }

    /// <summary>The note's node and first version (new ids at each call: call it once, when accepted).</summary>
    public SharedNotePlan Plan(SharedNote note)
    {
        var now = _clock.GetUtcNow();
        var node = new Node(Guid.NewGuid(), TypeCatalog.Note.Id, note.Name, now);
        return new SharedNotePlan(new NodeAdded(Guid.NewGuid(), now, node), new NoteToSave(new NoteEdited(Guid.NewGuid(), now, node.Id, []), note.Text));
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
