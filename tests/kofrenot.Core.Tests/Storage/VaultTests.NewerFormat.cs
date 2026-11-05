namespace Kofrenot.Core.Tests.Storage;

/// <summary>A vault a newer kofrenot wrote to, which this one may still read (<see cref="VaultFormat"/>): read only, skipping what it does not know.</summary>
public sealed partial class VaultTests
{
    private const int Newer = VaultFormat.Current + 1;

    [Fact]
    public void AChangeANewerKofrenotLetsThisOneRead_IsRead_ItsUnknownPartsIgnored()
    {
        var vault = Open(snapshot: false);
        WriteChange(NewerNodeAddedJson("Dune", oldestWriter: VaultFormat.Current));

        Assert.Equal(1, vault.Refresh());

        Assert.Contains(vault.Graph.Nodes, node => node.Name == "Dune");
        Assert.Equal(Newer, vault.Format);
        Assert.False(vault.IsReadOnly);
        AddNode(vault, "Herbert");
    }

    [Fact]
    public void AChangeANewerKofrenotLetsThisOneReadNotWrite_MakesTheVaultReadOnly()
    {
        var vault = Open(snapshot: false);
        WriteChange(NewerNodeAddedJson("Dune", oldestWriter: Newer));

        vault.Refresh();

        Assert.True(vault.IsReadOnly);
        Assert.Equal(Newer, vault.OldestWriter);
        Assert.Contains(vault.Graph.Nodes, node => node.Name == "Dune");
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("read only"));
        var refused = Assert.Throws<InvalidOperationException>(() => AddNode(vault, "Herbert"));
        Assert.Contains("read only", refused.Message);
        Assert.Throws<InvalidOperationException>(() => vault.Save(new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []), "text"));
    }

    [Fact]
    public void AHeaderANewerKofrenotLetsThisOneReadNotWrite_OpensTheVaultReadOnly()
    {
        var vault = Vault.Open(Folder, _header with { Format = Newer, OldestReader = VaultFormat.Current, OldestWriter = Newer }, _keys, null, _log);

        Assert.True(vault.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => AddNode(vault, "Dune"));
    }

    [Fact]
    public void AChangeANewerKofrenotWroteInAWayThisOneDoesNotKnow_IsSkipped_AndNotReadAgain()
    {
        var vault = Open(snapshot: false);
        WriteChange($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"oldestWriter\":{Newer},\"op\":\"defineEvent\",\"at\":\"2026-10-16T09:12:00Z\"}}");

        Assert.Equal(0, vault.Refresh());
        vault.Refresh();

        Assert.True(vault.IsReadOnly);
        Assert.False(vault.HasSkippedChanges);
        Assert.Single(_log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("1 changes of a newer kofrenot skipped"));
        Assert.DoesNotContain(_log.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public void AKnownChangeOnSomethingThisKofrenotDoesNotKnow_IsSkipped()
    {
        var vault = Open(snapshot: false);
        WriteChange($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"oldestWriter\":{Newer},\"op\":\"setField\",\"at\":\"2026-10-16T09:12:00Z\","
            + $"\"field\":{{\"link\":\"{Guid.NewGuid()}\",\"owner\":\"own\",\"id\":\"date\",\"name\":\"Date\",\"kind\":\"date\",\"value\":\"1965-01-01\",\"replaces\":[]}}}}");

        Assert.Equal(0, vault.Refresh());
        Assert.True(vault.IsReadOnly);
    }

    [Fact]
    public void AReadOnlyVault_StaysReadOnlyFromItsSnapshot_ItsSkippedChangesReadAgain()
    {
        WriteChange(NewerNodeAddedJson("Dune", oldestWriter: Newer));
        WriteChange($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"op\":\"defineEvent\",\"at\":\"2026-10-16T09:12:00Z\"}}");
        Open();

        var reopened = Open();

        Assert.True(reopened.IsReadOnly);
        Assert.Equal(Newer, reopened.Format);
        Assert.Contains(reopened.Graph.Nodes, node => node.Name == "Dune");
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("(1 changes from the snapshot, 0 read)"));
        Assert.Equal(2, _log.Entries.Count(entry => entry.Message.Contains("1 changes of a newer kofrenot skipped")));
    }

    [Fact]
    public void AFieldOfAKindANewerKofrenotWrote_IsKept_ItsValueAsText()
    {
        var vault = Open();
        var book = NewNode("Dune");
        vault.Append(new NodeAdded(Guid.NewGuid(), Start, book));
        WriteChange($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"oldestWriter\":{Newer},\"op\":\"defineNodeType\",\"at\":\"2026-10-16T09:12:00Z\","
            + "\"nodeType\":{\"id\":\"book\",\"name\":\"Book\",\"fields\":[{\"id\":\"stars\",\"name\":\"Stars\",\"kind\":\"rating\",\"max\":5}],\"replaces\":[]}}");
        WriteChange($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"op\":\"setField\",\"at\":\"2026-10-16T09:13:00Z\","
            + $"\"field\":{{\"node\":\"{book.Id}\",\"owner\":\"own\",\"id\":\"stars\",\"name\":\"Stars\",\"kind\":\"rating\",\"value\":\"4/5\",\"replaces\":[]}}}}");

        Assert.Equal(2, vault.Refresh());

        var fromSnapshot = Open();
        var type = Assert.Single(fromSnapshot.Graph.NodeTypes.Types, type => type.Id == "book");
        Assert.Equal(FieldKind.Unknown, Assert.Single(type.Fields).Kind);
        var value = Assert.Single(NodeFields.Of(fromSnapshot.Graph, book.Id));
        Assert.Equal((FieldKind.Unknown, "4/5"), (value.Kind, value.Value));
    }

    // An added node in the newer format, with a property this kofrenot does not know.
    private static string NewerNodeAddedJson(string name, int oldestWriter) =>
        $"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"oldestWriter\":{oldestWriter},\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\","
        + $"\"node\":{{\"id\":\"{Guid.NewGuid()}\",\"type\":\"generic\",\"name\":\"{name}\",\"created\":\"2026-10-16T09:12:00Z\",\"qualifiers\":[]}}}}";
}
