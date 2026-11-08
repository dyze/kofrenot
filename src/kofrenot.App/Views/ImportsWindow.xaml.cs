using System.IO;
using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Imports;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.UI.Platform;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>A mapping as listed: its name, what it imports, and whether it can be run or changed.</summary>
public sealed record MappingRow(Node Node, string Name, string Description, bool CanImport, bool CanWrite);

/// <summary>
/// The vault's mappings (<see cref="MappingPlanner"/>): added, edited (<see cref="MappingDialog"/>),
/// deleted once confirmed, and run: a JSON file read, its records planned (<see cref="ImportPlanner"/>),
/// shown (<see cref="RecordImportDialog"/>), then written as chosen. Nothing is written in a vault read only.
/// </summary>
public partial class ImportsWindow : Window
{
    private readonly VaultSession _session;
    private readonly ISessionQuestions _ask;
    private readonly ILog _log;

    public ImportsWindow(Window owner, VaultSession session, ISessionQuestions ask, ILog log)
    {
        InitializeComponent();
        Owner = owner;
        _session = session;
        _ask = ask;
        _log = log;
        Refresh();
    }

    private KnowledgeGraph? Graph => _session.Current?.Graph;

    private bool CanWrite => _session.Current is not null && !_session.IsReadOnly;

    private void Refresh()
    {
        var rows = Graph is { } graph
            ? new MappingPlanner(graph).Mappings.Select(node => RowOf(graph, node)).ToList()
            : [];
        MappingList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NewButton.IsEnabled = CanWrite;
    }

    private MappingRow RowOf(KnowledgeGraph graph, Node node)
    {
        var state = new MappingPlanner(graph).Load(node);
        var description = state.Definition is { } definition ? Describe(graph, definition) : "No definition yet: Edit… to choose it";
        if (state.IsConflict)
        {
            description += $" · {state.Versions.Count} versions (edited on two devices): Edit… to keep one";
        }

        return new MappingRow(node, node.Name, description, CanWrite && state.Definition is not null, CanWrite);
    }

    private static string Describe(KnowledgeGraph graph, MappingDefinition definition)
    {
        var type = graph.Types.FindNodeType(definition.NodeType);
        var match = definition.Match is { } field && type?.Fields.FirstOrDefault(candidate => candidate.Id == field) is { } matched
            ? $"match {matched.Name}"
            : "match by name";
        return $"{type?.Name ?? definition.NodeType} · {match}";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (Graph is not { } graph || MappingDialog.Ask(this, graph, name: null, definition: null) is not { } edit)
        {
            _log.Info("Action", "New mapping cancelled");
            return;
        }

        _session.Write(_ask, new MappingPlanner(graph).Add(edit.Name, edit.Definition), "Mapping added");
        Refresh();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var row = RowOfButton(sender);
        if (Graph is not { } graph || graph.FindNode(row.Node.Id) is not { } node)
        {
            return;
        }

        var planner = new MappingPlanner(graph);
        var loaded = planner.Load(node);
        if (MappingDialog.Ask(this, graph, node.Name, loaded.Definition) is not { } edit)
        {
            _log.Info("Action", "Mapping edit cancelled");
            return;
        }

        if (planner.Save(node, edit.Name, edit.Definition, loaded) is { Count: > 0 } changes)
        {
            _session.Write(_ask, changes, loaded.IsConflict ? $"Mapping edited (a conflict of {loaded.Versions.Count} versions resolved)" : "Mapping edited");
        }

        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var row = RowOfButton(sender);
        if (Graph is not { } graph || graph.FindNode(row.Node.Id) is not { } node)
        {
            return;
        }

        if (!MessageDialog.Confirm(this, $"Delete the mapping “{node.Name}”?\n\nThe nodes it imported stay.", "Delete"))
        {
            _log.Info("Action", "Mapping deletion cancelled");
            return;
        }

        _session.Write(_ask, new DeletionPlanner(graph).Node(node).Change, "Mapping deleted");
        Refresh();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var row = RowOfButton(sender);
        if (Graph is not { } graph || graph.FindNode(row.Node.Id) is not { } node || new MappingPlanner(graph).Load(node).Definition is not { } definition)
        {
            return;
        }

        var dialog = new OpenFileDialog { Title = $"Import with “{node.Name}”", Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true || ReadFile(dialog.FileName) is not { } file)
        {
            _log.Info("Action", "Record import cancelled");
            return;
        }

        var plan = new ImportPlanner(graph).Plan(definition, file);
        if (plan.Refusal is { } refusal)
        {
            MessageDialog.Inform(this, refusal);
            return;
        }

        if (RecordImportDialog.Ask(this, node.Name, Path.GetFileName(dialog.FileName), plan) is not { } choose)
        {
            _log.Info("Action", "Record import cancelled");
            return;
        }

        var changes = plan.ChangesFor(choose, DateTimeOffset.UtcNow);
        var (written, added) = (plan.CountFor(choose), plan.AddedFor(choose));
        if (changes.Count > 0 && _session.Write(_ask, changes, $"Records imported ({added} new, {written - added} updated)"))
        {
            MessageDialog.Inform(this, $"{written} written: {added} new, {written - added} updated.");
        }
    }

    // A file's records; null when it cannot be read or is not an array of objects (the user is told).
    private RecordFile? ReadFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonRecordReader.Read(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Warn("Import",$"A record file could not be read: {ex.GetType().Name}");
            MessageDialog.Inform(this, $"“{Path.GetFileName(path)}” cannot be imported: {ex.Message}");
            return null;
        }
    }

    private static MappingRow RowOfButton(object sender) => (MappingRow)((FrameworkElement)sender).Tag;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
