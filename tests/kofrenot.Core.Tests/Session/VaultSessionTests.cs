using System.Text.Json.Nodes;

namespace Kofrenot.Core.Tests.Session;

/// <summary>The session over folder vaults: registered, created, unlocked, locked, switched, written.</summary>
public sealed class VaultSessionTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKofre _kofre = new();
    private readonly Device _device;

    public VaultSessionTests() => _device = new Device(_kofre);

    private VaultSession Session => _device.Session;

    private ScriptedQuestions Ask => _device.Ask;

    public void Dispose() => _device.Dispose();

    [Fact]
    public void AnEmptyFolder_BecomesANewVault_NamedAndWatched()
    {
        var changed = 0;
        Session.Changed += () => changed++;

        var folder = _device.CreateFolderVault();

        Assert.Equal("Personal", _device.Vault.Graph.VaultName);
        Assert.Equal("Personal", Session.NameOf(Session.Selected!));
        Assert.Single(Ask.RecoveryKeys);
        Assert.Equal([folder], _device.Watching.Folders);
        Assert.Equal(folder, Session.Folder);
        Assert.Single(Session.Vaults);
        Assert.True(changed > 0);
    }

    [Fact]
    public void ANewVaultCancelled_IsNotAdded()
    {
        Ask.Cancels = true;
        var folder = _device.Folder("vault");
        Directory.CreateDirectory(folder);

        Assert.Null(Session.Register(Ask, folder));

        Assert.Empty(Session.Vaults);
        Assert.Null(Session.Current);
    }

    [Fact]
    public void AFolderWithOtherFiles_OrListedAlready_IsNotAdded()
    {
        var folder = _device.CreateFolderVault();
        var other = _device.Folder("other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "notes.txt"), "x");

        Assert.Contains("holds other files", Session.Register(Ask, other));
        Assert.Contains("in the list already", Session.Register(Ask, folder));
    }

    [Fact]
    public void ARememberedPassPhrase_OpensWithoutAsking()
    {
        Ask.Remember = true;
        _device.CreateFolderVault();
        Session.Close();
        Ask.Asked.Clear();

        Session.Open(Ask);

        Assert.NotNull(Session.Current);
        Assert.Empty(Ask.Asked);
        Assert.True(Session.IsRemembered(Session.Selected!));
    }

    [Fact]
    public void AWrongPassPhrase_IsRefused()
    {
        _device.CreateFolderVault();
        Session.Close();
        Ask.PassPhrase = "wrong pass phrase";

        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Equal(["refused"], Ask.Refusals);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("Wrong pass phrase"));
    }

    [Fact]
    public void Forgetting_ThePassPhrase_AsksItAgain()
    {
        Ask.Remember = true;
        _device.CreateFolderVault();
        Session.Forget(Session.Selected!);
        Session.Close();
        Ask.Remember = false;

        Session.Open(Ask);

        Assert.Contains(nameof(ISessionQuestions.AskPassPhrase), Ask.Asked);
        Assert.False(Session.IsRemembered(Session.Selected!));
    }

    [Fact]
    public void ALockedVault_StaysLocked_UntilItsPassPhraseIsGiven()
    {
        Ask.Remember = true;
        _device.CreateFolderVault();

        Session.Lock();
        Session.Open(Ask);
        Assert.True(Session.IsLocked);
        Assert.Null(Session.Current);

        Ask.Cancels = true;
        Session.Unlock(Ask);
        Assert.True(Session.IsLocked);

        Ask.Cancels = false;
        Session.Unlock(Ask);
        Assert.False(Session.IsLocked);
        Assert.NotNull(Session.Current);
    }

    [Fact]
    public void Unlocking_AVaultNotLocked_OpensIt()
    {
        _device.CreateFolderVault();
        Session.Close();

        Session.Unlock(Ask);

        Assert.NotNull(Session.Current);
    }

    [Fact]
    public void Switching_OpensTheOtherVault_OrUsesTheFirstAgainWhenCancelled()
    {
        _device.CreateFolderVault("first");
        var first = Session.Selected!.Location;
        Ask.Name = "Work";
        _device.CreateFolderVault("second");
        var second = Session.Selected!.Location;
        Assert.Equal("Work", _device.Vault.Graph.VaultName);

        Session.SwitchTo(Ask, first);
        Assert.Equal("Personal", _device.Vault.Graph.VaultName);
        Session.SwitchTo(Ask, first);

        Ask.Cancels = true;
        Session.SwitchTo(Ask, second);
        Assert.Equal(first, Session.Selected!.Location);
        Assert.Equal("Personal", _device.Vault.Graph.VaultName);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("used again"));
    }

    [Fact]
    public void ALockedVaultNotOpen_ShowsTheNameLastRead()
    {
        _device.CreateFolderVault();
        var vault = Session.Selected!;

        Session.Lock();

        Assert.Equal("Personal", Session.NameOf(vault));
    }

    [Fact]
    public void Unregistering_TheOpenVault_OpensTheNextOne()
    {
        Ask.Remember = true;
        _device.CreateFolderVault("first");
        var first = Session.Selected!;
        Ask.Name = "Work";
        _device.CreateFolderVault("second");
        Session.SwitchTo(Ask, first.Location);

        Session.Unregister(Ask, first.Location);

        Assert.Single(Session.Vaults);
        Assert.Equal("Work", _device.Vault.Graph.VaultName);
        Assert.False(Session.IsRemembered(first));
        Session.Unregister(Ask, first.Location);
    }

    [Fact]
    public void Moving_ChangesTheOrder()
    {
        _device.CreateFolderVault("first");
        var first = Session.Selected!.Location;
        _device.CreateFolderVault("second");

        Session.Move(first, 1);

        Assert.Equal(first, Session.Vaults[1].Location);
    }

    [Fact]
    public void AVaultOutOfReach_IsNeverMadeANewVault()
    {
        var folder = _device.CreateFolderVault();
        Session.Close();
        Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Contains(Ask.Told, told => told.Contains("cannot be reached"));
        Assert.False(File.Exists(VaultFolder.HeaderOf(folder)));
    }

    [Fact]
    public void AVaultOfANewerKofrenot_StaysClosed()
    {
        var folder = _device.CreateFolderVault();
        Session.Close();
        var header = JsonNode.Parse(File.ReadAllText(VaultFolder.HeaderOf(folder)))!;
        header["format"] = VaultFormat.Current + 1;
        File.WriteAllText(VaultFolder.HeaderOf(folder), header.ToJsonString());

        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Equal(VaultFormat.Current + 1, Session.NewerFormat);
    }

    [Fact]
    public void ADamagedHeader_IsTold()
    {
        var folder = _device.CreateFolderVault();
        Session.Close();
        File.WriteAllText(VaultFolder.HeaderOf(folder), "{damaged");

        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Contains(Ask.Told, told => told.Contains("cannot be opened"));
    }

    [Fact]
    public void AFolderThatCannotBeWatched_IsReadWhenShown()
    {
        _device.Watching.Fails = true;

        _device.CreateFolderVault();

        Assert.NotNull(Session.Current);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("cannot be watched"));
    }

    [Fact]
    public void ChangesOfAnotherApp_AreReadWhenTheFolderChanges()
    {
        var folder = _device.CreateFolderVault();
        var access = VaultKeys.Unlock(folder, VaultHeader.Read(VaultFolder.HeaderOf(folder)), ScriptedQuestions.FirstPassPhrase)!;
        Vault.Open(folder, access.Header, access.Keys).Append(new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start)));
        var changed = 0;
        Session.Changed += () => changed++;

        _device.Watching.Changed();

        Assert.Contains(_device.Vault.Graph.Nodes, node => node.Name == "Dune");
        Assert.Equal(1, changed);
    }

    [Fact]
    public void ASkippedChangeFile_IsReadAgainAFewSecondsLater()
    {
        var (file, content) = WriteUnreadableChange("Dune");

        _device.Watching.Changed();

        var retry = _device.Timers.Created[^1];
        Assert.Equal(TimeSpan.FromSeconds(2), retry.DueTime);
        File.WriteAllBytes(file, content);
        retry.Fire();
        _device.Ui.RunAll();
        Assert.Contains(_device.Vault.Graph.Nodes, node => node.Name == "Dune");
        Assert.Equal(Timeout.InfiniteTimeSpan, retry.DueTime);
    }

    [Fact]
    public void ASkippedChangeFile_IsTriedAFewTimes_ThenLeftToTheNextRefresh()
    {
        WriteUnreadableChange("Dune");
        _device.Watching.Changed();
        var retry = _device.Timers.Created[^1];

        var waits = new List<TimeSpan> { retry.DueTime };
        for (var i = 0; i < VaultSession.RetryDelays.Count; i++)
        {
            retry.Fire();
            _device.Ui.RunAll();
            waits.Add(retry.DueTime);
        }

        Assert.Equal([.. VaultSession.RetryDelays, VaultSession.RetryDelays[^1]], waits);
        Assert.True(_device.Vault.HasSkippedChanges);
        Session.Close();
        Assert.Equal(Timeout.InfiniteTimeSpan, retry.DueTime);
    }

    // A change of another app, its file damaged as when still being copied; its file and content.
    private (string File, byte[] Content) WriteUnreadableChange(string name)
    {
        var folder = _device.CreateFolderVault();
        var access = VaultKeys.Unlock(folder, VaultHeader.Read(VaultFolder.HeaderOf(folder)), ScriptedQuestions.FirstPassPhrase)!;
        var before = Directory.GetFiles(Path.Combine(folder, VaultFolder.ChangesFolder), "*", SearchOption.AllDirectories);
        Vault.Open(folder, access.Header, access.Keys).Append(new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start)));
        var file = Directory.GetFiles(Path.Combine(folder, VaultFolder.ChangesFolder), "*", SearchOption.AllDirectories).Except(before).Single();
        var content = File.ReadAllBytes(file);
        File.WriteAllBytes(file, [1, 2, 3]);
        return (file, content);
    }

    [Fact]
    public void TheRecoveryKey_OpensTheVault_ThenAsksANewPassPhrase()
    {
        _device.CreateFolderVault();
        var recovery = Ask.RecoveryKeys.Single();
        Session.Close();
        (Ask.UsesRecoveryKey, Ask.RecoveryKey) = (true, recovery);

        Session.Open(Ask);

        Assert.NotNull(Session.Current);
        Assert.Contains(nameof(ISessionQuestions.AskToChange), Ask.Asked);
        Session.Close();
        (Ask.UsesRecoveryKey, Ask.PassPhrase) = (false, Ask.NewPassPhrase);
        Session.Open(Ask);
        Assert.NotNull(Session.Current);
    }

    [Fact]
    public void TheRecoveryKeyCancelled_LeavesTheVaultClosed()
    {
        _device.CreateFolderVault();
        Session.Close();
        Ask.UsesRecoveryKey = true;

        Session.Open(Ask);

        Assert.Null(Session.Current);
        Assert.Contains(nameof(ISessionQuestions.AskRecoveryKey), Ask.Asked);
    }

    [Fact]
    public void ThePassPhrase_IsChanged()
    {
        _device.CreateFolderVault();
        Assert.True(Session.CanChangePassPhrase);

        Ask.Cancels = true;
        Session.ChangePassPhrase(Ask);
        Ask.Cancels = false;
        Session.ChangePassPhrase(Ask);
        Session.Close();
        Ask.PassPhrase = Ask.NewPassPhrase;
        Session.Open(Ask);

        Assert.NotNull(Session.Current);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("Pass phrase change cancelled"));
    }

    [Fact]
    public void ANode_IsCreated_WithItsNote()
    {
        _device.CreateFolderVault();
        Ask.Creation = form =>
        {
            form.SetType(Side.Left, TypeCatalog.Note);
            form.SetContent(Side.Left, "# Spice");
            return true;
        };

        var plan = Session.Create(Ask, InputParser.Parse("Dune")!);

        var dune = Assert.Single(_device.Vault.Graph.Nodes, node => node.Name == "Dune");
        Assert.NotNull(plan);
        Assert.Equal("# Spice", _device.Vault.ReadNote(_device.Vault.Graph.NoteVersionsOf(dune.Id).Single().Id));
    }

    [Fact]
    public void ACreationCancelled_AddsNothing()
    {
        _device.CreateFolderVault();
        Ask.Creation = _ => false;

        Assert.Null(Session.Create(Ask, InputParser.Parse("Dune")!));
        Assert.Null(Session.CreateNode(Ask, TypeCatalog.Note));
        Assert.DoesNotContain(_device.Vault.Graph.Nodes, node => node.Name == "Dune");
    }

    [Fact]
    public void WithoutAVault_NothingIsWritten()
    {
        var note = new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []);

        Assert.Null(Session.Create(Ask, InputParser.Parse("Dune")!));
        Assert.Null(Session.CreateNode(Ask));
        Assert.False(Session.SaveNote(Ask, note, "x"));
        Assert.False(Session.Write(Ask, new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start)), "Node added"));
        Assert.False(Session.Rename(Ask, new NodeRenamed(Guid.NewGuid(), Start, Guid.NewGuid(), "x", [])));
    }

    [Fact]
    public void ANote_IsSaved_AndANodeRenamed()
    {
        _device.CreateFolderVault();
        var node = AddNode("Dune", TypeCatalog.Note.Id);
        var note = new NoteEdited(Guid.NewGuid(), Start, node.Id, []);

        Assert.True(Session.SaveNote(Ask, note, "# Spice"));
        Assert.True(Session.Rename(Ask, new RenamePlanner(_device.Vault.Graph).Plan(node, "Arrakis").Change!));

        Assert.Equal("# Spice", _device.Vault.ReadNote(note.Id));
        Assert.Equal("Arrakis", _device.Vault.Graph.FindNode(node.Id)!.Name);
    }

    [Fact]
    public void AWriteThatFails_IsTold()
    {
        var folder = _device.CreateFolderVault();
        var changes = Path.Combine(folder, VaultFolder.ChangesFolder);
        Directory.Delete(changes, recursive: true);
        File.WriteAllText(changes, "a file where the changes go");

        Assert.False(Session.Write(Ask, new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start)), "Node added"));

        Assert.Contains(Ask.Told, told => told.StartsWith("Cannot write to the vault", StringComparison.Ordinal));
    }

    [Fact]
    public void ANode_IsMadeAView_AfterItsDefinitionIsGiven()
    {
        _device.CreateFolderVault();
        var node = AddNode("Books", TypeCatalog.Generic.Id);
        var retype = new RetypePlanner(_device.Vault.Graph).Plan(node, TypeCatalog.View).Change!;

        Assert.False(Session.Retype(Ask, retype));
        Ask.View = (_, _, _) => new ViewEdit(new FlatDefinition(TypeCatalog.Note.Id), Rename: null);
        Assert.True(Session.Retype(Ask, retype));

        Assert.Equal(TypeCatalog.View.Id, _device.Vault.Graph.FindNode(node.Id)!.Type);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("Node type change cancelled"));
    }

    [Fact]
    public void ANode_IsMadeANote()
    {
        _device.CreateFolderVault();
        var node = AddNode("Ideas", TypeCatalog.Generic.Id);

        Assert.True(Session.Retype(Ask, new RetypePlanner(_device.Vault.Graph).Plan(node, TypeCatalog.Note).Change!));
        Assert.False(Session.Retype(Ask, new NodeRetyped(Guid.NewGuid(), Start, Guid.NewGuid(), TypeCatalog.Note.Id, [])));
    }

    [Fact]
    public void AView_IsEdited_AndRenamed()
    {
        _device.CreateFolderVault();
        var view = AddNode("Books", TypeCatalog.View.Id);

        Assert.False(Session.EditView(Ask, view));
        Ask.View = (graph, node, _) => new ViewEdit(new FlatDefinition(TypeCatalog.Note.Id), new RenamePlanner(graph).Plan(node, "Notes").Change);
        Assert.True(Session.EditView(Ask, view));

        Assert.Equal("Notes", _device.Vault.Graph.FindNode(view.Id)!.Name);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("View edited and renamed"));
    }

    [Fact]
    public async Task ADataset_IsImported_Exported_AndCopied()
    {
        _device.CreateFolderVault();
        var folder = AddNode("Folder", TypeCatalog.Generic.Id);
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);
        Session.Write(Ask, new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, folder.Id, survey.Id, Start)), "Linked");
        var source = _device.Folder("survey.csv");
        File.WriteAllText(source, "a,b");
        var version = new DatasetEdited(Guid.NewGuid(), Start, survey.Id, DatasetFiles.Describe(source), []);

        var saving = Session.SaveDataset(Ask, version, () => File.OpenRead(source));
        Assert.True(Session.IsImporting(survey.Id));
        Assert.Equal(1, Session.ImportsRunning);
        Assert.True(await saving);

        var target = _device.Folder("exported.csv");
        Assert.True(await Session.ExportDataset(Ask, version, target));
        Assert.Equal("a,b", File.ReadAllText(target));

        var other = AddNode("Other", TypeCatalog.Generic.Id);
        var plan = new CopyPlanner(_device.Vault.Graph, TypeCatalog.Contains.Id).Plan(folder, other, _device.Vault.HasContent);
        Assert.True(await Session.Copy(Ask, plan));
        Assert.Equal(2, _device.Vault.Graph.Nodes.Count(node => node.Name == "Survey"));
    }

    [Fact]
    public async Task FilesImported_BecomeDatasets_UnderTheParent_WithTheirFiles()
    {
        _device.CreateFolderVault();
        var folder = AddNode("Folder", TypeCatalog.Generic.Id);
        File.WriteAllText(_device.Folder("a.csv"), "a");
        File.WriteAllText(_device.Folder("b.txt"), "bb");
        var preview = new FileImportPlanner(_device.Vault.Graph).Preview([_device.Folder("a.csv"), _device.Folder("b.txt")], new ParentLink(folder, TypeCatalog.Contains.Id));

        Assert.Equal(2, await Session.ImportFiles(Ask, preview));

        var graph = _device.Vault.Graph;
        var datasets = graph.LinksOf(folder.Id).Select(link => graph.FindNode(link.To)!).ToList();
        Assert.Equal(["a", "b"], datasets.Select(node => node.Name).Order());
        Assert.All(datasets, node => Assert.True(_device.Vault.Datasets.Exists(graph.DatasetVersionsOf(node.Id).Single().Id)));
        Assert.Contains(_device.Log.Entries, entry => entry.Message == "Files imported as datasets (2 files, 3 bytes, in a tree)");
    }

    [Fact]
    public async Task AFileImported_ThatCannotBeRead_LeavesItsDatasetEmpty()
    {
        _device.CreateFolderVault();
        var preview = new FileImportPlanner(_device.Vault.Graph).Preview([new ImportFile("a.csv", 3, () => throw new IOException("Locked"))]);

        Assert.Equal(0, await Session.ImportFiles(Ask, preview));

        var node = Assert.Single(_device.Vault.Graph.Nodes, node => node.Name == "a");
        Assert.Empty(_device.Vault.Graph.DatasetVersionsOf(node.Id));
        Assert.Contains(Ask.Told, told => told.StartsWith("Cannot import", StringComparison.Ordinal));
        Assert.Equal(0, await Session.ImportFiles(Ask, new FileImportPlanner(_device.Vault.Graph).Preview(Array.Empty<ImportFile>())));
    }

    [Fact]
    public async Task AFileThatCannotBeRead_IsNotImported_NorExported()
    {
        _device.CreateFolderVault();
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);
        var version = new DatasetEdited(Guid.NewGuid(), Start, survey.Id, new DatasetFile("a.csv", 3, new string('0', 64)), []);

        Assert.False(await Session.SaveDataset(Ask, version, () => throw new IOException("Locked")));
        Assert.False(await Session.ExportDataset(Ask, version, _device.Folder("out.csv")));
        Assert.False(await Session.ExportDataset(Ask, version, new MemoryStream()));

        Assert.Contains(Ask.Told, told => told.StartsWith("Cannot import", StringComparison.Ordinal));
        Assert.Equal(2, Ask.Told.Count(told => told.StartsWith("Cannot export", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ADataset_IsExported_ToAStream()
    {
        _device.CreateFolderVault();
        var survey = AddNode("Survey", TypeCatalog.Dataset.Id);
        var source = _device.Folder("survey.csv");
        File.WriteAllText(source, "a,b");
        var version = new DatasetEdited(Guid.NewGuid(), Start, survey.Id, DatasetFiles.Describe(source), []);
        Assert.True(await Session.SaveDataset(Ask, version, () => File.OpenRead(source)));
        using var target = new MemoryStream();

        Assert.True(await Session.ExportDataset(Ask, version, target));

        Assert.Equal("a,b", System.Text.Encoding.UTF8.GetString(target.ToArray()));
    }

    [Fact]
    public void TheVault_IsRenamed()
    {
        _device.CreateFolderVault();

        Assert.True(Session.Name(Ask, new VaultNamePlanner(_device.Vault.Graph).Plan("Home").Change!));

        Assert.Equal("Home", Session.NameOf(Session.Selected!));
    }

    [Fact]
    public void AnOlderVault_IsUpgraded_OnlyOnceAgreed()
    {
        var folder = Format5Vault();
        Ask.Agrees = false;

        Assert.Null(Session.Register(Ask, folder));
        Assert.Null(Session.Current);
        Assert.Equal(5, Session.OlderFormat);

        Ask.Agrees = true;
        Session.Upgrade(Ask);

        Assert.NotNull(Session.Current);
        Assert.Null(Session.OlderFormat);
        Assert.Contains(nameof(ISessionQuestions.AskToUpgrade), Ask.Asked);
        Assert.Single(Ask.RecoveryKeys);
        Assert.True(Session.CanChangePassPhrase);
    }

    [Fact]
    public void AnUpgradeWhoseKeysAreNotGiven_LeavesTheVaultUnused()
    {
        var folder = Format5Vault();
        Ask.Agrees = false;
        Session.Register(Ask, folder);

        (Ask.Agrees, Ask.Cancels) = (true, true);
        Session.Upgrade(Ask);

        Assert.Null(Session.Current);
        Assert.Equal(5, Session.OlderFormat);
        Assert.Contains(_device.Log.Entries, entry => entry.Message.Contains("Upgrade of the keys of vault"));
        Session.Upgrade(Ask);
    }

    // A vault as kofrenot 0.6 created it: its master key in its header, no keys/ folder.
    private string Format5Vault()
    {
        var folder = _device.Folder("old");
        var id = Guid.NewGuid();
        var (wrapped, _) = KeyWrap.Create(ScriptedQuestions.FirstPassPhrase, id.ToByteArray(), 10);
        Directory.CreateDirectory(Path.Combine(folder, VaultFolder.ChangesFolder));
        File.WriteAllText(VaultFolder.HeaderOf(folder), $"{{\"format\":5,\"id\":\"{id}\",\"key\":{{\"salt\":\"{Convert.ToBase64String(wrapped.Salt)}\",\"iterations\":10,\"sealed\":\"{Convert.ToBase64String(wrapped.Sealed)}\"}}}}");
        return folder;
    }

    private Node AddNode(string name, string type)
    {
        var node = new Node(Guid.NewGuid(), type, name, Start);
        Assert.True(Session.Write(Ask, new NodeAdded(Guid.NewGuid(), Start, node), "Node added"));
        return node;
    }
}
