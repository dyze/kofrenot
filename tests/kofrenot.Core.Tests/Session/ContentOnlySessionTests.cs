namespace Kofrenot.Core.Tests.Session;

/// <summary>
/// A client that edits content only (kofrenot for Android): it opens and syncs server vaults, saves
/// notes and imports files, which reach the other devices, and makes no other change.
/// </summary>
public sealed class ContentOnlySessionTests : IDisposable
{
    private const string ContentOnly = "This kofrenot edits notes and files only";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKofre _kofre = new();
    private readonly Device _laptop;
    private readonly Device _phone;

    public ContentOnlySessionTests()
    {
        _laptop = new Device(_kofre);
        _phone = new Device(_kofre, ClientMode.Content);
    }

    public void Dispose()
    {
        _laptop.Dispose();
        _phone.Dispose();
    }

    [Fact]
    public async Task OnlyContent_IsWritten_NothingElse()
    {
        await AddLaptopsVaultAsync();
        var files = Assert.Single(_kofre.Stores).Files.Count;
        var session = _phone.Session;

        Assert.False(session.Write(_phone.Ask, new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), "generic", "Kitchen", Start)), "Node added"));
        await _phone.SyncAsync();

        Assert.Equal("Personal", _phone.Vault.Graph.VaultName);
        Assert.True(session.EditsContentOnly);
        Assert.Equal(ClientMode.Content, session.Mode);
        Assert.False(session.IsReadOnly);
        Assert.False(session.CanShare);
        Assert.False(session.CanLeave);
        Assert.False(session.CanChangePassPhrase);
        Assert.Contains(_phone.Ask.Told, told => told.StartsWith(ContentOnly, StringComparison.Ordinal));
        Assert.Empty(_phone.Vault.Graph.Nodes);
        Assert.Equal(files, Assert.Single(_kofre.Stores).Files.Count);
    }

    [Fact]
    public async Task ChangesOfAnotherDevice_AreRead_AfterASync()
    {
        await AddLaptopsVaultAsync();
        var node = new Node(Guid.NewGuid(), "generic", "Kitchen", Start);
        _laptop.Session.Write(_laptop.Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added");
        await _laptop.SyncAsync();

        await _phone.SyncAsync();

        Assert.NotNull(_phone.Vault.Graph.FindNode(node.Id));
    }

    [Fact]
    public async Task ANoteEditedOnThePhone_ReachesTheLaptop()
    {
        await AddLaptopsVaultAsync();
        var node = new Node(Guid.NewGuid(), TypeCatalog.Note.Id, "Ideas", Start);
        var first = new NoteEdited(Guid.NewGuid(), Start, node.Id, []);
        _laptop.Session.Write(_laptop.Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added");
        _laptop.Session.SaveNote(_laptop.Ask, first, "- [ ] spice");
        await _laptop.SyncAsync();
        await _phone.SyncAsync();
        var edited = new NoteEdited(Guid.NewGuid(), Start.AddMinutes(1), node.Id, [first.Id]);

        Assert.True(_phone.Session.SaveNote(_phone.Ask, edited, "- [x] spice"));
        await _phone.SyncAsync();
        await _laptop.SyncAsync();

        Assert.Equal([edited.Id], _laptop.Vault.Graph.NoteVersionsOf(node.Id).Select(version => version.Id));
        Assert.True(await _laptop.Vault.FetchAsync(edited));
        Assert.Equal("- [x] spice", _laptop.Vault.ReadNote(edited.Id));
    }

    [Fact]
    public async Task AFileAddedOnThePhone_ReachesTheLaptop()
    {
        await AddLaptopsVaultAsync();
        var bytes = "a,b"u8.ToArray();
        var preview = new FileImportPlanner(_phone.Vault.Graph).Preview([new ImportFile("survey.csv", bytes.Length, () => new MemoryStream(bytes))]);

        Assert.Single(await _phone.Session.ImportFiles(_phone.Ask, preview));
        await _phone.SyncAsync();
        await _laptop.SyncAsync();

        var node = Assert.Single(_laptop.Vault.Graph.Nodes, node => node.Name == "survey");
        using var target = new MemoryStream();
        Assert.True(await _laptop.Session.ExportDataset(_laptop.Ask, _laptop.Vault.Graph.DatasetVersionsOf(node.Id).Single(), target));
        Assert.Equal(bytes, target.ToArray());
    }

    [Fact]
    public async Task ADatasetOfAnotherDevice_IsExported_OnDemand()
    {
        await AddLaptopsVaultAsync();
        var node = new Node(Guid.NewGuid(), TypeCatalog.Dataset.Id, "Survey", Start);
        var source = _laptop.Folder("survey.csv");
        File.WriteAllText(source, "a,b");
        var version = new DatasetEdited(Guid.NewGuid(), Start, node.Id, DatasetFiles.Describe(source), []);
        _laptop.Session.Write(_laptop.Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added");
        await _laptop.Session.SaveDataset(_laptop.Ask, version, () => File.OpenRead(source));
        await _laptop.SyncAsync();
        await _phone.SyncAsync();
        using var target = new MemoryStream();

        Assert.True(await _phone.Session.ExportDataset(_phone.Ask, version, target));

        Assert.Equal("a,b", System.Text.Encoding.UTF8.GetString(target.ToArray()));
    }

    [Fact]
    public async Task NothingIsChanged_OnTheServer()
    {
        await _phone.SignInAsync();
        var session = _phone.Session;
        var store = await _phone.Server.Client!.CreateStoreAsync();

        session.CreateOnServer(_phone.Ask);
        await session.AddFromServer(_phone.Ask, new ServerStore(store, StoreRole.Owner, 0, 0));
        await session.MoveToServer(_phone.Ask, new RegisteredVault(new VaultLocation(_phone.Folder("vault"))));
        Assert.False(await session.DeleteFromServer(_phone.Ask, new RegisteredVault(new VaultLocation("x", new StoreAddress(FakeKofre.Address, store))), Device.ServerPassword));
        session.SetAccountPassPhrase(_phone.Ask);

        Assert.Equal(5, _phone.Ask.Told.Count(told => told.StartsWith(ContentOnly, StringComparison.Ordinal)));
        Assert.Empty(session.Vaults);
        Assert.Empty(_phone.Ask.Asked);
        Assert.Empty(_kofre.StoreOf(store).Files);
    }

    [Fact]
    public void AFolderVault_IsNotCreated()
    {
        var folder = _phone.Folder("vault");
        Directory.CreateDirectory(folder);

        _phone.Session.Register(_phone.Ask, folder);

        Assert.Null(_phone.Session.Current);
        Assert.Empty(Directory.GetFiles(folder));
        Assert.Contains(_phone.Ask.Told, told => told.StartsWith(ContentOnly, StringComparison.Ordinal));
    }

    [Fact]
    public void AVaultThatNeedsAnUpgrade_IsNotOpened()
    {
        var folder = _phone.Folder("old");
        var id = Guid.NewGuid();
        var (wrapped, _) = KeyWrap.Create(ScriptedQuestions.FirstPassPhrase, id.ToByteArray(), 10);
        Directory.CreateDirectory(Path.Combine(folder, VaultFolder.ChangesFolder));
        File.WriteAllText(VaultFolder.HeaderOf(folder), $"{{\"format\":5,\"id\":\"{id}\",\"key\":{{\"salt\":\"{Convert.ToBase64String(wrapped.Salt)}\",\"iterations\":10,\"sealed\":\"{Convert.ToBase64String(wrapped.Sealed)}\"}}}}");

        _phone.Session.Register(_phone.Ask, folder);

        Assert.Null(_phone.Session.Current);
        Assert.Null(_phone.Session.OlderFormat);
        Assert.Contains(_phone.Ask.Told, told => told.Contains("It needs an upgrade"));
        Assert.DoesNotContain(nameof(ISessionQuestions.AskToUpgrade), _phone.Ask.Asked);
    }

    [Fact]
    public void AServerVaultOfItsOwnPassPhrase_WithoutAnAccountPassPhrase_IsNotOpened()
    {
        OpenUnattachedServerVault();

        Assert.Null(_phone.Session.Current);
        Assert.Contains(_phone.Ask.Told, told => told.Contains("Choose your server account's pass phrase in kofrenot on Windows"));
    }

    [Fact]
    public async Task AServerVaultOfItsOwnPassPhrase_IsNotHandedToTheAccount()
    {
        await _laptop.SignInAsync();
        _laptop.Ask.PassPhrase = "account pass phrase";
        _laptop.Session.SetAccountPassPhrase(_laptop.Ask);
        await _phone.SignInAsync();
        Assert.NotNull(_phone.Server.Identity);

        OpenUnattachedServerVault();

        Assert.Null(_phone.Session.Current);
        Assert.Contains(_phone.Ask.Told, told => told.Contains("It still opens with its own pass phrase"));
        Assert.DoesNotContain(nameof(ISessionQuestions.AskAccount), _phone.Ask.Asked);
    }

    /// <summary>The laptop's new server vault, added on the phone with the account's pass phrase.</summary>
    private async Task AddLaptopsVaultAsync()
    {
        await _laptop.SignInAsync();
        _laptop.Session.CreateOnServer(_laptop.Ask);
        await _laptop.SyncAsync();
        await _phone.SignInAsync();

        await _phone.Session.AddFromServer(_phone.Ask, Assert.Single((await _phone.Session.StoresToAdd(_phone.Ask))!));

        Assert.NotNull(_phone.Session.Current);
    }

    // A server vault as kofrenot 0.6 moved it: opened by its own pass phrase, not its account's.
    private void OpenUnattachedServerVault()
    {
        var store = new StoreAddress(FakeKofre.Address, Guid.NewGuid());
        var location = new VaultLocation(_phone.Files.MirrorOf(store.Id), store);
        VaultFolder.Create(location.Folder, ScriptedQuestions.FirstPassPhrase, 10);
        _phone.Settings.SetVaults(_phone.Settings.Vaults.Add(new RegisteredVault(location)));
        _phone.Settings.SelectVault(location);

        _phone.Session.Open(_phone.Ask);
    }
}
