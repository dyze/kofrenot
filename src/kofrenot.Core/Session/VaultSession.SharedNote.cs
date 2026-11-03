using Kofrenot.Core.Graph;
using Kofrenot.Core.Notes;

namespace Kofrenot.Core.Session;

/// <summary>Text shared by another app, added as a new note once its preview is accepted (<see cref="SharedNotePlanner"/>).</summary>
public sealed partial class VaultSession
{
    /// <summary>Adds the note: its node, its link under its destination, and its text. The node; null when it could not be written.</summary>
    public Node? AddNote(ISessionQuestions ask, SharedNote note)
    {
        if (Current is not { } vault)
        {
            return null;
        }

        var plan = new SharedNotePlanner(vault.Graph, _time).Plan(note);
        var details = $"{(note.IsHomonym ? ", a duplicate" : string.Empty)}{(note.Under is null ? string.Empty : ", in a tree")}";
        return Append(ask, vault, plan.Changes, $"Note added from shared text{details}", [plan.Note])
            ? plan.Node
            : null;
    }
}
