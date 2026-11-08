using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A query view's settings (<see cref="QueryDraft"/>): the query, typed with the input box's proposals, what stops it
/// under it, and a box per link phrase, checked for the columns its rows show.
/// </summary>
public partial class ViewDefinitionEditor
{
    private readonly Dictionary<ViewColumn, CheckBox> _columnBoxes = [];

    private UIElement[] QueryRows => [QueryLabel, QueryPanel, ColumnsLabel, ColumnsPanel];

    /// <summary>A box per link phrase, each way, named as a row reads it ("written by").</summary>
    private void SetupQuery()
    {
        QueryBox.GraphOf = () => _graph;
        QueryBox.Changed += QueryBox_Changed;
        ColumnBoxes.Children.Clear();
        _columnBoxes.Clear();
        foreach (var column in QueryDraft.ColumnChoices(_graph))
        {
            var box = new CheckBox { Content = QueryDraft.NameOf(column, _graph), Tag = column, Margin = new Thickness(0, 0, 12, 4) };
            box.Click += ColumnBox_Click;
            ColumnBoxes.Children.Add(box);
            _columnBoxes[column] = box;
        }
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

        QueryProblem.Text = Draft.Query.StatusIn(_graph);
        foreach (var (column, box) in _columnBoxes)
        {
            box.IsChecked = Draft.Query.Columns.Contains(column);
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
