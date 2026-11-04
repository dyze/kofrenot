using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Platform;

namespace Kofrenot.Samples;

/// <summary>
/// The made-up nodes, links, notes, files, types, fields and views of the demo vault, written as the
/// app writes them (Core's planners). Home projects and notes here, with a history to restore, a
/// conflict to resolve and unlinked nodes; people and books in DemoContent.People.
/// </summary>
internal sealed partial class DemoContent(Vault vault, string pictures, DateTimeOffset start)
{
    public const string VaultName = "Demo";

    private readonly WpfThumbnailMaker _thumbnails = new();
    private readonly DemoClock _clock = new(start);

    private KnowledgeGraph Graph => vault.Graph;

    private DateTimeOffset Now => _clock.GetUtcNow();

    public DemoViews Write()
    {
        Append(new VaultNamed(Guid.NewGuid(), Now, VaultName, []));

        var home = Add("Home");
        var garden = Add("Garden", under: home);
        var kitchen = Add("Kitchen", under: home);
        Rename(kitchen, "Kitchen renovation");
        var lisbon = Add("Trip to Lisbon", TypeCatalog.Note, under: home);
        Paint(lisbon, NodeColor.Teal);
        var drafts = Note(lisbon, "- [ ] Book the flights\n- [ ] Hotel in Alfama");
        drafts = Note(lisbon, "- [x] Book the flights\n- [ ] Hotel in Alfama\n- [ ] Tram 28 tickets", drafts);
        Note(lisbon, "- [x] Book the flights\n- [x] Hotel in Alfama\n- [ ] Tram 28 tickets\n- [ ] Day trip to Sintra\n- [ ] Pastéis de nata in Belém", drafts);
        Dataset(Add("Pastéis de nata", TypeCatalog.Dataset, under: lisbon), "Pastéis de nata.md", "# Pastéis de nata\n\n*Makes 12.*\n\n1. Roll the puff pastry into a log, cut 12 slices.\n2. Press each slice into a muffin tin.\n3. Whisk 4 yolks, 100 g sugar, 250 ml milk and a spoon of flour; warm until thick.\n4. Fill the cups, bake 15 minutes at 250 °C, until the tops are dark.\n");

        var ideas = Add("Ideas for the garden", TypeCatalog.Note, under: garden);
        Note(ideas, "# Garden\n\nRaised beds along the south fence, **tomatoes** and *basil* first.\nA small pond near the apple tree?");
        Paint(ideas, NodeColor.Green);
        Dataset(Add("Garden plan.csv", TypeCatalog.Dataset, under: garden), "Garden plan.csv", "bed,plant,sown\n1,tomatoes,2026-04-12\n2,basil,2026-04-20\n3,beans,2026-05-02\n");
        Dataset(Add("Budget.csv", TypeCatalog.Dataset, under: kitchen), "Budget.csv", "item,cost\ncabinets,4200\nworktop,1300\npaint,180\nlights,350\n");
        Picture(Add("Kitchen sketch", TypeCatalog.Dataset, under: kitchen), "Kitchen sketch.jpg", Covers.Sketch);
        var paint = Add("Paint colours", TypeCatalog.Note, under: kitchen);
        Note(paint, "- [x] Sage green for the walls\n- [ ] Warm white for the ceiling\n- [ ] Samples from the shop");
        Paint(paint, NodeColor.Yellow);

        var groceries = Add("Groceries", TypeCatalog.Note);
        Note(groceries, "- [ ] Olive oil\n- [ ] Lemons\n- [ ] Coffee beans\n- [x] Bread\n- [x] Eggs");
        Paint(groceries, NodeColor.Pink);
        AddUnlinked();

        var people = WritePeople();
        var meeting = Add("Meeting notes", TypeCatalog.Note, under: people.Club);
        Paint(meeting, NodeColor.Purple);
        WriteConflict(meeting);

        var projects = View("Projects", new TreeDefinition(home.Id, TypeCatalog.Contains.Id));
        var notes = View("Notes", new FlatDefinition(TypeCatalog.Note.Id));
        var covers = View("Covers", new FlatDefinition(TypeCatalog.Dataset.Id));
        Append(new NodePinned(Guid.NewGuid(), Now, projects.Id));
        Append(new NodePinned(Guid.NewGuid(), Now, groceries.Id));
        Append(new NodePinned(Guid.NewGuid(), Now, people.FamilyView));
        return new DemoViews(projects.Id, people.FamilyView, people.ClubView, people.PeopleView, notes.Id, covers.Id);
    }

    // The nodes no link leads to, to put into a tree (Unlinked, beside Projects).
    private void AddUnlinked()
    {
        Add("Call the plumber");
        Note(Add("Birthday gift ideas", TypeCatalog.Note), "- [ ] A pasta machine\n- [ ] Tickets for the jazz festival\n- [x] A good olive oil");
        Add("Wallpaper samples");
    }

    // Edited on two devices before they synced: both versions replace the first one.
    private void WriteConflict(Node meeting)
    {
        var first = Note(meeting, "# Book club, October\n\nNext book: *Frankenstein*.");
        Note(meeting, "# Book club, October\n\nNext book: *Frankenstein*. Meet at Ana's on the 14th.", first);
        Note(meeting, "# Book club, October\n\nNext book: *Frankenstein*. Meet at Ben's on the 15th, bring snacks.", first);
    }

    private Node Add(string name, NodeType? type = null, Node? under = null)
    {
        var node = new Node(Guid.NewGuid(), (type ?? TypeCatalog.Generic).Id, name, Now);
        Append(new NodeAdded(Guid.NewGuid(), node.Created, node));
        if (under is not null)
        {
            Link(under, TypeCatalog.Contains.Id, node);
        }

        return node;
    }

    private void Link(Node from, string type, Node to) =>
        Append(new LinkAdded(Guid.NewGuid(), Now, new Link(Guid.NewGuid(), type, from.Id, to.Id, Now)));

    private void Rename(Node node, string name) =>
        Append(new NodeRenamed(Guid.NewGuid(), Now, node.Id, name, Graph.NamesOf(node.Id).Select(current => current.Id).ToList()));

    private void Paint(Node node, NodeColor color) =>
        Append(new NodeColored(Guid.NewGuid(), Now, node.Id, color.ToString().ToLowerInvariant(), []));

    /// <summary>A note's version, over <paramref name="replaces"/> (none for the first); its id, for the next one.</summary>
    private Guid Note(Node note, string content, Guid? replaces = null)
    {
        var version = new NoteEdited(Guid.NewGuid(), Now, note.Id, replaces is { } replaced ? [replaced] : []);
        vault.Save(version, content);
        return version.Id;
    }

    private DatasetEdited Dataset(Node dataset, string name, string text) =>
        Import(dataset, Draw(name, path => File.WriteAllText(path, text)));

    private DatasetEdited Picture(Node dataset, string name, Action<string> draw) => Import(dataset, Draw(name, draw));

    // A file of the pictures folder, written by draw: what is imported.
    private string Draw(string name, Action<string> draw)
    {
        var path = Path.Combine(pictures, name);
        draw(path);
        return path;
    }

    private DatasetEdited Import(Node dataset, string path)
    {
        var version = new DatasetEdited(Guid.NewGuid(), Now, dataset.Id, DatasetFiles.Describe(path), []);
        vault.Save(version, () => File.OpenRead(path), _thumbnails);
        return version;
    }

    private Node View(string name, ViewDefinition definition)
    {
        var view = Add(name, TypeCatalog.View);
        Append(new ViewEdited(Guid.NewGuid(), Now, view.Id, definition, []));
        return view;
    }

    private void Append(Change change) => vault.Append(change);

    private static T Planned<T>(T? change, string status)
        where T : Change =>
        change ?? throw new InvalidOperationException($"The demo cannot write this: {status}");
}
