using Kofrenot.Core.History;

namespace Kofrenot.Core.Session;

/// <summary>A version of a node restored from its history (<see cref="RestorePlanner"/>).</summary>
public sealed partial class VaultSession
{
    /// <summary>
    /// Restores a version: a note's text or a dataset's file written again (downloaded first when
    /// it is not here), then the new version. False when no vault is open, this client edits
    /// content only, or it cannot be written (the user is told).
    /// </summary>
    public Task<bool> Restore(ISessionQuestions ask, RestorePlan plan) =>
        WriteWithContents(ask, plan.Contents, plan.Changes, "restore", $"Version restored ({plan.Entry.Kind})");
}
