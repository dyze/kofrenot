using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Imports;
using Kofrenot.UI.Platform;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>A choice of the match box: a field of the type, or none to find nodes by their names only.</summary>
public sealed record MatchChoice(FieldDefinition? Field)
{
    public override string ToString() => Field?.Name ?? "None: by the names only";
}

/// <summary>
/// Edits a mapping's name and definition (<see cref="MappingDefinition"/>). A sample file offers its
/// keys, suggests the field rules by name (<see cref="MappingPlanner.Suggest"/>) and shows its first
/// records as they would be imported (<see cref="ImportPlanner"/>), nothing written.
/// </summary>
public partial class MappingDialog : Window
{
    private const int SampleSize = 10;

    private readonly KnowledgeGraph _graph;
    private readonly IReadOnlyList<NodeType> _linkedTypes;
    private readonly IReadOnlyList<LinkVerb> _verbs;
    private List<FieldRuleRow> _fields = [];
    private List<LinkRuleRow> _links = [];
    private RecordFile? _sample;

    // The definition the rows are first built from, while the type is first selected.
    private MappingDefinition? _definition;
    private MappingEdit? _saved;

    private MappingDialog(Window owner, KnowledgeGraph graph, string? name, MappingDefinition? definition)
    {
        InitializeComponent();
        Owner = owner;
        _graph = graph;
        _linkedTypes = graph.Types.Chosen.Where(type => !type.HasContent).ToList();
        _verbs = graph.Types.Verbs;
        HeadingText.Text = name is null ? "New mapping" : $"Mapping “{name}”";
        NameBox.Text = name ?? string.Empty;
        TypeBox.ItemsSource = graph.NodeTypes.All.Select(custom => custom.Type).ToList();
        if (definition is not null)
        {
            NameKeysBox.Text = string.Join(", ", definition.NameKeys);
            _links = [.. definition.Links.Select(rule => new LinkRuleRow(
                rule.Key,
                _linkedTypes.FirstOrDefault(type => type.Id == rule.NodeType),
                _verbs.FirstOrDefault(verb => verb.Type.Id == rule.LinkType && verb.IsReverse == rule.IsReverse),
                [],
                _linkedTypes,
                _verbs))];
            LinkRows.ItemsSource = _links;
        }

        // Selecting the type builds its rows: from the definition when it is its type.
        _definition = definition;
        TypeBox.SelectedItem = definition is null ? null : graph.Types.FindNodeType(definition.NodeType);
        _definition = null;
        ShowFields();
    }

    /// <summary>The mapping as saved; null when cancelled.</summary>
    public static MappingEdit? Ask(Window owner, KnowledgeGraph graph, string? name, MappingDefinition? definition)
    {
        var dialog = new MappingDialog(owner, graph, name, definition);
        return dialog.ShowDialog() == true ? dialog._saved : null;
    }

    private NodeType? Type => TypeBox.SelectedItem as NodeType;

    private IReadOnlyList<string> SampleKeys => _sample?.Keys ?? [];

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => BuildFields();

    // The type's small fields, with their keys: the definition's, else those suggested by the sample.
    private void BuildFields()
    {
        var type = Type;
        var fields = type?.Fields.Where(field => field.Kind.IsSmall()).ToList() ?? [];
        var suggested = type is null ? [] : MappingPlanner.Suggest(type, SampleKeys);
        _fields = fields.Select(field => new FieldRuleRow(
            field,
            _definition?.Fields.FirstOrDefault(rule => rule.Field == field.Id)?.Key ?? suggested.FirstOrDefault(rule => rule.Field == field.Id)?.Key ?? string.Empty,
            _definition?.OtherNames.Contains(field.Id) == true,
            SampleKeys)).ToList();
        MatchBox.ItemsSource = fields.Select(field => new MatchChoice(field)).Prepend(new MatchChoice(null)).ToList();
        MatchBox.SelectedItem = new MatchChoice(fields.FirstOrDefault(field => field.Id == _definition?.Match));
        ShowFields();
    }

    private void ShowFields()
    {
        FieldRows.ItemsSource = _fields;
        NoFieldText.Visibility = Type is not null && _fields.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose a sample file", Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(dialog.FileName);
            _sample = JsonRecordReader.Read(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialog.Inform(this, $"“{Path.GetFileName(dialog.FileName)}” cannot be read: {ex.Message}");
            return;
        }

        SampleText.Text = $"{Path.GetFileName(dialog.FileName)}: {_sample.Records.Count} records, keys {string.Join(", ", _sample.Keys)}";
        var suggested = Type is { } type ? MappingPlanner.Suggest(type, _sample.Keys) : [];
        _fields = _fields.Select(row => new FieldRuleRow(
            row.Field,
            row.Key.Length > 0 ? row.Key : suggested.FirstOrDefault(rule => rule.Field == row.Field.Id)?.Key ?? string.Empty,
            row.IsOtherName,
            _sample.Keys)).ToList();
        _links = _links.Select(row => new LinkRuleRow(row.Key, row.NodeType, row.Verb, _sample.Keys, _linkedTypes, _verbs)).ToList();
        ShowFields();
        LinkRows.ItemsSource = _links;
        TryButton.IsEnabled = true;
        Try();
    }

    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        _links = [.. _links, new LinkRuleRow(string.Empty, null, null, SampleKeys, _linkedTypes, _verbs)];
        LinkRows.ItemsSource = _links;
    }

    private void RemoveLink_Click(object sender, RoutedEventArgs e)
    {
        _links = _links.Where(row => row != ((FrameworkElement)sender).Tag).ToList();
        LinkRows.ItemsSource = _links;
    }

    private void Try_Click(object sender, RoutedEventArgs e) => Try();

    // The sample's first records, planned with the definition as it stands.
    private void Try()
    {
        if (_sample is null)
        {
            return;
        }

        if (Built(out var error) is not { } definition)
        {
            TryText.Text = error;
            return;
        }

        var plan = new ImportPlanner(_graph).Plan(definition, _sample with { Records = _sample.Records.Take(SampleSize).ToList() });
        TryText.Text = plan.Refusal ?? string.Join("\n", plan.Warnings.Concat(plan.Items.Select(RecordImportView.Summary)));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var definition = Built(out var error);
        error = name.Length == 0 ? "Name the mapping." : error;
        if (definition is null || name.Length == 0)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        _saved = new MappingEdit(name, definition);
        DialogResult = true;
    }

    // The definition as edited, or null with why it is not complete.
    private MappingDefinition? Built(out string? error)
    {
        var nameKeys = NameKeysBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        var fields = _fields.Where(row => row.Key.Trim().Length > 0).ToList();
        var match = (MatchBox.SelectedItem as MatchChoice)?.Field;
        var links = _links.Select(row => row.ToRule()).ToList();
        error = Type is null ? "Choose the type the records become."
            : nameKeys.Count == 0 ? "Give at least one key for the name."
            : match is not null && fields.All(row => row.Field != match) ? $"Give {match.Name} a key, or match by the names only."
            : links.Contains(null) ? "Complete each link, or remove it."
            : null;
        return error is null
            ? new MappingDefinition(
                MappingDefinition.Json,
                Type!.Id,
                nameKeys,
                match?.Id,
                [.. fields.Where(row => row.IsOtherName).Select(row => row.Field.Id)],
                [.. fields.Select(row => new FieldRule(row.Key.Trim(), row.Field.Id))],
                links!)
            : null;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
