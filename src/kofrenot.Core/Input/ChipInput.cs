using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The input box as labels, then the text still typed (<see cref="Tail"/>). The first node and the
/// link type become labels once a link type is recognized and a space typed after it; a proposal
/// (<see cref="InputCompletion"/>) becomes one when accepted. Backspace with no text after them turns
/// the last label back into text. <see cref="Text"/> is read as before (<see cref="InputParser"/>):
/// labels change how it shows, not what it means.
/// </summary>
public sealed class ChipInput
{
    private string? _left;
    private string? _linkType;
    private string? _right;
    private bool _typedAtEnd;

    /// <summary>The text after the labels, as typed.</summary>
    public string Tail { get; private set; } = string.Empty;

    public bool IsEmpty => _left is null && Tail.Length == 0;

    /// <summary>The labels and the text after them, as one text.</summary>
    public string Text => string.Join(' ', Labels.Append(Tail));

    private IEnumerable<string> Labels => new[] { _left, _linkType, _right }.OfType<string>();

    /// <summary>
    /// The text after the labels, typed or edited. True when labels formed: the text after them is
    /// then <see cref="Tail"/>. Labels form, and a proposal shows, only for text added at its end.
    /// The same text again changes nothing: a box may tell of one change several times (Avalonia's, later).
    /// </summary>
    public bool Type(KnowledgeGraph graph, string tail)
    {
        if (tail == Tail)
        {
            return false;
        }

        _typedAtEnd = tail.Length > Tail.Length && tail.StartsWith(Tail, StringComparison.Ordinal);
        Tail = tail;
        return _typedAtEnd && _linkType is null && Recognize(graph.Types);
    }

    /// <summary>Backspace with no text after the labels: the last one becomes text again. False when there is text, or no label.</summary>
    public bool Backspace()
    {
        if (Tail.Length > 0 || (_right ?? _linkType ?? _left) is not { } last)
        {
            return false;
        }

        if (_right is not null)
        {
            _right = null;
        }
        else if (_linkType is not null)
        {
            _linkType = null;
        }
        else
        {
            _left = null;
        }

        Tail = last;
        _typedAtEnd = false;
        return true;
    }

    /// <summary>What Tab would add, shown in grey after the text; null after an edit that did not add text at the end.</summary>
    public Proposal? Proposal(KnowledgeGraph graph)
    {
        if (!_typedAtEnd)
        {
            return null;
        }

        return _linkType is null
            ? InputCompletion.Propose(graph, Text, linkTypeTyped: false)
            : InputCompletion.Propose(graph, _right is null ? Tail : $"{_right} {Tail}", linkTypeTyped: true);
    }

    /// <summary>Tab: the proposal becomes a label, replacing the side it completes. False when there is none.</summary>
    public bool Accept(KnowledgeGraph graph)
    {
        if (Proposal(graph) is not { } proposal)
        {
            return false;
        }

        if (proposal.IsLinkType)
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

    /// <summary>Esc: hides the proposal until more is typed. False when there was none.</summary>
    public bool Dismiss(KnowledgeGraph graph)
    {
        if (Proposal(graph) is null)
        {
            return false;
        }

        _typedAtEnd = false;
        return true;
    }

    /// <summary>All the labels become text again, as one text (the window shown again, its text selected).</summary>
    public void Unlabel()
    {
        Tail = Text;
        _left = _linkType = _right = null;
        _typedAtEnd = false;
    }

    public void Clear()
    {
        _left = _linkType = _right = null;
        Tail = string.Empty;
        _typedAtEnd = false;
    }

    /// <summary>The labels, as shown; a node's pictures the node the plan uses, or else the oldest of its name.</summary>
    public IReadOnlyList<InputChip> Chips(KnowledgeGraph graph, AddPlan? plan)
    {
        var link = plan as LinkPlan;
        var chips = new List<InputChip>();
        if (_left is not null)
        {
            chips.Add(NodeChip(graph, _left, link?.Left));
        }

        if (_linkType is not null)
        {
            chips.Add(new InputChip(_linkType, IsLinkType: true));
        }

        if (_right is not null)
        {
            chips.Add(NodeChip(graph, _right, link?.Right));
        }

        return chips;
    }

    /// <summary>Enter: everything becomes labels, as read (a node, or both nodes and the link type), no text left after them.</summary>
    public void LabelAll(TypeCatalog types)
    {
        var text = Text;
        switch (InputParser.Parse(text, types))
        {
            case NodeRequest node:
                (_left, _linkType, _right) = (node.Name, null, null);
                break;
            case LinkRequest link:
                (_left, _linkType, _right) = (link.Left, PhraseOf(InputParser.Words(text), link), link.Right.Length > 0 ? link.Right : null);
                break;
            default:
                return;
        }

        Tail = string.Empty;
        _typedAtEnd = false;
    }

    // The first node and the link type, as typed, become labels; what follows stays text.
    private bool Recognize(TypeCatalog types)
    {
        var text = Text;
        if (InputParser.Parse(text, types) is not LinkRequest link || (link.Right.Length == 0 && !char.IsWhiteSpace(text[^1])))
        {
            return false;
        }

        var words = InputParser.Words(text);
        _left = link.Left;
        _linkType = PhraseOf(words, link);
        Tail = After(text, words.Length - InputParser.Words(link.Right).Length);
        return true;
    }

    // The link type's words, as typed between both nodes.
    private static string PhraseOf(string[] words, LinkRequest link) =>
        string.Join(' ', words[InputParser.Words(link.Left).Length..(words.Length - InputParser.Words(link.Right).Length)]);

    // The text after its first words, without the spaces before it.
    private static string After(string text, int words)
    {
        var index = 0;
        for (var word = 0; word < words; word++)
        {
            while (char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        return text[index..].TrimStart();
    }

    private static InputChip NodeChip(KnowledgeGraph graph, string name, PlannedSide? side)
    {
        var nodes = graph.NodesNamed(name);
        if (nodes.Count == 0)
        {
            return new InputChip(name, IsLinkType: false, IsNew: true);
        }

        var shown = side is { Chosen: { } chosen } && NameKey.Of(side.Name) == NameKey.Of(name) ? chosen : nodes[0];
        return new InputChip(name, IsLinkType: false, Homonyms: nodes.Count, Visual: NodeVisual.Of(graph, shown));
    }
}
