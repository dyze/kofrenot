using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The input box as labels, then the text still typed (<see cref="Tail"/>). The first node and the
/// link type become labels once a link type is recognized and a space typed after it; the second node
/// and a qualifier's name once <c>, </c> and the name of a qualifier of the link type are typed, with a
/// space after it (<see cref="QualifierText"/>); a proposal (<see cref="InputCompletion"/>, or a qualifier's
/// name after <c>,</c>) becomes one when accepted. A qualifier's value is the text after its name. Backspace with no text after them turns
/// the last label back into text. <see cref="Text"/> is read as before (<see cref="InputParser"/>):
/// labels change how it shows, not what it means. With a fixed first node (the node of the details' Link
/// box), it is the start of <see cref="Text"/>, never a label shown nor taken out, and only a link type is proposed after it.
/// </summary>
public sealed class ChipInput(string? fixedLeft = null)
{
    private readonly string? _fixedLeft = fixedLeft;
    private string? _left = fixedLeft;
    private string? _linkType;
    private string? _right;
    private bool _typedAtEnd;

    // The qualifiers' names, each with its value; the last one's is null while it is the text typed (Tail).
    private readonly List<(string Name, string? Value)> _qualifiers = [];

    /// <summary>The text after the labels, as typed.</summary>
    public string Tail { get; private set; } = string.Empty;

    public bool IsEmpty => (_left is null || IsFixed) && _linkType is null && Tail.Length == 0;

    /// <summary>The labels and the text after them, as one text.</summary>
    public string Text => Compose(Labels);

    /// <summary>The <see cref="Text"/> after the fixed first node; all of it without one.</summary>
    public string Typed => Compose(Labels.Skip(IsFixed ? 1 : 0));

    private bool IsFixed => _fixedLeft is not null;

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
        if (!_typedAtEnd)
        {
            return false;
        }

        var labelled = _linkType is null && Recognize(graph.Types);
        while (_linkType is not null && RecognizeQualifier(graph))
        {
            labelled = true;
        }

        return labelled;
    }

    /// <summary>Backspace with no text after the labels: the last one becomes text again. False when there is text, or no label.</summary>
    public bool Backspace()
    {
        if (Tail.Length == 0 && _qualifiers.Count > 0)
        {
            var (name, value) = _qualifiers[^1];
            _qualifiers.RemoveAt(_qualifiers.Count - 1);
            Tail = value is { Length: > 0 } ? $", {name} {value}" : $", {name}";
            _typedAtEnd = false;
            return true;
        }

        if (Tail.Length > 0 || (_right ?? _linkType ?? (IsFixed ? null : _left)) is not { } last)
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

        if (_linkType is null)
        {
            return _fixedLeft is { } left
                ? InputCompletion.ProposeLinkTypeAfter(graph.Types, left, Tail)
                : InputCompletion.Propose(graph, Text, linkTypeTyped: false);
        }

        if (QualifierText.Proposed(Tail, QualifiersOf(graph), nodeTyped: _right is not null) is { } qualifier)
        {
            var typed = string.Join(' ', InputParser.Words(Tail[(Tail.LastIndexOf(',') + 1)..]));
            return new Proposal(qualifier.Name[typed.Length..], qualifier.Name, IsLinkType: false, IsQualifier: true);
        }

        return _qualifiers.Count > 0 || Tail.Contains(',') ? null : InputCompletion.Propose(graph, _right is null ? Tail : $"{_right} {Tail}", linkTypeTyped: true);
    }

    /// <summary>Tab: the proposal becomes a label, replacing the side it completes. False when there is none.</summary>
    public bool Accept(KnowledgeGraph graph)
    {
        if (Proposal(graph) is not { } proposal)
        {
            return false;
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
        Tail = Typed;
        (_left, _linkType, _right) = (_fixedLeft, null, null);
        _qualifiers.Clear();
        _typedAtEnd = false;
    }

    public void Clear()
    {
        (_left, _linkType, _right) = (_fixedLeft, null, null);
        _qualifiers.Clear();
        Tail = string.Empty;
        _typedAtEnd = false;
    }

    /// <summary>The labels, as shown; a node's pictures the node the plan uses, or else the oldest of its name.</summary>
    public IReadOnlyList<InputChip> Chips(KnowledgeGraph graph, AddPlan? plan)
    {
        var link = plan as LinkPlan;
        var chips = new List<InputChip>();
        if (_left is not null && !IsFixed)
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

        chips.AddRange(_qualifiers.Select(qualifier =>
            new InputChip(qualifier.Value is { Length: > 0 } value ? $"{qualifier.Name} {value}" : qualifier.Name, IsLinkType: false, IsQualifier: true)));
        return chips;
    }

    /// <summary>Enter: everything becomes labels, as read (a node, or both nodes, the link type and the qualifiers), no text left after them.</summary>
    public void LabelAll(KnowledgeGraph graph)
    {
        var text = Text;
        switch (InputParser.Parse(text, graph.Types))
        {
            case var request when IsFixed && (request is not LinkRequest link || !StartsWithFixed(link)):
                return;
            case NodeRequest node:
                (_left, _linkType, _right) = (node.Name, null, null);
                break;
            case LinkRequest link:
                var qualified = (LinkRequest)InputParser.Parse(text, graph)!;
                (_left, _linkType, _right) = (link.Left, PhraseOf(InputParser.Words(text), link), qualified.Right.Length > 0 ? qualified.Right : null);
                _qualifiers.Clear();
                _qualifiers.AddRange(qualified.QualifiersTyped.Select(qualifier => (qualifier.Name, (string?)qualifier.Value)));
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
        if (InputParser.Parse(text, types) is not LinkRequest link || (link.Right.Length == 0 && !char.IsWhiteSpace(text[^1]))
            || (IsFixed && !StartsWithFixed(link)))
        {
            return false;
        }

        var words = InputParser.Words(text);
        _left = link.Left;
        _linkType = PhraseOf(words, link);
        Tail = After(text, words.Length - InputParser.Words(link.Right).Length);
        return true;
    }

    // ", Name " typed after the second node: it becomes a label, and the qualifier's name another one.
    private bool RecognizeQualifier(KnowledgeGraph graph)
    {
        var fields = QualifiersOf(graph);
        if (QualifierText.FirstQualifier(Tail, fields, complete: true, nodeTyped: _right is not null) is not { } comma)
        {
            return false;
        }

        var field = QualifierText.NameAt(Tail[(comma + 1)..], fields, complete: true)!;
        var rest = After(Tail[(comma + 1)..], InputParser.Words(field.Name).Length);
        StartQualifier(Tail[..comma], field.Name);
        Tail = rest;
        _typedAtEnd = true;
        return true;
    }

    // A qualifier's name becomes a label: the text before its "," completes the label before it (the second node, or the last value).
    private void StartQualifier(string before, string name)
    {
        var typed = string.Join(' ', InputParser.Words(before));
        if (_qualifiers.Count > 0)
        {
            var (lastName, lastValue) = _qualifiers[^1];
            _qualifiers[^1] = (lastName, string.Join(' ', new[] { lastValue ?? string.Empty, typed }.Where(part => part.Length > 0)));
        }
        else if (typed.Length > 0)
        {
            _right = _right is null ? typed : $"{_right} {typed}";
        }

        _qualifiers.Add((name, null));
        Tail = string.Empty;
        _typedAtEnd = false;
    }

    // The qualifiers of the link type typed; none before it is.
    private IReadOnlyList<Fields.FieldDefinition> QualifiersOf(KnowledgeGraph graph) =>
        InputParser.Parse(Compose(Labels, withTail: false), graph.Types) is LinkRequest link ? graph.LinkTypes.FieldsOf(link.Verb.Type.Id) : [];

    // The labels, the qualifiers, then the text typed: "Dune written by Frank Herbert, Date 1965, Source isfdb".
    private string Compose(IEnumerable<string> labels, bool withTail = true)
    {
        var tail = withTail ? Tail : string.Empty;
        if (_qualifiers.Count == 0)
        {
            return string.Join(' ', labels.Append(tail));
        }

        var qualifiers = _qualifiers.Select(qualifier => qualifier.Value is { Length: > 0 } value ? $", {qualifier.Name} {value}" : $", {qualifier.Name}");
        return $"{string.Join(' ', labels)}{string.Concat(qualifiers)} {tail}";
    }

    // The link read starts with the fixed node, not with more words of the text.
    private bool StartsWithFixed(LinkRequest link) => InputParser.Words(link.Left).Length == InputParser.Words(_fixedLeft!).Length;

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
