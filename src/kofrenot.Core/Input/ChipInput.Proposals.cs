using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The proposals of the text typed at its end, at most <see cref="MaxProposals"/>: the first one is selected
/// until another is chosen (<see cref="MoveProposal"/>); Tab accepts the one selected, Esc hides them.
/// </summary>
public sealed partial class ChipInput
{
    /// <summary>The most proposals offered at once.</summary>
    public const int MaxProposals = 5;

    private int _proposalIndex;

    /// <summary>What Tab may add, the first one first; none after an edit that did not add text at the end.</summary>
    public IReadOnlyList<Proposal> Proposals(KnowledgeGraph graph) =>
        _typedAtEnd ? [.. Candidates(graph).Take(MaxProposals)] : [];

    /// <summary>The index of the proposal selected among <see cref="Proposals"/>.</summary>
    public int ProposalIndex(KnowledgeGraph graph) => Math.Min(_proposalIndex, Math.Max(Proposals(graph).Count - 1, 0));

    /// <summary>What Tab would add: the proposal selected, shown in grey after the text; null when there is none.</summary>
    public Proposal? Proposal(KnowledgeGraph graph)
    {
        var proposals = Proposals(graph);
        return proposals.Count == 0 ? null : proposals[Math.Min(_proposalIndex, proposals.Count - 1)];
    }

    /// <summary>Up/Down: the previous or next proposal selected, around from the last to the first. False with fewer than two.</summary>
    public bool MoveProposal(KnowledgeGraph graph, int step)
    {
        var count = Proposals(graph).Count;
        if (count < 2)
        {
            return false;
        }

        _proposalIndex = ((Math.Min(_proposalIndex, count - 1) + step) % count + count) % count;
        return true;
    }

    /// <summary>Tab: the proposal selected becomes a label, replacing the side it completes. False when there is none.</summary>
    public bool Accept(KnowledgeGraph graph)
    {
        if (Proposal(graph) is not { } proposal)
        {
            return false;
        }

        if (proposal.IsQueryText)
        {
            // Text added: the next word is proposed at once.
            Tail += proposal.Rest;
            return true;
        }

        if (proposal.IsQualifier)
        {
            StartQualifier(Tail[..Tail.LastIndexOf(',')], proposal.Label);
        }
        else if (proposal.IsLinkType)
        {
            _left = string.Join(' ', InputParser.Words(Text)[..proposal.WordsBefore]);
            _linkType = proposal.Label;
        }
        else if (_linkType is null)
        {
            _left = proposal.Label;
        }
        else
        {
            _right = proposal.Label;
        }

        Tail = string.Empty;
        _typedAtEnd = false;
        return true;
    }

    /// <summary>Esc: hides the proposals until more is typed. False when there were none.</summary>
    public bool Dismiss(KnowledgeGraph graph)
    {
        if (Proposal(graph) is null)
        {
            return false;
        }

        _typedAtEnd = false;
        return true;
    }

    private IEnumerable<Proposal> Candidates(KnowledgeGraph graph)
    {
        if (IsQueryText)
        {
            return QueryCompletion.Proposals(graph, Tail);
        }

        if (_linkType is null)
        {
            return _fixedLeft is { } left
                ? InputCompletion.LinkTypeProposalsAfter(graph.Types, left, Tail)
                : InputCompletion.Proposals(graph, Text, linkTypeTyped: false);
        }

        var typed = string.Join(' ', InputParser.Words(Tail[(Tail.LastIndexOf(',') + 1)..]));
        var qualifiers = QualifierText.Proposed(Tail, QualifiersOf(graph), nodeTyped: _right is not null)
            .Select(qualifier => new Proposal(qualifier.Name[typed.Length..], qualifier.Name, IsLinkType: false, IsQualifier: true))
            .ToList();
        if (qualifiers.Count > 0)
        {
            return qualifiers;
        }

        return _qualifiers.Count > 0 || Tail.Contains(',') ? [] : InputCompletion.Proposals(graph, _right is null ? Tail : $"{_right} {Tail}", linkTypeTyped: true);
    }
}
