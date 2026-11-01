namespace Kofrenot.Core.Tests.Session;

/// <summary>A file picked (a stream, as the phone gives it) imported as a dataset's new version, or set as an image field's image.</summary>
public sealed class FileImportSessionTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKofre _kofre = new();
    private readonly Device _device;

    public FileImportSessionTests()
    {
        _device = new Device(_kofre);
        _device.CreateFolderVault();
    }

    private VaultSession Session => _device.Session;

    private ScriptedQuestions Ask => _device.Ask;

    public void Dispose() => _device.Dispose();

    [Fact]
    public async Task AFilePicked_IsTheDatasetsNewVersion_ButNotTwice()
    {
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);

        Assert.True(await Session.ImportVersion(Ask, survey, FileOf("survey.csv", "a,b")));
        Assert.True(await Session.ImportVersion(Ask, survey, FileOf("survey.csv", "a,b,c")));
        Assert.False(await Session.ImportVersion(Ask, survey, FileOf("survey.csv", "a,b,c")));

        var versions = _device.Vault.Graph.DatasetVersionsOf(survey.Id);
        var latest = Assert.Single(versions);
        Assert.Equal(5, latest.File.Size);
        Assert.True(_device.Vault.Datasets.Exists(latest.Id));
        Assert.Equal("This file is the dataset's already: nothing imported.", Ask.Told[^1]);
        Assert.Contains(_device.Log.Entries, entry => entry.Message == "Dataset import: the same file, nothing imported");
    }

    [Fact]
    public async Task AFilePicked_ResolvesAConflict_EvenTheSameFileAgain()
    {
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);
        var picked = FileOf("survey.csv", "a,b");
        Assert.True(await Session.ImportVersion(Ask, survey, picked));
        var mine = Assert.Single(_device.Vault.Graph.DatasetVersionsOf(survey.Id));
        var theirs = new DatasetEdited(Guid.NewGuid(), Start, survey.Id, new DatasetFile("theirs.csv", 1, new string('c', 64)), []);
        Assert.True(Session.Write(Ask, theirs, "Dataset edited"));

        Assert.True(await Session.ImportVersion(Ask, survey, picked));

        var resolved = Assert.Single(_device.Vault.Graph.DatasetVersionsOf(survey.Id));
        Assert.Equal(mine.File, resolved.File);
        Assert.Equal(new HashSet<Guid> { mine.Id, theirs.Id }, resolved.Replaces.ToHashSet());
    }

    [Fact]
    public async Task AnImagePicked_IsTheFieldsImage()
    {
        var (bruce, portrait) = Person();

        Assert.True(await Session.SetImage(Ask, bruce, portrait, FileOf("bruce.png", "png bytes")));

        var field = Assert.Single(NodeFields.Of(_device.Vault.Graph, bruce.Id));
        var image = field.Image!.Image!;
        Assert.Equal("bruce.png", image.File.Name);
        Assert.True(_device.Vault.Datasets.Exists(image.Id));
    }

    [Fact]
    public async Task AFileNotAnImage_IsRefused_AndTold()
    {
        var (bruce, portrait) = Person();

        Assert.False(await Session.SetImage(Ask, bruce, portrait, FileOf("notes.txt", "text")));

        Assert.Null(Assert.Single(NodeFields.Of(_device.Vault.Graph, bruce.Id)).Image);
        Assert.StartsWith("“notes.txt” is not an image kofrenot shows", Ask.Told[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileThatCannotBeRead_IsNeitherImportedNorSet_AndTold()
    {
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);
        var (bruce, portrait) = Person();
        var locked = new ImportFile("locked.png", 3, () => throw new IOException("Locked"));

        Assert.False(await Session.ImportVersion(Ask, survey, locked));
        Assert.False(await Session.SetImage(Ask, bruce, portrait, locked));

        Assert.Empty(_device.Vault.Graph.DatasetVersionsOf(survey.Id));
        Assert.Equal(2, Ask.Told.Count(told => told.StartsWith("Cannot import", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task WithoutAVault_NothingIsImported()
    {
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);
        var (bruce, portrait) = Person();
        Session.Close();

        Assert.False(await Session.ImportVersion(Ask, survey, FileOf("survey.csv", "a")));
        Assert.False(await Session.SetImage(Ask, bruce, portrait, FileOf("bruce.png", "png")));
    }

    private static ImportFile FileOf(string name, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new ImportFile(name, bytes.Length, () => new MemoryStream(bytes));
    }

    private (Node Node, NodeField Field) Person()
    {
        Assert.True(Session.Write(Ask, new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [new FieldDefinition("portrait", "Portrait", FieldKind.Image)], []), "Node type defined"));
        var bruce = AddNode("Bruce", "person");
        return (bruce, Assert.Single(NodeFields.Of(_device.Vault.Graph, bruce.Id)));
    }

    private Node AddNode(string name, string type)
    {
        var node = new Node(Guid.NewGuid(), type, name, Start);
        Assert.True(Session.Write(Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added"));
        return node;
    }
}
