namespace Kofrenot.Core.Tests.Browse;

public sealed class TypeListTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void NodeTypes_BuiltInFirst_ThenTheUsersByName_WithTheirFields()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null,
            [new FieldDefinition("born", "Born", FieldKind.Date), new FieldDefinition("mail", "Email", FieldKind.Email)], []));
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "book", "Book", null, null, [], []));

        var lines = TypeList.NodeTypes(_graph);

        Assert.Equal(["Dataset", "Generic", "Note", "View", "Book", "Person"], lines.Select(line => line.Name));
        Assert.All(lines.Take(4), line => Assert.True(line.IsBuiltIn));
        Assert.Equal("Fields: Born (date), Email (email)", lines.Single(line => line.Name == "Person").Detail);
        Assert.False(lines.Single(line => line.Name == "Person").IsBuiltIn);
        Assert.Equal("No fields", lines.Single(line => line.Name == "Book").Detail);
    }

    [Fact]
    public void NodeTypes_LeaveOutContent_AndMappings()
    {
        var lines = TypeList.NodeTypes(_graph);

        Assert.Equal("No fields", lines.Single(line => line.Name == "Note").Detail);
        Assert.DoesNotContain(lines, line => line.Name == TypeCatalog.Mapping.Name);
    }

    [Fact]
    public void LinkTypes_TellTheirReverse_Aliases_AndQualifiers()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "read-by", "read by", "has read",
            [new FieldDefinition("date", "Date", FieldKind.Date)], []));

        var lines = TypeList.LinkTypes(_graph);

        Assert.Equal(["contains", "is a", "relates to", "read by"], lines.Select(line => line.Name));
        Assert.Equal("reverse: contained by", lines.Single(line => line.Name == "contains").Detail);
        Assert.Equal("reverse: has instance · aliases: is an", lines.Single(line => line.Name == "is a").Detail);
        Assert.Equal("the same both ways · aliases: related to", lines.Single(line => line.Name == "relates to").Detail);
        Assert.Equal("reverse: has read · Qualifiers: Date (date)", lines.Single(line => line.Name == "read by").Detail);
        Assert.False(lines.Single(line => line.Name == "read by").IsBuiltIn);
    }
}
