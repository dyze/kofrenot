using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Views;

/// <summary>A tree's children sorted by a qualifier of their links: books by date.</summary>
public sealed class QualifierOrderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);
    private static readonly FieldDefinition Rank = new("rank", "Rank", FieldKind.Number);
    private static readonly FieldDefinition Cover = new("cover", "Cover", FieldKind.Image);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _frank;

    public QualifierOrderTests()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", [Date, Rank, Cover], []));
        _frank = Add("Frank Herbert");
        Wrote("Dune", "1965-08-01", "10");
        Wrote("Children of Dune", "1976-04-01", "9");
        Wrote("Dune Messiah", "1969-10-15", "2");
        Wrote("The Dragon in the Sea", date: null, rank: null);
        Wrote("Destination: Void", date: null, rank: null);
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);
        _graph.Add(node);
        return node;
    }

    private void Wrote(string book, string? date, string? rank)
    {
        var link = new Link(Guid.NewGuid(), "wrote", _frank.Id, Add(book).Id, Start);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        if (date is not null)
        {
            _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, "wrote", Date.Id, Date.Name, FieldKind.Date, date, []));
            _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, "wrote", Rank.Id, Rank.Name, FieldKind.Number, rank, []));
        }
    }

    private IEnumerable<string> Children(string? order)
    {
        var tree = new TreeView(_graph, new TreeDefinition(_frank.Id, "wrote", order));
        return tree.ChildrenOf(tree.Root!).Select(item => item.Node.Name);
    }

    [Fact]
    public void ATree_SortedByADate_ShowsTheChildrenWithoutOneLast_ByName() =>
        Assert.Equal(["Dune", "Dune Messiah", "Children of Dune", "Destination: Void", "The Dragon in the Sea"], Children("date"));

    [Fact]
    public void ATree_SortedByANumber_ComparesNumbers() =>
        Assert.Equal(["Dune Messiah", "Children of Dune", "Dune", "Destination: Void", "The Dragon in the Sea"], Children("rank"));

    [Fact]
    public void ATree_NotSorted_IsByName() =>
        Assert.Equal(["Children of Dune", "Destination: Void", "Dune", "Dune Messiah", "The Dragon in the Sea"], Children(order: null));

    [Fact]
    public void TheDraft_KeepsTheOrder_AndOffersTheTypedQualifiers()
    {
        var definition = new TreeDefinition(_frank.Id, "wrote", "date");

        var draft = ViewDraft.Of(definition, _graph);

        Assert.Equal(definition, draft.Definition);
        Assert.Equal([Date, Rank], draft.OrderChoices(_graph));
        Assert.Equal("A tree from “Frank Herbert” through “wrote”, by “Date”", ViewDraft.Describe(definition, _graph));
        Assert.Equal("A tree from “Frank Herbert” through “wrote”", ViewDraft.Describe(definition with { Order = "gone" }, _graph));
    }

    [Fact]
    public void ASortedTree_RoundTrips_ItsOrderWrittenOnlyWhenSet()
    {
        var key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
        var codec = new ChangeCodec(new KeyedBox(KeyRing.Of(key)));
        var sorted = new ViewEdited(Guid.NewGuid(), Start, Guid.NewGuid(), new TreeDefinition(_frank.Id, "wrote", "date"), []);
        var plain = sorted with { Id = Guid.NewGuid(), Definition = new TreeDefinition(_frank.Id, "wrote") };

        Assert.Equal(sorted.Definition, Assert.IsType<ViewEdited>(codec.Decode(sorted.Id, codec.Encode(sorted))).Definition);
        Assert.Equal(plain.Definition, Assert.IsType<ViewEdited>(codec.Decode(plain.Id, codec.Encode(plain))).Definition);

        var id = Guid.NewGuid();
        var json = "{\"format\":11,\"at\":\"2026-10-16T09:12:00Z\",\"op\":\"editView\",\"view\":{\"node\":\"" + Guid.NewGuid() + "\",\"display\":\"tree\","
            + "\"query\":{\"root\":\"" + _frank.Id + "\",\"linkType\":\"wrote\"},\"order\":{\"field\":\"\"},\"replaces\":[]}}";
        Assert.Throws<InvalidDataException>(() => codec.Decode(id, new SecretBox(key).Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray())));
    }
}
