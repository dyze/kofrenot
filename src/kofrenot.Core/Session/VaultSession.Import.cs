using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>Files imported: as new datasets, once their preview is accepted (<see cref="FileImportPlanner"/>); as a dataset's new version; as an image field's image.</summary>
public sealed partial class VaultSession
{
    /// <summary>
    /// Writes <paramref name="preview"/>: its new datasets and folder nodes with their links at once,
    /// then each file in turn, described and sealed off the UI thread: a new dataset's first version,
    /// an existing one's new version (none when it is its latest file already). A file that cannot be
    /// read leaves its dataset as it was (the user is told). The datasets whose file was imported.
    /// </summary>
    public async Task<IReadOnlyList<Node>> ImportFiles(ISessionQuestions ask, ImportPreview preview)
    {
        if (Current is not { } vault || preview.Count == 0)
        {
            return [];
        }

        var plan = new FileImportPlanner(vault.Graph, _time).Plan(preview);
        var under = preview.Under is null ? string.Empty : ", in a tree";
        var counts = $"{preview.FileCount} files, {preview.FolderCount} folders, {preview.UpdateCount} updates, {preview.TotalSize} bytes{under}";
        if (!Append(ask, vault, plan.Changes, $"Files imported as datasets ({counts})"))
        {
            return [];
        }

        var imported = new List<Node>();
        foreach (var file in plan.Files)
        {
            if (await Describe(ask, file.File) is { } described
                && await SaveDataset(ask, DatasetPlanner.First(file.Node.Id, described, _time.GetUtcNow()), file.File.Open))
            {
                imported.Add(file.Node);
            }
        }

        var unchanged = 0;
        var planner = new DatasetPlanner(vault.Graph, _time);
        foreach (var update in plan.Updates)
        {
            if (await Describe(ask, update.File) is not { } described)
            {
                continue;
            }

            if (planner.Import(update.Node, described, planner.Load(update.Node, _ => true)) is not { } version)
            {
                unchanged++;
            }
            else if (await SaveDataset(ask, version, update.File.Open))
            {
                imported.Add(update.Node);
            }
        }

        if (unchanged > 0)
        {
            _log.Info("Action", $"Dataset import: {unchanged} files the same as their dataset's, nothing imported for them");
        }

        return imported;
    }

    /// <summary>
    /// Imports <paramref name="file"/> as the new version of <paramref name="dataset"/>'s file, as Import… does
    /// on Windows, described off the UI thread. False when it cannot be read or saved (the user is
    /// told), and when it is the latest file already (nothing to import: the user is told too).
    /// </summary>
    public async Task<bool> ImportVersion(ISessionQuestions ask, Node dataset, ImportFile file)
    {
        if (Current is not { } vault || await Describe(ask, file) is not { } described)
        {
            return false;
        }

        var planner = new DatasetPlanner(vault.Graph, _time);
        if (planner.Import(dataset, described, planner.Load(dataset, _ => true)) is not { } version)
        {
            _log.Info("Action", "Dataset import: the same file, nothing imported");
            ask.Tell("This file is the dataset's already: nothing imported.");
            return false;
        }

        return await SaveDataset(ask, version, file.Open);
    }

    /// <summary>
    /// Sets <paramref name="file"/> as the image of <paramref name="field"/>, as Choose… does on Windows,
    /// described off the UI thread. False when it cannot be read or saved, or is not an image
    /// kofrenot shows (the user is told why).
    /// </summary>
    public async Task<bool> SetImage(ISessionQuestions ask, FieldHolder holder, NodeField field, ImportFile file)
    {
        if (Current is not { } vault || await Describe(ask, file) is not { } described)
        {
            return false;
        }

        var plan = new FieldPlanner(vault.Graph, _time).SetImage(holder, field, described);
        if (plan.Changes is not [FieldSet value])
        {
            ask.Tell(plan.Status);
            return false;
        }

        return await SaveImage(ask, value, file.Open);
    }

    private async Task<DatasetFile?> Describe(ISessionQuestions ask, ImportFile file)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var content = file.Open();
                return DatasetFiles.Describe(file.Name, content);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The message names the file: shown, not logged.
            _log.Warn("Action", $"Cannot read a file to import ({ex.GetType().Name})");
            ask.Tell($"Cannot import the file.\n\n{ex.Message}");
            return null;
        }
    }
}
