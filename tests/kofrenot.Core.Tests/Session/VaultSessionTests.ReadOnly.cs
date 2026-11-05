using System.Text;

namespace Kofrenot.Core.Tests.Session;

/// <summary>A vault a newer kofrenot wrote to, which this one may read but not write: read only in the session.</summary>
public sealed partial class VaultSessionTests
{
    [Fact]
    public void AVaultANewerKofrenotWroteTo_TurnsReadOnly_AndRefusesChanges()
    {
        var folder = _device.CreateFolderVault();
        var changed = 0;
        Session.Changed += () => changed++;
        Assert.False(Session.IsReadOnly);

        WriteNewerChange(folder, $"{{\"format\":{VaultFormat.Current + 1},\"oldestReader\":{VaultFormat.Current},\"oldestWriter\":{VaultFormat.Current + 1},\"op\":\"defineEvent\",\"at\":\"2026-10-16T09:12:00Z\"}}");
        _device.Watching.Changed();

        Assert.Equal(1, changed);
        Assert.True(Session.IsReadOnly);
        Assert.True(Session.NeedsNewerKofrenotToWrite);
        Assert.Equal(VaultFormat.ReadOnlyMessage, Session.ReadOnlyReason);
        Assert.False(Session.CanShare);
        Assert.False(Session.Write(Ask, new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start)), "Node added"));
        Assert.Contains(VaultFormat.ReadOnlyMessage, Ask.Told);
        Assert.DoesNotContain(_device.Vault.Graph.Nodes, node => node.Name == "Dune");
    }

    [Fact]
    public void AVaultOfThisKofrenot_IsNotReadOnly()
    {
        _device.CreateFolderVault();

        Assert.Null(Session.ReadOnlyReason);
        Assert.False(Session.NeedsNewerKofrenotToWrite);
    }

    // A change file as a newer kofrenot writes it.
    private static void WriteNewerChange(string folder, string json)
    {
        var access = VaultKeys.Unlock(folder, VaultHeader.Read(VaultFolder.HeaderOf(folder)), ScriptedQuestions.FirstPassPhrase)!;
        var id = Guid.NewGuid();
        var name = id.ToString("N");
        var path = Path.Combine(folder, VaultFolder.ChangesFolder, name[..2], name + VaultFolder.ChangeExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new SecretBox(access.Keys.KeyOf(0)).Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray()));
    }
}
