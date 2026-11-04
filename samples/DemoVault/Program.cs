using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;

namespace Kofrenot.Samples;

/// <summary>
/// Builds a demo vault of made-up data in artifacts\demo, opened by
/// <c>kofrenot.exe --data artifacts\demo\data</c> without asking its pass phrase (remembered for
/// this Windows account; it is <see cref="PassPhrase"/>): projects, notes and task lists, books and
/// people of node types with fields and pictures, a family tree and a book club as graphs, a
/// history to restore, a conflict, unlinked nodes, and its views shown as tabs (<see cref="DemoContent"/>).
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
        var views = new DemoContent(vault, pictures, Start).Write();

        Register(files, folder, access, views);
        var read = Vault.Open(folder, access.Header, access.Keys).Graph;
        Console.WriteLine($"Demo vault: {folder}, read back: {read.Nodes.Count} nodes, {read.Links.Count} links");
        foreach (var view in new[] { views.Family, views.BookClub })
        {
            var node = read.FindNode(view)!;
            var definition = (GraphDefinition)new ViewPlanner(read).Load(node).Definition!;
            Console.WriteLine($"  {node.Name}: {new GraphView(read, definition, view).Read()?.Nodes.Count ?? 0} cards");
        }

        Console.WriteLine($"Open it with: kofrenot.exe --data \"{files.DataDirectory}\" (pass phrase: {PassPhrase})");
        return 0;
    }

    /// <summary>
    /// The vault listed, selected, its pass phrase remembered and its name known; its views open as
    /// tabs: the notes and covers as cards, the people in columns, the projects with their unlinked
    /// nodes beside them, the book club with its link names.
    /// </summary>
    private static void Register(AppFiles files, string folder, VaultAccess access, DemoViews views)
    {
        var settings = new SettingsStore(files.Settings);
        var location = new VaultLocation(folder);
        settings.SetVaults(new VaultList([new RegisteredVault(location, access.Header.Id)]));
        settings.SelectVault(location);

        var protector = new DpapiProtector();
        new KeyMemory(files.RememberedKeys, protector).Remember(access);
        new VaultNameMemory(files.VaultNames, protector).Remember(access.Header.Id, DemoContent.VaultName);

        var vault = access.Header.Id;
        var tabs = new ViewTabs(files.ViewTabs);
        foreach (var view in new[] { views.Projects, views.Family, views.BookClub, views.People, views.Notes, views.Covers })
        {
            tabs.Open(vault, view);
        }

        var states = new ViewStates(files.ViewStates);
        states.SetShowsCards(vault, views.Notes, true);
        states.SetShowsCards(vault, views.Covers, true);
        states.SetShowsDetails(vault, views.People, true);
        states.SetShowsUnlinked(vault, views.Projects, true);
        states.SetShowsLinkNames(vault, views.BookClub, true);
    }
}

/// <summary>The views of the demo vault, opened as tabs.</summary>
internal sealed record DemoViews(Guid Projects, Guid Family, Guid BookClub, Guid People, Guid Notes, Guid Covers);

/// <summary>The demo's clock: each change a minute after the one before, so their order and history read naturally.</summary>
internal sealed class DemoClock(DateTimeOffset start) : TimeProvider
{
    private int _minutes;

    public override DateTimeOffset GetUtcNow() => start.AddMinutes(_minutes++);
}
