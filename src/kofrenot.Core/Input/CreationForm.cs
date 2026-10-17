using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The creation screen: what a request adds, the types the user can still change (each new
/// node's, and the link's), and what already exists. Every change plans again.
/// </summary>
public sealed class CreationForm
{
    private readonly AddPlanner _planner;
    private readonly PlanText _text;
    private InputRequest _request;
    private AddChoices _choices;

    /// <param name="choices">The homonyms chosen in the input box; the types default to generic.</param>
    public CreationForm(KnowledgeGraph graph, InputRequest request, AddChoices? choices = null, TypeCatalog? types = null, TimeProvider? clock = null)
    {
        _planner = new AddPlanner(graph, clock);
        _text = new PlanText(graph);
        _request = request;
        _choices = choices ?? AddChoices.Default;
        Types = types ?? TypeCatalog.BuiltIn;
        Plan = _planner.Plan(_request, _choices);
    }

    public TypeCatalog Types { get; }

    public AddPlan Plan { get; private set; }

    public bool IsLink => _request is LinkRequest;

    public string Title => IsLink ? "Add a link" : "Add a node";

    /// <summary>The type of the node, or of the left side's node when it is new.</summary>
    public NodeType LeftType
    {
        get => _choices.LeftType ?? TypeCatalog.Generic;
        set => Replan(_request, _choices with { LeftType = value });
    }

    /// <summary>The type of the right side's node when it is new.</summary>
    public NodeType RightType
    {
        get => _choices.RightType ?? TypeCatalog.Generic;
        set => Replan(_request, _choices with { RightType = value });
    }

    /// <summary>The link's type, read from left to right (one of <see cref="TypeCatalog.Verbs"/>); null for a node.</summary>
    /// <exception cref="InvalidOperationException">Set on a node request.</exception>
    public LinkVerb? Verb
    {
        get => (_request as LinkRequest)?.Verb;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var link = _request as LinkRequest ?? throw new InvalidOperationException("A node has no link type.");
            Replan(link with { Verb = value }, _choices);
        }
    }

    /// <summary>What Add does, or why it cannot.</summary>
    public string Status => Plan is LinkPlan link && _text.Problem(link) is { } problem
        ? problem
        : $"Adds {_text.Additions(Plan)}.";

    /// <summary>A node whose name exists already: Add makes another one. Null otherwise.</summary>
    public string? Warning => PlanText.Duplicate(Plan) is { } duplicate
        ? $"{duplicate} Add makes another node with this name."
        : null;

    /// <summary>The nodes that already have the name of a node request, with their types.</summary>
    public IReadOnlyList<string> Existing => Plan is NodePlan node
        ? node.Existing.Select(existing => $"{_text.NodeLabel(existing)} ({Types.NodeTypeName(existing.Type)})").ToList()
        : [];

    /// <summary>A side's label: the chosen node's (marked among homonyms), or the new name.</summary>
    public string Label(PlannedSide side) => _text.Label(side);

    /// <summary>Whether a side is a new node, or which existing one the link uses.</summary>
    public string Describe(PlannedSide side) => side switch
    {
        { IsNew: true } => "New node",
        { Candidates.Count: 1 } => $"Exists already ({Types.NodeTypeName(side.Chosen!.Type)})",
        _ => $"Exists already ({Types.NodeTypeName(side.Chosen!.Type)}), one of {side.Candidates.Count} nodes with this name",
    };

    private void Replan(InputRequest request, AddChoices choices)
    {
        _request = request;
        _choices = choices;
        Plan = _planner.Plan(_request, _choices);
    }
}
