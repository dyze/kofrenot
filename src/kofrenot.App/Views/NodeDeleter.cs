using System.Windows;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>Deletes one node with its links (<see cref="DeletionPlanner"/>), asked first: from its details, a list or the search.</summary>
public static class NodeDeleter
{
    /// <summary>False when cancelled or not written.</summary>
    public static bool Delete(Window? owner, KnowledgeGraph graph, Node node, Func<Change, string, bool> write, ILog log)
    {
        var deletion = new DeletionPlanner(graph).Node(node);
        if (!MessageDialog.Confirm(owner, deletion.Question, "Delete"))
        {
            log.Info("Action", "Node deletion cancelled");
            return false;
        }

        return write(deletion.Change, "Node deleted");
    }
}
