namespace Kofrenot.Core.Tests.Session;

/// <summary>The session over server vaults, between devices and accounts on one <see cref="FakeKofre"/>.</summary>
public sealed class ServerVaultSessionTests : IDisposable
{
    private const string Grace = "grace@example.com";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKofre _kofre = new();
    private readonly Device _laptop;
    private readonly Device _desktop;
    private readonly Device _graces;

    public ServerVaultSessionTests()
    {
        _laptop = new Device(_kofre);
        _desktop = new Device(_kofre);
        _graces = new Device(_kofre);
    }

    public void Dispose()
    {
        _laptop.Dispose();
        _desktop.Dispose();
        _graces.Dispose();
    }

    [Fact]
    public async Task SignedOut_TheServerIsNotUsed()
    {
        var ask = _laptop.Ask;
        var session = _laptop.Session;

        session.CreateOnServer(ask);
        Assert.Null(await session.StoresToAdd(ask));
        await session.AddFromServer(ask, new ServerStore(Guid.NewGuid(), StoreRole.Owner, 1, 1));
        await session.MoveToServer(ask, new RegisteredVault(new VaultLocation(_laptop.Folder("vault"))));
        Assert.False(await session.DeleteFromServer(ask, new RegisteredVault(new VaultLocation("x", new StoreAddress(FakeKofre.Address, Guid.NewGuid()))), "x"));
        session.SetAccountPassPhrase(ask);

        Assert.Equal(6, ask.Told.Count(told => told.Contains("Sign in to a server first")));
    }

    [Fact]
    public async Task AServerTooOld_KeepsNoAccountPassPhrase()
    {
        _kofre.Version = null;
        await _laptop.SignInAsync();

        _laptop.Session.CreateOnServer(_laptop.Ask);
        _laptop.Session.SetAccountPassPhrase(_laptop.Ask);

        Assert.Equal(2, _laptop.Ask.Told.Count(told => told.Contains("needs 0.2")));
    }

    [Fact]
    public async Task AVaultCreatedOnTheServer_IsOpened_AndUploaded()
    {
        await CreateServerVaultAsync(_laptop);

        Assert.Equal("Personal", _laptop.Vault.Graph.VaultName);
        Assert.Single(_laptop.Ask.RecoveryKeys);
        var store = Assert.Single(_kofre.Stores);
        Assert.Contains(store.Files, file => file.Path == VaultFolder.HeaderFile);
        Assert.False(_laptop.Session.CanChangePassPhrase);
    }

    [Fact]
    public async Task AServerVaultCreationCancelled_MakesNothing()
    {
        await _laptop.SignInAsync();
        _laptop.Ask.Cancels = true;

        _laptop.Session.CreateOnServer(_laptop.Ask);

        Assert.Empty(_kofre.Stores);
        Assert.Null(_laptop.Session.Current);
    }

    [Fact]
    public async Task AnotherDevice_AddsTheVault_WithTheAccountsPassPhrase()
    {
        await _desktop.SignInAsync();
        Assert.Null(await _desktop.Session.StoresToAdd(_desktop.Ask));
        Assert.Contains(_desktop.Ask.Told, told => told.Contains("no vault on this server yet"));
        await CreateServerVaultAsync(_laptop);
        await _desktop.SyncAsync();

        var stores = await _desktop.Session.StoresToAdd(_desktop.Ask);
        await _desktop.Session.AddFromServer(_desktop.Ask, Assert.Single(stores!));

        Assert.Equal("Personal", _desktop.Vault.Graph.VaultName);
        Assert.Null(await _desktop.Session.StoresToAdd(_desktop.Ask));
        Assert.Contains(_desktop.Ask.Told, told => told.Contains("all in the list already"));
    }

    [Fact]
    public async Task AnEmptyStore_BecomesANewVault()
    {
        await _laptop.SignInAsync();
        await _laptop.Server.Client!.CreateStoreAsync();

        var store = Assert.Single((await _laptop.Session.StoresToAdd(_laptop.Ask))!);
        await _laptop.Session.AddFromServer(_laptop.Ask, store);

        Assert.Contains(nameof(ISessionQuestions.AskNewOnServer), _laptop.Ask.Asked);
        Assert.Equal(store.Id, _laptop.Session.Selected!.Location.Store!.Id);
    }

    [Fact]
    public async Task AStoreThatCannotBeDownloaded_IsNotAdded()
    {
        await _laptop.SignInAsync();
        _kofre.Offline = true;

        await _laptop.Session.AddFromServer(_laptop.Ask, new ServerStore(Guid.NewGuid(), StoreRole.Owner, 3, 100));

        Assert.Contains(_laptop.Ask.Told, told => told.StartsWith("The vault cannot be downloaded", StringComparison.Ordinal));
        Assert.Empty(_laptop.Session.Vaults);
    }

    [Fact]
    public async Task AFolderVault_MovesToTheServer_ThenToTheAccountsPassPhrase()
    {
        var folder = _laptop.CreateFolderVault();
        await _laptop.SignInAsync();

        await _laptop.Session.MoveToServer(_laptop.Ask, _laptop.Session.Selected!);

        Assert.True(_laptop.Session.Selected!.Location.IsOnServer);
        Assert.NotNull(_laptop.Session.Current);
        Assert.Contains(_laptop.Ask.Told, told => told.Contains($"Its folder {folder} is left as it was"));
        Assert.Contains(nameof(ISessionQuestions.AskAccount), _laptop.Ask.Asked);
        Assert.NotEmpty(Assert.Single(_kofre.Stores).Files);
    }

    [Fact]
    public async Task AMoveTheServerRefuses_IsTold()
    {
        _laptop.CreateFolderVault();
        await _laptop.SignInAsync();
        _kofre.Failing.Add("POST v1/stores");

        await _laptop.Session.MoveToServer(_laptop.Ask, _laptop.Session.Selected!);

        Assert.Contains(_laptop.Ask.Told, told => told.StartsWith("The vault cannot be moved to the server", StringComparison.Ordinal));
        Assert.False(_laptop.Session.Selected!.Location.IsOnServer);
    }

    [Fact]
    public async Task AServerVault_IsDeleted_WithThePassword()
    {
        await CreateServerVaultAsync(_laptop);
        var vault = _laptop.Session.Selected!;

        Assert.False(await _laptop.Session.DeleteFromServer(_laptop.Ask, vault, "wrong"));
        Assert.Contains(_laptop.Ask.Told, told => told.StartsWith("Wrong password", StringComparison.Ordinal));
        Assert.True(await _laptop.Session.DeleteFromServer(_laptop.Ask, vault, Device.ServerPassword));

        Assert.True(Assert.Single(_kofre.Stores).IsTrashed);
        Assert.Empty(_laptop.Session.Vaults);
        Assert.False(Directory.Exists(vault.Location.Folder));
    }

    [Fact]
    public async Task TheAccountsPassPhrase_IsChosen_ThenChanged_ForEveryDevice()
    {
        await _laptop.SignInAsync();
        _laptop.Session.SetAccountPassPhrase(_laptop.Ask);
        Assert.Single(_laptop.Ask.RecoveryKeys);
        _laptop.Session.CreateOnServer(_laptop.Ask);
        await _laptop.SyncAsync();

        _laptop.Session.SetAccountPassPhrase(_laptop.Ask);
        Assert.Contains(nameof(ISessionQuestions.AskToChangeAccount), _laptop.Ask.Asked);

        await _desktop.SignInAsync();
        _desktop.Ask.PassPhrase = _laptop.Ask.NewPassPhrase;
        await _desktop.Session.AddFromServer(_desktop.Ask, Assert.Single((await _desktop.Session.StoresToAdd(_desktop.Ask))!));
        Assert.NotNull(_desktop.Session.Current);
    }

    [Fact]
    public async Task TheAccountsPassPhrase_NotGiven_ChangesNothing()
    {
        await _laptop.SignInAsync();
        _laptop.Ask.Cancels = true;

        _laptop.Session.SetAccountPassPhrase(_laptop.Ask);

        Assert.Null(_laptop.Server.Identity);
        Assert.Contains(_laptop.Log.Entries, entry => entry.Message.Contains("pass phrase not given"));
    }

    [Fact]
    public async Task TheAccountsRecoveryKey_OpensAServerVault_ThenAsksANewPassPhrase()
    {
        await CreateServerVaultAsync(_laptop);
        _laptop.Session.Close();
        (_laptop.Ask.UsesRecoveryKey, _laptop.Ask.RecoveryKey) = (true, _laptop.Ask.RecoveryKeys.Single());

        _laptop.Session.Open(_laptop.Ask);

        Assert.NotNull(_laptop.Session.Current);
        Assert.Contains(nameof(ISessionQuestions.AskToChangeAccount), _laptop.Ask.Asked);
    }

    [Fact]
    public async Task TheServerFailing_IsTold_InWords()
    {
        await _laptop.SignInAsync();
        _kofre.Offline = true;

        Assert.Null(await _laptop.Session.StoresToAdd(_laptop.Ask));

        Assert.Contains(_laptop.Ask.Told, told => told.Contains("The server cannot be reached"));
    }

    [Fact]
    public async Task AVault_IsShared_AsAWriter_ThenMadeAReader()
    {
        await ShareWithGraceAsync(MemberRole.Writer);

        Assert.Equal(2, _laptop.Session.Members.Count);
        Assert.True(_graces.Session.CanLeave);
        Assert.False(_graces.Session.IsReadOnly);
        Assert.Contains(_laptop.Ask.Told, told => told.StartsWith($"The vault is shared with {Grace}", StringComparison.Ordinal));
        Assert.True(_graces.Session.Write(_graces.Ask, NewNode("Dune"), "Node added"));

        await _laptop.Session.ChangeRole(_laptop.Ask, _laptop.Session.Members.Single(member => member.Email == Grace));
        await _laptop.SyncAsync();
        await _graces.SyncAsync();
        _graces.Session.Refresh();

        Assert.True(_graces.Session.IsReadOnly);
        Assert.False(_graces.Session.Write(_graces.Ask, NewNode("Arrakis"), "Node added"));
        Assert.Contains(_graces.Ask.Told, told => told.Contains("read only"));
    }

    [Fact]
    public async Task SharingCancelled_SharesNothing()
    {
        await CreateServerVaultAsync(_laptop);
        Assert.True(_laptop.Session.CanShare);

        await _laptop.Session.Share(_laptop.Ask);

        Assert.Empty(_laptop.Session.Members);
        Assert.Contains(_laptop.Log.Entries, entry => entry.Message.Contains("Sharing cancelled"));
    }

    [Fact]
    public async Task ARemovedMember_LosesAccess_AndTheKeyChanges()
    {
        await ShareWithGraceAsync(MemberRole.Writer);
        var store = Assert.Single(_kofre.Stores);
        Assert.True(_laptop.Session.CanRemoveMembers);

        _laptop.Ask.Agrees = false;
        await _laptop.Session.RemoveMember(_laptop.Ask, _laptop.Session.Members.Single(member => member.Email == Grace));
        Assert.Equal(2, store.Members.Count);

        _laptop.Ask.Agrees = true;
        await _laptop.Session.RemoveMember(_laptop.Ask, _laptop.Session.Members.Single(member => member.Email == Grace));

        Assert.Single(store.Members);
        Assert.Single(_laptop.Session.Members);
        Assert.NotNull(store.Fence);
    }

    [Fact]
    public async Task AMemberWhoLeft_IsRotatedAway_AtTheOwnersNextOpening()
    {
        await ShareWithGraceAsync(MemberRole.Reader);
        var store = Assert.Single(_kofre.Stores);

        await _graces.Session.Leave(_graces.Ask);
        Assert.Empty(_graces.Session.Vaults);
        _laptop.Session.Close();
        _laptop.Session.Open(_laptop.Ask);
        await Settled(() => store.Fence is not null);

        Assert.Single(_laptop.Session.Members);
    }

    [Fact]
    public async Task ANewerKey_IsTakenUpByTheOwnersOtherDevice()
    {
        await ShareWithGraceAsync(MemberRole.Writer);
        await _desktop.SignInAsync();
        await _desktop.Session.AddFromServer(_desktop.Ask, (await _desktop.Session.StoresToAdd(_desktop.Ask))!.Single());

        await _laptop.Session.RemoveMember(_laptop.Ask, _laptop.Session.Members.Single(member => member.Email == Grace));
        await _desktop.SyncAsync();

        Assert.Contains(_desktop.Log.Entries, entry => entry.Message.Contains("taken up"));
        Assert.True(_desktop.Session.Write(_desktop.Ask, NewNode("Dune"), "Node added"));
    }

    [Fact]
    public async Task ANoteOfAnotherDevice_IsDownloadedWhenRead()
    {
        await CreateServerVaultAsync(_laptop);
        var node = new Node(Guid.NewGuid(), TypeCatalog.Note.Id, "Ideas", Start);
        var note = new NoteEdited(Guid.NewGuid(), Start, node.Id, []);
        _laptop.Session.Write(_laptop.Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added");
        _laptop.Session.SaveNote(_laptop.Ask, note, "# Spice");
        await _laptop.SyncAsync();
        await _desktop.SignInAsync();
        await _desktop.Session.AddFromServer(_desktop.Ask, (await _desktop.Session.StoresToAdd(_desktop.Ask))!.Single());
        var changed = 0;
        _desktop.Session.Changed += () => changed++;

        Assert.Null(_desktop.Vault.ReadNote(note.Id));
        Assert.True(await _desktop.Vault.FetchAsync(note));
        _desktop.Ui.RunAll();

        Assert.Equal("# Spice", _desktop.Vault.ReadNote(note.Id));
        Assert.True(changed > 0);
    }

    [Fact]
    public async Task ExportingANoteNotDownloaded_Offline_IsTold()
    {
        await CreateServerVaultAsync(_laptop);
        var node = new Node(Guid.NewGuid(), TypeCatalog.Dataset.Id, "Survey", Start);
        var source = _laptop.Folder("survey.csv");
        File.WriteAllText(source, "a,b");
        var version = new DatasetEdited(Guid.NewGuid(), Start, node.Id, DatasetFiles.Describe(source), []);
        _laptop.Session.Write(_laptop.Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added");
        await _laptop.Session.SaveDataset(_laptop.Ask, version, () => File.OpenRead(source));
        await _laptop.SyncAsync();
        await _desktop.SignInAsync();
        await _desktop.Session.AddFromServer(_desktop.Ask, (await _desktop.Session.StoresToAdd(_desktop.Ask))!.Single());
        _kofre.Offline = true;

        Assert.False(await _desktop.Session.ExportDataset(_desktop.Ask, version, _desktop.Folder("out.csv")));

        Assert.Contains(_desktop.Ask.Told, told => told.StartsWith("Cannot export.", StringComparison.Ordinal) && told.Contains("cannot be reached"));
        _kofre.Offline = false;
        await _desktop.SyncAsync();
        Assert.True(await _desktop.Session.ExportDataset(_desktop.Ask, version, _desktop.Folder("out.csv")));
    }

    private static NodeAdded NewNode(string name) => new(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start));

    /// <summary>Signs in, creates a server vault (choosing the account's pass phrase), and uploads it.</summary>
    private static async Task CreateServerVaultAsync(Device device)
    {
        await device.SignInAsync();
        device.Session.CreateOnServer(device.Ask);
        Assert.NotNull(device.Session.Current);
        await device.SyncAsync();
    }

    /// <summary>The laptop's server vault, shared with Grace, who adds it and opens it with her own pass phrase.</summary>
    private async Task ShareWithGraceAsync(MemberRole role)
    {
        await _graces.SignInAsync(Grace);
        _graces.Ask.PassPhrase = "grace pass phrase";
        _graces.Session.SetAccountPassPhrase(_graces.Ask);
        await CreateServerVaultAsync(_laptop);

        _laptop.Ask.ShareWith = (Grace, role);
        await _laptop.Session.Share(_laptop.Ask);
        await _laptop.SyncAsync();

        await _graces.Session.AddFromServer(_graces.Ask, Assert.Single((await _graces.Session.StoresToAdd(_graces.Ask))!));
        Assert.NotNull(_graces.Session.Current);
    }

    /// <summary>Waits for work the session started without awaiting it (members settled after opening).</summary>
    private static async Task Settled(Func<bool> done)
    {
        for (var tries = 0; tries < 100 && !done(); tries++)
        {
            await Task.Delay(20);
        }

        Assert.True(done());
    }
}
