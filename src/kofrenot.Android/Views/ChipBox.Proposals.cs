using Avalonia.Controls;
using Avalonia.Layout;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Several proposals (up to <see cref="ChipInput.MaxProposals"/>), as on Windows: listed under the box, the one
/// Enter accepts in bold and shown in grey after the text; a tap on another accepts it.
/// </summary>
public sealed partial class ChipBox
{
    private void ShowProposals(KnowledgeGraph graph)
    {
        var proposals = Input.Proposals(graph);
        var selected = Input.ProposalIndex(graph);
        ProposalText.Text = proposals.Count == 0 ? null : proposals[selected].Rest;
        ProposalItems.Children.Clear();
        if (proposals.Count > 1)
        {
            for (var index = 0; index < proposals.Count; index++)
            {
                ProposalItems.Children.Add(ProposalRow(graph, proposals[index], index, index == selected));
            }
        }

        ProposalItems.MinWidth = Box.Bounds.Width;
        PlaceProposalLater();
    }

    private Button ProposalRow(KnowledgeGraph graph, Proposal proposal, int index, bool isSelected)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        content.Children.Add(new TextBlock { Text = proposal.Label, VerticalAlignment = VerticalAlignment.Center });
        if (KindOf(proposal) is { } kind)
        {
            content.Children.Add(new TextBlock { Text = kind, Classes = { "caption" }, VerticalAlignment = VerticalAlignment.Center });
        }

        var row = new Button { Content = content, Classes = { "proposal" } };
        if (isSelected)
        {
            row.Classes.Add("selected");
        }

        row.Click += (_, _) =>
        {
            Input.MoveProposal(graph, index - Input.ProposalIndex(graph));
            AcceptProposal();
        };
        return row;
    }

    private static string? KindOf(Proposal proposal) =>
        proposal.IsLinkType ? "link" : proposal.IsQualifier ? "qualifier" : null;
}
