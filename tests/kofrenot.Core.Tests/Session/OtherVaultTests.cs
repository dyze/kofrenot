using System.Text.Json.Nodes;

namespace Kofrenot.Core.Tests.Session;

/// <summary>Another registered vault read without switching to it: its types, to copy them.</summary>
public sealed class OtherVaultTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKofre _kofre = new();
    private readonly Device _device;
    private readonly RegisteredVault _first;

    public OtherVaultTests()
    {
        _device = new Device(_kofre);
        var folder = _device.CreateFolderVault("first");
        Session.Write(Ask, new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", "Contact", null, [new FieldDefinition("born", "Born", FieldKind.Date)], []), "Node type added");
        _first = Session.Selected!;
        Assert.Equal(folder, _first.Location.Folder);
        _device.CreateFolderVault("second");
        Ask.Asked.Clear();
    }

    private VaultSession Session => _device.Session;

    private ScriptedQuestions Ask => _device.Ask;

    public void Dispose() => _device.Dispose();

    [Fact]
    public void AnotherVaultsTypes_AreRead_AfterItsPassPhrase_WithoutSwitching()
    {
        var types = Session.GraphOf(Ask, _first)!.NodeTypes.All;

        var person = Assert.Single(types);
        Assert.Equal(("person", "Person"), (person.Type.Id, person.Type.Name));
        Assert.Equal([nameof(ScriptedQuestions.AskPassPhrase)], Ask.Asked);
        Assert.Empty(_device.Vault.Graph.NodeTypes.All);
        Assert.NotEqual(_first.Location, Session.Selected!.Location);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.EndsWith("read, not switched to", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NodesOfAnotherVault_AreImported_TheirNotesAndFilesSealedAgainHere()
    {
        var source = Session.VaultOf(Ask, _first)!;
        var (ideas, survey) = (new Node(Guid.NewGuid(), "note", "Ideas", Start), new Node(Guid.NewGuid(), "dataset", "Survey", Start));
        source.Append(new NodeAdded(Guid.NewGuid(), Start, ideas));
        source.Append(new NodeAdded(Guid.NewGuid(), Start, survey));
        source.Save(new NoteEdited(Guid.NewGuid(), Start, ideas.Id, []), "# Ideas");
        var file = _device.Folder("survey.csv");
        File.WriteAllText(file, "a,b\n");
        source.Save(new DatasetEdited(Guid.NewGuid(), Start, survey.Id, DatasetFiles.Describe(file), []), () => File.OpenRead(file));
        var plan = new VaultImportPlanner(_device.Vault.Graph, VaultContent.Of(_device.Vault)).Plan(source.Graph, VaultContent.Of(source), "first", [ideas, survey]);

        Assert.True(await Session.ImportFromVault(Ask, source, plan.WriteFor(_ => RecordChoice.Import), "Nodes imported (2 new)"));

        var here = _device.Vault;
        Assert.Equal("# Ideas", here.Notes.Read(Assert.Single(here.Graph.NoteVersionsOf(ideas.Id)).Id));
        var exported = _device.Folder("exported.csv");
        here.Datasets.Export(Assert.Single(here.Graph.DatasetVersionsOf(survey.Id)).Id, exported);
        Assert.Equal("a,b\n", File.ReadAllText(exported));
        Assert.Contains(_device.Log.Entries, entry => entry.Message == "Nodes imported (2 new)");
    }

    [Fact]
    public void ItsPassPhrase_IsRemembered_WhenAsked_AndUsedNextTime()
    {
        Ask.Remember = true;
        Session.GraphOf(Ask, _first);
        Ask.Asked.Clear();

        Assert.Single(Session.GraphOf(Ask, _first)!.NodeTypes.All);
        Assert.Empty(Ask.Asked);
    }

    [Fact]
    public void ACancelledUnlock_ReadsNothing()
    {
        Ask.Cancels = true;

        Assert.Null(Session.GraphOf(Ask, _first));
    }

    [Fact]
    public void TheOpenVault_IsNotReadThisWay()
    {
        Assert.Throws<ArgumentException>(() => Session.GraphOf(Ask, Session.Selected!));
    }

    [Fact]
    public void AVaultThatCannotBeRead_IsTold()
    {
        File.WriteAllText(VaultFolder.HeaderOf(_first.Location.Folder), "not a header");

        Assert.Null(Session.GraphOf(Ask, _first));
        Assert.Contains(Ask.Told, told => told.StartsWith("The vault in", StringComparison.Ordinal));
    }

    [Fact]
    public void AVaultOfANewerFormat_AsksToUpdate()
    {
        var header = VaultFolder.HeaderOf(_first.Location.Folder);
        var json = JsonNode.Parse(File.ReadAllText(header))!;
        foreach (var level in new[] { "format", "oldestReader", "oldestWriter" })
        {
            json[level] = VaultFormat.Current + 1;
        }

        File.WriteAllText(header, json.ToJsonString());

        Assert.Null(Session.GraphOf(Ask, _first));
        Assert.Contains("This vault was written by a newer kofrenot: update kofrenot to read it.", Ask.Told);
    }
}
