namespace Kofrenot.Core.Tests.Session;

public sealed class PhoneChangesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Note.Id, "Dune", Start);

    public PhoneChangesTests() => _graph.Add(_dune);

    public static TheoryData<Change> OfNodes => new()
    {
        new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Arrakis", Start)),
        new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, Guid.NewGuid(), Guid.NewGuid(), Start)),
        new NodeRenamed(Guid.NewGuid(), Start, Guid.NewGuid(), "Dune I", []),
        new NodeColored(Guid.NewGuid(), Start, Guid.NewGuid(), "blue", []),
        new NodePinned(Guid.NewGuid(), Start, Guid.NewGuid()),
        new NodeTypeAdded(Guid.NewGuid(), Start, Guid.NewGuid(), "person"),
        new NodeTypeRemoved(Guid.NewGuid(), Start, Guid.NewGuid(), [Guid.NewGuid()]),
        new FieldSet(Guid.NewGuid(), Start, Guid.NewGuid(), FieldSet.Own, "born", "Born", FieldKind.Date, "1965-01-01", []),
        new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []),
        new ViewEdited(Guid.NewGuid(), Start, Guid.NewGuid(), new FlatDefinition(TypeCatalog.Note.Id), []),
        new MemberAdded(Guid.NewGuid(), Start, new VaultMember(Guid.NewGuid(), Guid.NewGuid(), [], "ada@example.com", MemberRole.Writer), []),
    };

    public static TheoryData<Change> OfTheVault => new()
    {
        new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [], []),
        new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", []),
        new AliasAdded(Guid.NewGuid(), Start, TypeCatalog.Contains.Id, IsReverse: false, "holds"),
        new VaultNamed(Guid.NewGuid(), Start, "Mine", []),
        new MemberRemoved(Guid.NewGuid(), Start, Guid.NewGuid(), []),
    };

    [Theory]
    [MemberData(nameof(OfNodes))]
    public void AChangeOfTheNodes_IsWrittenOnThePhone(Change change) =>
        Assert.True(PhoneChanges.Allows(change, _graph));

    [Theory]
    [MemberData(nameof(OfTheVault))]
    public void AChangeThatDefinesTheVault_IsLeftToWindows(Change change) =>
        Assert.False(PhoneChanges.Allows(change, _graph));

    [Fact]
    public void ARemoval_OfNodesLinksAndPins_IsWritten_OneOfADefinition_IsNot()
    {
        var person = new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [], []);
        var wrote = new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", []);
        var holds = new AliasAdded(Guid.NewGuid(), Start, TypeCatalog.Contains.Id, IsReverse: false, "holds");
        var pin = new NodePinned(Guid.NewGuid(), Start, _dune.Id);
        foreach (var change in new Change[] { person, wrote, holds, pin })
        {
            _graph.Apply(change);
        }

        Assert.True(PhoneChanges.Allows(new ChangesRemoved(Guid.NewGuid(), Start, [_dune.Id, pin.Id]), _graph));
        Assert.False(PhoneChanges.Allows(new ChangesRemoved(Guid.NewGuid(), Start, [person.Id]), _graph));
        Assert.False(PhoneChanges.Allows(new ChangesRemoved(Guid.NewGuid(), Start, [wrote.Id]), _graph));
        Assert.False(PhoneChanges.Allows(new ChangesRemoved(Guid.NewGuid(), Start, [_dune.Id, holds.Id]), _graph));
    }
}
