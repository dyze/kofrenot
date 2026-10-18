using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Datasets;

/// <summary>A version of a dataset shown in a conflict; <see cref="IsAvailable"/> is false while its file is not synced.</summary>
public sealed record DatasetChoice(DatasetEdited Version, bool IsAvailable);

/// <summary>
/// A dataset as loaded in its details. <see cref="Latest"/> is its latest current version, null
/// before the first import; <see cref="IsMissing"/> while that version's file is not synced yet
/// (it cannot be exported then). <see cref="Conflict"/> lists the current versions when files
/// were imported on two devices before they synced.
/// </summary>
public sealed record DatasetState(IReadOnlyList<DatasetEdited> Versions, bool IsMissing, IReadOnlyList<DatasetChoice> Conflict)
{
    public DatasetEdited? Latest => Versions.Count == 0 ? null : Versions[^1];
}

/// <summary>
/// Loads a dataset's versions (the metadata only: the files are read when exported), and turns an
/// import, or a version kept from a conflict, into the version that saves it. A version replaces
/// every version loaded, so it resolves a conflict; a version imported meanwhile elsewhere stays,
/// as a new conflict.
/// </summary>
public sealed class DatasetPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="exists">Whether a version's file is in the vault.</param>
    public DatasetState Load(Node node, Func<Guid, bool> exists)
    {
        var versions = graph.DatasetVersionsOf(node.Id);
        IReadOnlyList<DatasetChoice> conflict = versions.Count > 1
            ? versions.Select(version => new DatasetChoice(version, exists(version.Id))).ToList()
            : [];
        return new DatasetState(versions, versions.Count > 0 && !exists(versions[^1].Id), conflict);
    }

    /// <summary>
    /// The version importing <paramref name="file"/>, or null when there is nothing to save: it is
    /// the latest file already, and there is no conflict to resolve.
    /// </summary>
    public DatasetEdited? Import(Node node, DatasetFile file, DatasetState loaded) =>
        file == loaded.Latest?.File && loaded.Conflict.Count == 0 ? null : NewVersion(node, file, loaded);

    /// <summary>
    /// The version keeping <paramref name="kept"/>, one of a conflict's: the same file, saved again
    /// (its content copied) over every version loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not one of the versions loaded, or its file is not synced yet.</exception>
    public DatasetEdited Keep(Node node, DatasetEdited kept, DatasetState loaded) =>
        loaded.Conflict.Any(choice => choice.Version.Id == kept.Id && choice.IsAvailable)
            ? NewVersion(node, kept.File, loaded)
            : throw new InvalidOperationException("Only an available version of the conflict can be kept.");

    /// <summary>The first version of a new dataset, created with <paramref name="file"/>.</summary>
    public static DatasetEdited First(Guid node, DatasetFile file, DateTimeOffset at) => new(Guid.NewGuid(), at, node, file, []);

    private DatasetEdited NewVersion(Node node, DatasetFile file, DatasetState loaded) =>
        new(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, file, loaded.Versions.Select(version => version.Id).ToList());
}
