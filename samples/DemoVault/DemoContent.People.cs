using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Storage;
using Color = System.Windows.Media.Color;

namespace Kofrenot.Samples;

/// <summary>What the people and books add: the book club node and the views of people.</summary>
internal sealed record DemoPeople(Node Club, Guid FamilyView, Guid ClubView, Guid PeopleView);

/// <summary>
/// Node types with fields (a Person, a Book), link types of the user's, a made-up family over three
/// generations with drawn portraits (the Family graph, in ranks), and a book club of members, books
/// and their authors (the Book club graph, as a network). Every name and picture is made up, the
/// classic authors aside.
/// </summary>
internal sealed partial class DemoContent
{
    private NodeType _person = null!;
    private NodeType _book = null!;

    private DemoPeople WritePeople()
    {
        _person = DefineType("Person", "contact", NodeColor.Blue,
            new FieldDraft(null, "Born", FieldKind.Date),
            new FieldDraft(null, "Died", FieldKind.Date),
            new FieldDraft(null, "Email", FieldKind.Email),
            new FieldDraft(null, "Website", FieldKind.Url),
            new FieldDraft(null, "Address", FieldKind.Address),
            new FieldDraft(null, "Photo", FieldKind.Image));
        _book = DefineType("Book", "book", NodeColor.Yellow,
            new FieldDraft(null, "Genre", FieldKind.Choice, ["Novel", "Adventure", "Romance", "Gothic", "Epic"]),
            new FieldDraft(null, "Published", FieldKind.Number),
            new FieldDraft(null, "Read", FieldKind.Checkbox),
            new FieldDraft(null, "Cover", FieldKind.Image));
        var parentOf = DefineLink("parent of", "child of");
        var marriedTo = DefineLink("married to", null);
        var writtenBy = DefineLink("written by", "author of");
        var memberOf = DefineLink("member of", "has member");
        var recommends = DefineLink("recommends", "recommended by");

        var family = WriteFamily(parentOf, marriedTo);
        var club = WriteBookClub(writtenBy, memberOf, recommends);

        var familyView = View("Family", new GraphDefinition(
            family.Id, 6, GraphLayoutKind.Layered,
            [new GraphLink(parentOf.Id, GraphRole.Down), new GraphLink(marriedTo.Id, GraphRole.Beside)],
            [_person.Id], ["Born", "Died"]));
        var clubView = View("Book club", new GraphDefinition(
            club.Id, 3, GraphLayoutKind.Network,
            [new GraphLink(memberOf.Id, GraphRole.Free), new GraphLink(recommends.Id, GraphRole.Free), new GraphLink(writtenBy.Id, GraphRole.Free)],
            [], ["Genre"]));
        var peopleView = View("People", new FlatDefinition(_person.Id));
        return new DemoPeople(club, familyView.Id, clubView.Id, peopleView.Id);
    }

    /// <summary>The Marlowes and the Fenwicks; the root of the Family graph, Daniel, is returned.</summary>
    private Node WriteFamily(LinkType parentOf, LinkType marriedTo)
    {
        var arthur = Person("Arthur Marlowe", "1938-04-02", "2015-11-20", Color.FromRgb(70, 90, 140));
        var helen = Person("Helen Marlowe", "1941-07-15", null, Color.FromRgb(160, 90, 120));
        var george = Person("George Fenwick", "1940-01-09", "2019-03-03", Color.FromRgb(80, 120, 90));
        var ruth = Person("Ruth Fenwick", "1943-09-30", null, Color.FromRgb(170, 120, 70));
        var daniel = Person("Daniel Marlowe", "1965-05-21", null, Color.FromRgb(60, 110, 150));
        var claire = Person("Claire Fenwick", "1967-12-04", null, Color.FromRgb(140, 80, 140));
        var sophie = Person("Sophie Becker", "1969-03-11", null, Color.FromRgb(190, 100, 90));
        var tom = Person("Tom Becker", "1968-08-26", null, Color.FromRgb(90, 100, 110));
        var emma = Person("Emma Marlowe", "1994-06-18", null, Color.FromRgb(50, 140, 140));
        var lucas = Person("Lucas Marlowe", "1997-02-07", null, Color.FromRgb(120, 120, 60));
        var nora = Person("Nora Becker", "2001-10-29", null, Color.FromRgb(170, 90, 160));

        foreach (var (one, other) in new[] { (arthur, helen), (george, ruth), (daniel, claire), (tom, sophie) })
        {
            Link(one, marriedTo.Id, other);
        }

        foreach (var (parent, child) in new[]
        {
            (arthur, daniel), (helen, daniel), (arthur, sophie), (helen, sophie), (george, claire), (ruth, claire),
            (daniel, emma), (claire, emma), (daniel, lucas), (claire, lucas), (sophie, nora), (tom, nora),
        })
        {
            Link(parent, parentOf.Id, child);
        }

        Set(emma, "Email", "emma.marlowe@example.com");
        Set(emma, "Website", "https://example.com/emma");
        Set(emma, "Address", "4 Rope Walk, Bristol");
        Set(emma, "Address", "27 Harbour Street, Bristol");
        Set(claire, "Email", "claire.fenwick@example.com");
        Set(daniel, "Address", "12 Orchard Lane, Bath");
        return daniel;
    }

    /// <summary>The book club, its members, their recommendations and the books' authors; the club node, the Book club graph's root.</summary>
    private Node WriteBookClub(LinkType writtenBy, LinkType memberOf, LinkType recommends)
    {
        var club = Add("Book club");
        var ana = Person("Ana Ruiz", "1990-03-14", null, Color.FromRgb(200, 110, 60));
        var ben = Person("Ben Okafor", "1988-11-02", null, Color.FromRgb(60, 130, 100));
        var claire = Graph.Nodes.First(node => node.Name == "Claire Fenwick");
        var emma = Graph.Nodes.First(node => node.Name == "Emma Marlowe");
        foreach (var member in new[] { ana, ben, claire, emma })
        {
            Link(member, memberOf.Id, club);
        }

        var list = Add("Reading list");
        var novel = Add("novel");
        var melville = Author("Herman Melville", "1819-08-01", "1891-09-28");
        var austen = Author("Jane Austen", "1775-12-16", "1817-07-18");
        var shelley = Author("Mary Shelley", "1797-08-30", "1851-02-01");
        var stevenson = Author("Robert Louis Stevenson", "1850-11-13", "1894-12-03");
        var homer = Add("Homer", _person);
        var mobyDick = Book(list, novel, "Moby-Dick", melville, writtenBy, "Adventure", 1851, isRead: true, Color.FromRgb(20, 60, 110), Color.FromRgb(10, 25, 50));
        var pride = Book(list, novel, "Pride and Prejudice", austen, writtenBy, "Romance", 1813, isRead: true, Color.FromRgb(190, 120, 140), Color.FromRgb(110, 50, 80));
        var frankenstein = Book(list, novel, "Frankenstein", shelley, writtenBy, "Gothic", 1818, isRead: false, Color.FromRgb(60, 90, 70), Color.FromRgb(20, 30, 25));
        var treasure = Book(list, novel, "Treasure Island", stevenson, writtenBy, "Adventure", 1883, isRead: true, Color.FromRgb(210, 150, 60), Color.FromRgb(120, 60, 20));
        var odyssey = Book(list, novel: null, "The Odyssey", homer, writtenBy, "Epic", null, isRead: false, Color.FromRgb(70, 110, 170), Color.FromRgb(200, 170, 110));

        foreach (var (member, book) in new[] { (ana, frankenstein), (ana, pride), (ben, mobyDick), (ben, odyssey), (claire, treasure), (emma, frankenstein), (emma, odyssey) })
        {
            Link(member, recommends.Id, book);
        }

        return club;
    }

    private Node Person(string name, string born, string? died, Color color)
    {
        var person = Add(name, _person);
        Set(person, "Born", born);
        if (died is not null)
        {
            Set(person, "Died", died);
        }

        var initials = string.Concat(name.Split(' ').Select(word => word[0]));
        SetImage(person, "Photo", Draw($"{name}.jpg", path => Covers.Portrait(path, initials, color)));
        return person;
    }

    // A real author: dates only, no picture.
    private Node Author(string name, string born, string died)
    {
        var author = Add(name, _person);
        Set(author, "Born", born);
        Set(author, "Died", died);
        return author;
    }

    /// <summary>A book in the reading list, a novel or not, by its author; its cover a dataset of its own, shown as its Cover field.</summary>
    private Node Book(Node list, Node? novel, string title, Node author, LinkType writtenBy, string genre, int? published, bool isRead, Color top, Color bottom)
    {
        var book = Add(title, _book, under: list);
        if (novel is not null)
        {
            Link(book, TypeCatalog.IsA.Id, novel);
        }

        Link(book, writtenBy.Id, author);
        Set(book, "Genre", genre);
        if (published is { } year)
        {
            Set(book, "Published", year.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Apply(new FieldPlanner(Graph, _clock).Check(book, FieldOf(book, "Read"), isRead));
        var cover = Add($"{title} cover", TypeCatalog.Dataset);
        Link(book, TypeCatalog.RelatesTo.Id, cover);
        var image = Picture(cover, $"{title}.jpg", path => Covers.Book(path, title, author.Name, top, bottom));
        Apply(new FieldPlanner(Graph, _clock).SetImageFrom(book, FieldOf(book, "Cover"), image));
        return book;
    }

    private NodeType DefineType(string name, string icon, NodeColor color, params FieldDraft[] fields)
    {
        var plan = new NodeTypePlanner(Graph, _clock).Plan(new NodeTypeDraft(name, icon, color, fields));
        var defined = Planned(plan.Change, plan.Status);
        Append(defined);
        return Graph.Types.FindNodeType(defined.NodeType)!;
    }

    private LinkType DefineLink(string name, string? reverse)
    {
        var plan = new LinkTypePlanner(Graph, _clock).Plan(name, reverse);
        var defined = Planned(plan.Change, plan.Status);
        Append(defined);
        return Graph.Types.FindLinkType(defined.LinkType)!;
    }

    private void Set(Node node, string field, string value) =>
        Apply(new FieldPlanner(Graph, _clock).Set(node, FieldOf(node, field), value));

    // Its file written as a dataset version's, then its value.
    private void SetImage(Node node, string field, string path)
    {
        var plan = new FieldPlanner(Graph, _clock).SetImage(node, FieldOf(node, field), DatasetFiles.Describe(path));
        var value = (FieldSet)Planned(plan.Changes.SingleOrDefault(), plan.Status);
        vault.WriteDataset(value.Image!, () => File.OpenRead(path), _thumbnails);
        Append(value);
    }

    private NodeField FieldOf(Node node, string name) => NodeFields.Of(Graph, node.Id).Single(field => field.Name == name);

    private void Apply(FieldPlan plan)
    {
        foreach (var change in plan.Changes.Count > 0 ? plan.Changes : [Planned<Change>(null, plan.Status)])
        {
            Append(change);
        }
    }
}
