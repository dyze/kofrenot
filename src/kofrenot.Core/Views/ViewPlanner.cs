using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// A view as loaded to show or edit it: its current versions, and the definition shown (the latest
/// one's; null before the first). Several versions are a conflict: edited on two devices before
/// they synced.
/// </summary>
public sealed record ViewState(IReadOnlyList<ViewEdited> Versions, ViewDefinition? Definition)
{
    public bool IsConflict => Versions.Count > 1;
}

/// <summary>
/// Loads a view's definition, and turns an edit into the version that saves it. A version replaces
/// every version loaded, so saving resolves a conflict; one saved meanwhile elsewhere stays, as a
/// new conflict.
/// </summary>
public sealed class ViewPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public ViewState Load(Node view)
    {
        var versions = graph.ViewVersionsOf(view.Id);
        return new ViewState(versions, versions is [.., var latest] ? latest.Definition : null);
    }

    /// <summary>The version saving <paramref name="definition"/>, or null when it is the one loaded and there is no conflict.</summary>
    public ViewEdited? Save(Node view, ViewDefinition definition, ViewState loaded) =>
        definition == loaded.Definition && !loaded.IsConflict
            ? null
            : new ViewEdited(Guid.NewGuid(), _clock.GetUtcNow(), view.Id, definition, loaded.Versions.Select(version => version.Id).ToList());
}
