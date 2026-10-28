namespace Kofrenot.Core.Tests.Session;

/// <summary>
/// kofrenot for Android: it opens and syncs server vaults, changes their nodes (nodes, links, notes,
/// files), which reach the other devices, and leaves what defines a vault to kofrenot on Windows.
/// </summary>
public sealed class PhoneSessionTests : IDisposable
{
    private const string OnWindows = "This change is made in kofrenot on Windows";
    private const string Grace = "grace@example.com";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKofre _kofre = new();
    private readonly Device _laptop;
    private readonly Device _phone;

    public PhoneSessionTests()
    {
        _laptop = new Device(_kofre);
        _phone = new Device(_kofre, ClientMode.Phone);
    }

    public void Dispose()
    {
        _laptop.Dispose();
        _phone.Dispose();
    }

    [Fact]
    public async Task NodesAndLinks_AreWritten_AndReachTheLaptop()
    {
        await AddLaptopsVaultAsync();
        var session = _phone.Session;
        var kitchen = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Kitchen", Start);
        var knives = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Knives", Start);

        Assert.True(session.Write(_phone.Ask, [new NodeAdded(Guid.NewGuid(), Start, kitchen), new NodeAdded(Guid.NewGuid(), Start, knives)], "Nodes added"));
        Assert.True(session.Write(_phone.Ask, new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, kitchen.Id, knives.Id, Start)), "Linked"));
        Assert.True(session.Rename(_phone.Ask, new NodeRenamed(Guid.NewGuid(), Start, kitchen.Id, "Cuisine", [kitchen.Id])));
        Assert.True(session.Write(_phone.Ask, new NodeColored(Guid.NewGuid(), Start, kitchen.Id, "yellow", []), "Coloured"));
        Assert.True(session.Write(_phone.Ask, new PinPlanner(_phone.Vault.Graph).Toggle(kitchen), "Pinned"));
        Assert.True(session.Write(_phone.Ask, new ChangesRemoved(Guid.NewGuid(), Start, [knives.Id]), "Node deleted"));
        await _phone.SyncAsync();
        await _laptop.SyncAsync();

        var graph = _laptop.Vault.Graph;
        Assert.Equal("Cuisine", Assert.Single(graph.Nodes).Name);
        Assert.Equal(NodeColor.Yellow, graph.ColorOf(kitchen.Id));
        Assert.Single(graph.PinnedNodes);
        Assert.True(session.IsPhone);
        Assert.Equal(ClientMode.Phone, session.Mode);
        Assert.False(session.CanChangePassPhrase);
        Assert.Empty(_phone.Ask.Told);
    }

    [Fact]
    public async Task WhatDefinesTheVault_IsLeftToWindows()
    {
        await AddLaptopsVaultAsync();
        var session = _phone.Session;
        var alias = new AliasAdded(Guid.NewGuid(), Start, TypeCatalog.Contains.Id, IsReverse: false, "holds");
        _laptop.Session.Write(_laptop.Ask, alias, "Alias added");
        await _laptop.SyncAsync();
        await _phone.SyncAsync();
        var files = Assert.Single(_kofre.Stores).Files.Count;

        Assert.False(session.Write(_phone.Ask, new VaultNamed(Guid.NewGuid(), Start, "Mine", []), "Vault renamed"));
        Assert.False(session.Write(_phone.Ask, new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [], []), "Node type added"));
        Assert.False(session.Write(_phone.Ask, new ChangesRemoved(Guid.NewGuid(), Start, [alias.Id]), "Alias removed"));
        await _phone.SyncAsync();

        Assert.Equal("Personal", _phone.Vault.Graph.VaultName);
        Assert.Equal(3, _phone.Ask.Told.Count(told => told.StartsWith(OnWindows, StringComparison.Ordinal)));
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
    public async Task AVersion_IsRestoredOnThePhone()
    {
        await AddLaptopsVaultAsync();
        var node = new Node(Guid.NewGuid(), TypeCatalog.Note.Id, "Ideas", Start);
        _laptop.Session.Write(_laptop.Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added");
        _laptop.Session.Rename(_laptop.Ask, new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), node.Id, "Plans", [node.Id]));
        await _laptop.SyncAsync();
        await _phone.SyncAsync();
        var graph = _phone.Vault.Graph;
        var plan = new RestorePlanner(graph).Plan(node, Assert.Single(NodeHistory.Of(graph, node), entry => entry.Id == node.Id))!;

        Assert.True(await _phone.Session.Restore(_phone.Ask, plan));

        Assert.Equal("Ideas", graph.FindNode(node.Id)!.Name);
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
    public async Task TextSharedOnThePhone_BecomesANote_OnTheLaptop()
    {
        await AddLaptopsVaultAsync();
        var note = new SharedNotePlanner(_phone.Vault.Graph).Preview("Buy spice\nand water");

        var node = _phone.Session.AddNote(_phone.Ask, note);
        await _phone.SyncAsync();
        await _laptop.SyncAsync();

        Assert.NotNull(node);
        var version = Assert.Single(_laptop.Vault.Graph.NoteVersionsOf(node.Id));
        Assert.True(await _laptop.Vault.FetchAsync(version));
        Assert.Equal("Buy spice\nand water", _laptop.Vault.ReadNote(version.Id));
        Assert.Equal("Buy spice", _laptop.Vault.Graph.FindNode(node.Id)!.Name);
    }

    [Fact]
    public void WithoutAVault_NoNoteIsAdded() =>
        Assert.Null(_phone.Session.AddNote(_phone.Ask, new SharedNote("a", "a", false)));

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

        Assert.Equal(5, _phone.Ask.Told.Count(told => told.StartsWith(OnWindows, StringComparison.Ordinal)));
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
        Assert.Contains(_phone.Ask.Told, told => told.StartsWith(OnWindows, StringComparison.Ordinal));
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

    [Fact]
    public async Task AVault_IsShared_AndARoleChanged_OnThePhone_ButNoMemberRemoved()
    {
        using var grace = new Device(_kofre);
        await grace.SignInAsync(Grace);
        grace.Ask.PassPhrase = "grace pass phrase";
        grace.Session.SetAccountPassPhrase(grace.Ask);
        await AddLaptopsVaultAsync();
        var session = _phone.Session;
        Assert.True(session.CanShare);

        _phone.Ask.ShareWith = (Grace, MemberRole.Writer);
        await session.Share(_phone.Ask);
        var member = session.Members.Single(member => member.Email == Grace);
        await session.ChangeRole(_phone.Ask, member);

        Assert.Equal(MemberRole.Reader, session.Members.Single(member => member.Email == Grace).Role);
        Assert.True(session.CanManageMembers);
        Assert.False(session.CanRemoveMembers);
        await session.RemoveMember(_phone.Ask, member);
        Assert.Equal(2, session.Members.Count);
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
