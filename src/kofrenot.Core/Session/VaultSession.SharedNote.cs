using Kofrenot.Core.Graph;
using Kofrenot.Core.Notes;

namespace Kofrenot.Core.Session;

/// <summary>Text shared by another app, added as a new note once its preview is accepted (<see cref="SharedNotePlanner"/>).</summary>
public sealed partial class VaultSession
{
    /// <summary>Adds the note: its node and its text, content a client that edits content only writes. The node; null when it could not be written.</summary>
    public Node? AddNote(ISessionQuestions ask, SharedNote note)
    {
        if (Current is not { } vault)
        {
            return null;
        }

        var plan = new SharedNotePlanner(vault.Graph, _time).Plan(note);
        return Append(ask, vault, [plan.Node], $"Note added from shared text{(note.IsHomonym ? ", a duplicate" : string.Empty)}", [plan.Note], isContent: true)
            ? plan.Node.Node
            : null;
    }
}
