namespace Kofrenot.Core.Tests.Views;

public sealed class GraphLinkPlannerTests
{
    private readonly FamilyGraph _family = new();
    private readonly ManualClock _clock = new();
    private readonly GraphLinkPlanner _planner;

    public GraphLinkPlannerTests() => _planner = new GraphLinkPlanner(_family.Graph, _clock);

    [Fact]
    public void TheVerbs_AreEachWayOfTheViewsKnownLinkTypes_InItsOrder()
    {
        var definition = _family.Definition(_family.Add("Me"), links: [new(FamilyGraph.Spouse, GraphRole.Beside), new("unknown", GraphRole.Free), new(FamilyGraph.Parent, GraphRole.Down), new(FamilyGraph.Parent, GraphRole.Up)]);

        Assert.Equal(["married to", "parent of", "child of"], _planner.Verbs(definition).Select(verb => verb.Name));
    }

    [Fact]
    public void TheChoices_ReadFromTheFirstNode_AndSayWhichAreThereAlready()
    {
        var (anne, bob) = (_family.Add("Anne"), _family.Add("Bob"));
        _family.ParentOf(bob, anne);

        var choices = _planner.Choices(_family.Definition(anne), anne.Id, bob.Id);

        Assert.Equal(["Anne parent of Bob", "Anne child of Bob", "Anne married to Bob"], choices.Select(choice => choice.Label));
        Assert.Equal([false, true, false], choices.Select(choice => choice.Exists));
    }

    [Fact]
    public void ASymmetricLink_IsThereAlready_EitherWay()
    {
        var (anne, bob) = (_family.Add("Anne"), _family.Add("Bob"));
        _family.Married(bob, anne);

        var marriage = _planner.Choices(_family.Definition(anne), anne.Id, bob.Id).Single(choice => choice.Verb.Type.Id == FamilyGraph.Spouse);

        Assert.True(marriage.Exists);
        Assert.Null(_planner.Add(marriage));
    }

    [Fact]
    public void NoChoice_FromANodeToItself_OrWithANodeNotThere()
    {
        var anne = _family.Add("Anne");
        var definition = _family.Definition(anne);

        Assert.Empty(_planner.Choices(definition, anne.Id, anne.Id));
        Assert.Empty(_planner.Choices(definition, anne.Id, Guid.NewGuid()));
        Assert.Empty(_planner.Choices(definition, Guid.NewGuid(), anne.Id));
    }

    [Fact]
    public void AChoiceAdded_IsALinkOfItsType_ReversedWhenItsVerbIs()
    {
        var (anne, bob) = (_family.Add("Anne"), _family.Add("Bob"));
        var choices = _planner.Choices(_family.Definition(anne), anne.Id, bob.Id);

        var parent = _planner.Add(choices[0])!;
        var child = _planner.Add(choices[1])!;

        Assert.Equal((FamilyGraph.Parent, anne.Id, bob.Id, _clock.Now), (parent.Link.Type, parent.Link.From, parent.Link.To, parent.At));
        Assert.Equal((FamilyGraph.Parent, bob.Id, anne.Id), (child.Link.Type, child.Link.From, child.Link.To));
    }

    [Fact]
    public void AChoiceAddedSinceItWasOffered_AddsNothing()
    {
        var (anne, bob) = (_family.Add("Anne"), _family.Add("Bob"));
        var choice = _planner.Choices(_family.Definition(anne), anne.Id, bob.Id)[0];
        _family.ParentOf(anne, bob);

        Assert.Null(_planner.Add(choice));
    }
}
