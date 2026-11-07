using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A proposal in the list: the rest it adds, whether it is the one selected, the height of a line of text.</summary>
public sealed record ProposalRow(string Rest, bool IsSelected, double Height);

/// <summary>
/// Several proposals: a list from the caret, the one selected on the line of the text (where the grey one would be),
/// the others above and below it. Up/Down select another (<see cref="MoveProposal"/>); a click accepts one.
/// </summary>
public partial class ChipBox
{
    // The list's own spacing above its rows (ChipBox.xaml): its border, then its padding. It has neither on its left,
    // so the rows start at the caret and never cover the text typed.
    private const double ListBorder = 1;
    private const double ListPadding = 3;

    private IReadOnlyList<Proposal> _proposals = [];
    private int _proposalIndex;

    /// <summary>The list of several proposals is shown: Up/Down, Enter and Esc are its own.</summary>
    public bool HasProposalList => ProposalList.IsOpen;

    /// <summary>Up/Down while the list is shown: the previous or next proposal selected. False when it is not shown.</summary>
    public bool MoveProposal(int step)
    {
        if (!HasProposalList || GraphOf?.Invoke() is not { } graph || !Input.MoveProposal(graph, step))
        {
            return false;
        }

        ShowProposals(graph);
        return true;
    }

    private void ShowProposals(KnowledgeGraph? graph)
    {
        _proposals = graph is null ? [] : Input.Proposals(graph);
        _proposalIndex = graph is null ? 0 : Input.ProposalIndex(graph);
        ProposalText.Text = _proposals.Count == 0 ? string.Empty : _proposals[_proposalIndex].Rest;
        PlaceProposal();
    }

    // The rows from the caret, moved up so the one selected lies on the line of the text.
    private void OpenProposalList(Rect end)
    {
        ProposalItems.ItemsSource = _proposals.Select((proposal, index) => new ProposalRow(proposal.Rest, index == _proposalIndex, end.Height)).ToList();
        ProposalList.HorizontalOffset = end.Right;
        ProposalList.VerticalOffset = end.Top - ListBorder - ListPadding - (_proposalIndex * end.Height);
        ProposalList.IsOpen = true;
    }

    private void Box_KeyboardFocusChanged(object sender, KeyboardFocusChangedEventArgs e) => PlaceProposal();

    private void ProposalRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProposalRow row } || GraphOf?.Invoke() is not { } graph)
        {
            return;
        }

        var index = ProposalItems.Items.IndexOf(row);
        if (index != _proposalIndex)
        {
            Input.MoveProposal(graph, index - _proposalIndex);
        }

        e.Handled = AcceptProposal();
        Box.Focus();
    }
}
