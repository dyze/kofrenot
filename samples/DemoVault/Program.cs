using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Platform;
using Color = System.Windows.Media.Color;

namespace Kofrenot.Samples;

/// <summary>
/// Builds a demo vault of made-up data in artifacts\demo, opened by
/// <c>kofrenot.exe --data artifacts\demo\data</c> without asking its pass phrase (remembered for
/// this Windows account; it is <see cref="PassPhrase"/>): books with covers, notes and task lists,
/// a projects tree, and views shown as tabs, two of them as cards.
/// </summary>
internal static class Program
{
    public const string PassPhrase = "demo";

    private static readonly DateTimeOffset Start = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [STAThread]
    private static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("artifacts", "demo"));
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var files = new AppFiles(Path.Combine(root, "data"), AppContext.BaseDirectory, isChosen: true);
        var folder = Path.Combine(root, "vault");
        var pictures = Directory.CreateDirectory(Path.Combine(root, "pictures")).FullName;
        Directory.CreateDirectory(folder);

        var (access, _) = VaultFolder.Create(folder, PassPhrase);
        var vault = Vault.Open(folder, access.Header, access.Keys);
        var demo = new DemoContent(vault, pictures, Start);
        var views = demo.Write();

        Register(files, folder, access, views);
        Console.WriteLine($"Demo vault: {folder}");
        Console.WriteLine($"Open it with: kofrenot.exe --data \"{files.DataDirectory}\" (pass phrase: {PassPhrase})");
        return 0;
    }

    /// <summary>The vault listed, selected, its pass phrase remembered and its name known; its views open as tabs, the lists as cards.</summary>
    private static void Register(AppFiles files, string folder, VaultAccess access, DemoViews views)
    {
        var settings = new SettingsStore(files.Settings);
        var location = new VaultLocation(folder);
        settings.SetVaults(new VaultList([new RegisteredVault(location, access.Header.Id)]));
        settings.SelectVault(location);

        var protector = new DpapiProtector();
        new KeyMemory(files.RememberedKeys, protector).Remember(access);
        new VaultNameMemory(files.VaultNames, protector).Remember(access.Header.Id, DemoContent.VaultName);

        var tabs = new ViewTabs(files.ViewTabs);
        var states = new ViewStates(files.ViewStates);
        foreach (var view in new[] { views.Projects, views.Notes, views.Covers })
        {
            tabs.Open(access.Header.Id, view);
        }

        states.SetShowsCards(access.Header.Id, views.Notes, true);
        states.SetShowsCards(access.Header.Id, views.Covers, true);
    }
}

/// <summary>The views of the demo vault, opened as tabs.</summary>
internal sealed record DemoViews(Guid Projects, Guid Notes, Guid Covers);

/// <summary>The made-up nodes, links, notes, files and views of the demo vault.</summary>
internal sealed class DemoContent(Vault vault, string pictures, DateTimeOffset start)
{
    public const string VaultName = "Demo";

    private readonly WpfThumbnailMaker _thumbnails = new();
    private int _minutes;

    private DateTimeOffset Now => start.AddMinutes(_minutes++);

    public DemoViews Write()
    {
        Append(new VaultNamed(Guid.NewGuid(), Now, VaultName, []));

        var home = Add("Home");
        var garden = Add("Garden", under: home);
        var kitchen = Add("Kitchen renovation", under: home);
        var lisbon = Add("Trip to Lisbon", TypeCatalog.Note, under: home);
        Note(lisbon, "- [x] Book the flights\n- [x] Hotel in Alfama\n- [ ] Tram 28 tickets\n- [ ] Day trip to Sintra\n- [ ] Pastéis de nata in Belém", NodeColor.Teal);

        Note(Add("Ideas for the garden", TypeCatalog.Note, under: garden), "# Garden\n\nRaised beds along the south fence, **tomatoes** and *basil* first.\nA small pond near the apple tree?", NodeColor.Green);
        Dataset(Add("Garden plan.csv", TypeCatalog.Dataset, under: garden), "Garden plan.csv", "bed,plant,sown\n1,tomatoes,2026-04-12\n2,basil,2026-04-20\n3,beans,2026-05-02\n");
        Dataset(Add("Budget.csv", TypeCatalog.Dataset, under: kitchen), "Budget.csv", "item,cost\ncabinets,4200\nworktop,1300\npaint,180\nlights,350\n");
        Picture(Add("Kitchen sketch", TypeCatalog.Dataset, under: kitchen), "Kitchen sketch.jpg", Covers.Sketch);
        Note(Add("Paint colours", TypeCatalog.Note, under: kitchen), "- [x] Sage green for the walls\n- [ ] Warm white for the ceiling\n- [ ] Samples from the shop", NodeColor.Yellow);

        var groceries = Add("Groceries", TypeCatalog.Note);
        Note(groceries, "- [ ] Olive oil\n- [ ] Lemons\n- [ ] Coffee beans\n- [x] Bread\n- [x] Eggs", NodeColor.Pink);
        Note(Add("Meeting notes", TypeCatalog.Note), "# Book club, October\n\nNext book: *Frankenstein*. Meet at Ana's on the 14th.", NodeColor.Purple);

        var books = Add("Reading list");
        var novel = Add("novel");
        Book(books, novel, "Moby-Dick", "Herman Melville", Color.FromRgb(20, 60, 110), Color.FromRgb(10, 25, 50));
        Book(books, novel, "Pride and Prejudice", "Jane Austen", Color.FromRgb(190, 120, 140), Color.FromRgb(110, 50, 80));
        Book(books, novel, "Frankenstein", "Mary Shelley", Color.FromRgb(60, 90, 70), Color.FromRgb(20, 30, 25));
        Book(books, novel, "Treasure Island", "Robert Louis Stevenson", Color.FromRgb(210, 150, 60), Color.FromRgb(120, 60, 20));
        Book(books, novel, "The Odyssey", "Homer", Color.FromRgb(70, 110, 170), Color.FromRgb(200, 170, 110));

        var projects = View("Projects", new TreeDefinition(home.Id, TypeCatalog.Contains.Id));
        var notes = View("Notes", new FlatDefinition(TypeCatalog.Note.Id));
        var covers = View("Covers", new FlatDefinition(TypeCatalog.Dataset.Id));
        Append(new NodePinned(Guid.NewGuid(), Now, projects.Id));
        Append(new NodePinned(Guid.NewGuid(), Now, groceries.Id));
        return new DemoViews(projects.Id, notes.Id, covers.Id);
    }

    /// <summary>A book in the reading list, a novel, and its cover as a dataset.</summary>
    private void Book(Node list, Node novel, string title, string author, Color top, Color bottom)
    {
        var book = Add(title, under: list);
        Link(book, TypeCatalog.IsA.Id, novel);
        var cover = Add($"{title} cover", TypeCatalog.Dataset);
        Link(book, TypeCatalog.RelatesTo.Id, cover);
        Picture(cover, $"{title}.jpg", path => Covers.Book(path, title, author, top, bottom));
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

    private void Note(Node note, string content, NodeColor color)
    {
        vault.Save(new NoteEdited(Guid.NewGuid(), Now, note.Id, []), content);
        Append(new NodeColored(Guid.NewGuid(), Now, note.Id, color.ToString().ToLowerInvariant(), []));
    }

    private void Dataset(Node dataset, string name, string text)
    {
        var path = Path.Combine(pictures, name);
        File.WriteAllText(path, text);
        Import(dataset, path);
    }

    private void Picture(Node dataset, string name, Action<string> draw)
    {
        var path = Path.Combine(pictures, name);
        draw(path);
        Import(dataset, path);
    }

    private void Import(Node dataset, string path) =>
        vault.Save(new DatasetEdited(Guid.NewGuid(), Now, dataset.Id, DatasetFiles.Describe(path), []), () => File.OpenRead(path), _thumbnails);

    private Node View(string name, ViewDefinition definition)
    {
        var view = Add(name, TypeCatalog.View);
        Append(new ViewEdited(Guid.NewGuid(), Now, view.Id, definition, []));
        return view;
    }

    private void Append(Change change) => vault.Append(change);
}
