using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>A side of a request: the node, or a link's left node; or a link's right node.</summary>
public enum Side
{
    Left,
    Right,
}

/// <summary>A step of the creation screen: a new node (<see cref="Side"/>), or the link (no side).</summary>
public sealed record CreationStep(Side? Side)
{
    public bool IsLink => Side is null;
}

/// <summary>The content of a new note, saved with the creation: its first version.</summary>
public sealed record NoteToSave(NoteEdited Version, string Content);

/// <summary>
/// The steps of the creation screen: one per new node (its type, and its content for a note), then
/// the link. An existing node has no step. Nothing is added before the last step.
/// </summary>
public sealed partial class CreationForm
{
    private readonly Dictionary<Side, string> _contents = new() { [Side.Left] = string.Empty, [Side.Right] = string.Empty };

    /// <summary>Fixed for the request: types and the link type do not change which nodes are new.</summary>
    public IReadOnlyList<CreationStep> Steps { get; }

    public int StepIndex { get; private set; }

    public CreationStep Step => Steps[StepIndex];

    public bool IsLastStep => StepIndex == Steps.Count - 1;

    /// <summary>"Step 1 of 3", or empty for a single step.</summary>
    public string StepText => Steps.Count > 1 ? $"Step {StepIndex + 1} of {Steps.Count}" : string.Empty;

    /// <summary>A new note on a step has content: cancelling would lose it.</summary>
    public bool HasContent => Steps.Any(step => step.Side is { } side && IsNote(side) && !string.IsNullOrWhiteSpace(_contents[side]));

    /// <summary>The next step. False on the last one.</summary>
    public bool Next()
    {
        if (IsLastStep)
        {
            return false;
        }

        StepIndex++;
        return true;
    }

    /// <summary>The previous step. False on the first one.</summary>
    public bool Back()
    {
        if (StepIndex == 0)
        {
            return false;
        }

        StepIndex--;
        return true;
    }

    /// <summary>The name of a side: the node's, or one end of the link.</summary>
    public string NameOf(Side side) =>
        Plan is LinkPlan link ? (side == Side.Left ? link.Left : link.Right!).Name : ((NodePlan)Plan).Name;

    public NodeType TypeOf(Side side) => side == Side.Left ? LeftType : RightType;

    public void SetType(Side side, NodeType type)
    {
        if (side == Side.Left)
        {
            LeftType = type;
        }
        else
        {
            RightType = type;
        }
    }

    /// <summary>A new node of type note: its step has a content editor.</summary>
    public bool IsNote(Side side) => TypeOf(side).Id == TypeCatalog.Note.Id;

    public string ContentOf(Side side) => _contents[side];

    public void SetContent(Side side, string content) => _contents[side] = content;

    /// <summary>
    /// The first version of each new note with content, for its node in <see cref="AddPlan.Changes"/>.
    /// New ids at each call: call it once, when adding.
    /// </summary>
    public IReadOnlyList<NoteToSave> NotesToSave()
    {
        var added = Plan.NewNodes;
        var newSides = Steps.Select(step => step.Side).OfType<Side>().ToList();
        return newSides
            .Select((side, index) => (Side: side, Node: added[index]))
            .Where(pair => IsNote(pair.Side) && !string.IsNullOrWhiteSpace(_contents[pair.Side]))
            .Select(pair => new NoteToSave(new NoteEdited(Guid.NewGuid(), _clock.GetUtcNow(), pair.Node.Id, []), _contents[pair.Side]))
            .ToList();
    }

    private static List<CreationStep> StepsOf(AddPlan plan)
    {
        if (plan is not LinkPlan link)
        {
            return [new CreationStep(Side.Left)];
        }

        var steps = new List<CreationStep>();
        if (link.Left.IsNew)
        {
            steps.Add(new CreationStep(Side.Left));
        }

        if (link.Right is { IsNew: true })
        {
            steps.Add(new CreationStep(Side.Right));
        }

        steps.Add(new CreationStep(Side: null));
        return steps;
    }
}
