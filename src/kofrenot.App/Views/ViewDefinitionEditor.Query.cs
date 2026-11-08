using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A query view's settings (<see cref="QueryDraft"/>): the query, typed with the input box's proposals, what stops it
/// under it, and its columns to check, in groups: Links, Fields, Qualifiers of the query's link. The groups follow the query.
/// </summary>
public partial class ViewDefinitionEditor
{
    private readonly Dictionary<ViewColumn, CheckBox> _columnBoxes = [];
    private IReadOnlyList<ColumnChoice> _columnChoices = [];
    private string _qualifiersHeading = string.Empty;

    private UIElement[] QueryRows => [QueryLabel, QueryPanel, ColumnsLabel, ColumnsPanel];

    private void SetupQuery()
    {
        QueryBox.GraphOf = () => _graph;
        QueryBox.Changed += QueryBox_Changed;
        _columnChoices = [];
    }

    private void LoadQuery(QueryDraft query)
    {
        QueryBox.Clear();
        QueryBox.Replace(query.Text);
    }

    private void ShowQuery(bool isQuery)
    {
        foreach (var row in QueryRows)
        {
            row.Visibility = isQuery ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!isQuery)
        {
            return;
        }

        QueryProblem.Text = Draft.Query.StatusIn(_graph);
        var choices = Draft.Query.ColumnChoices(_graph);
        if (!choices.SequenceEqual(_columnChoices) || Draft.Query.QualifiersHeading(_graph) != _qualifiersHeading)
        {
            FillColumnGroups(choices);
        }

        foreach (var (column, box) in _columnBoxes)
        {
            box.IsChecked = Draft.Query.Columns.Contains(column);
        }
    }

    // A group per kind of column, each a box per choice; a group with none is left out.
    private void FillColumnGroups(IReadOnlyList<ColumnChoice> choices)
    {
        _columnChoices = choices;
        _qualifiersHeading = Draft.Query.QualifiersHeading(_graph);
        ColumnGroups.Children.Clear();
        _columnBoxes.Clear();
        AddColumnGroup("Links", choices.Where(choice => choice.Column is LinkColumn));
        AddColumnGroup("Fields", choices.Where(choice => choice.Column is FieldColumn));
        AddColumnGroup(_qualifiersHeading, choices.Where(choice => choice.Column is QualifierColumn));
    }

    private void AddColumnGroup(string heading, IEnumerable<ColumnChoice> choices)
    {
        var boxes = new WrapPanel();
        foreach (var choice in choices)
        {
            var box = new CheckBox { Content = choice.Name, Tag = choice.Column, Margin = new Thickness(0, 0, 12, 4) };
            box.Click += ColumnBox_Click;
            boxes.Children.Add(box);
            _columnBoxes[choice.Column] = box;
        }

        if (boxes.Children.Count > 0)
        {
            ColumnGroups.Children.Add(new TextBlock { Text = heading, Opacity = 0.7, Margin = new Thickness(0, ColumnGroups.Children.Count == 0 ? 0 : 6, 0, 4) });
            ColumnGroups.Children.Add(boxes);
        }
    }

    private void QueryBox_Changed() => Change(view => view with { Query = view.Query.WithText(QueryBox.Input.Text, _graph) });

    // Tab or Enter accepts the proposal, Up/Down move in the proposal list.
    private void QueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = e.Key switch
        {
            Key.Tab => QueryBox.AcceptProposal(),
            Key.Up or Key.Down when QueryBox.HasProposalList => QueryBox.MoveProposal(e.Key == Key.Up ? -1 : 1),
            Key.Enter when QueryBox.HasProposalList => QueryBox.AcceptProposal(),
            _ => false,
        };
    }

    private void ColumnBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: ViewColumn column } box)
        {
            Change(view => view with { Query = view.Query.WithColumn(column, box.IsChecked == true) });
        }
    }
}
