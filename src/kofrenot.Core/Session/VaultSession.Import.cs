using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>Files imported as new datasets, once their preview is accepted (<see cref="FileImportPlanner"/>).</summary>
public sealed partial class VaultSession
{
    /// <summary>
    /// Adds the datasets of <paramref name="preview"/>: the nodes and their links at once, then
    /// each file in turn, described and sealed off the UI thread. A file that cannot be read leaves
    /// its dataset empty (the user is told). How many files were imported.
    /// </summary>
    public async Task<int> ImportFiles(ISessionQuestions ask, ImportPreview preview)
    {
        if (Current is not { } vault || preview.Count == 0)
        {
            return 0;
        }

        var plan = new FileImportPlanner(vault.Graph, _time).Plan(preview);
        var under = preview.Under is null ? string.Empty : ", in a tree";
        if (!Append(ask, vault, plan.Changes, $"Files imported as datasets ({preview.Count} files, {preview.TotalSize} bytes{under})", isContent: true))
        {
            return 0;
        }

        var imported = 0;
        foreach (var file in plan.Files)
        {
            if (await Describe(ask, file.File) is { } described
                && await SaveDataset(ask, DatasetPlanner.First(file.Node.Id, described, _time.GetUtcNow()), file.File.Open))
            {
                imported++;
            }
        }

        return imported;
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
