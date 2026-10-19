namespace Kofrenot.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "kofrenot-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Defaults_FollowWindows_NoDebugLogging_UpdateCheckOn()
    {
        var store = new SettingsStore(FilePath);

        Assert.Equal(ThemeChoice.System, store.Theme);
        Assert.False(store.DebugLogging);
        Assert.True(store.CheckForUpdates);
        Assert.Empty(store.Vaults.Vaults);
        Assert.Null(store.SelectedVault);
        Assert.Equal(Hotkey.Default, store.Hotkey);
    }

    [Fact]
    public void TheVaults_AndTheSelectedOne_ArePersisted()
    {
        var first = new RegisteredVault(new VaultLocation(@"D:\Sync\private"), Guid.NewGuid(), new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "I"));
        var second = new RegisteredVault(new VaultLocation(@"D:\Sync\personal"));
        var store = new SettingsStore(FilePath);
        store.SetVaults(new VaultList([first, second]));
        store.SelectVault(second.Location);

        var reloaded = new SettingsStore(FilePath);

        Assert.Equal([first, second], reloaded.Vaults.Vaults);
        Assert.Equal(second.Location, reloaded.SelectedVault);
    }

    [Fact]
    public void AVaultFolderOfAnEarlierVersion_BecomesTheOnlyVault_Selected()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{\"VaultFolder\":\"D:\\\\Sync\\\\kofrenot\"}");

        var store = new SettingsStore(FilePath);

        var vault = Assert.Single(store.Vaults.Vaults);
        Assert.Equal(new RegisteredVault(new VaultLocation(@"D:\Sync\kofrenot")), vault);
        Assert.Equal(vault.Location, store.SelectedVault);
    }

    [Fact]
    public void TheSelectedVault_IsStillWrittenAsTheVaultFolder_ForAnEarlierVersion()
    {
        var store = new SettingsStore(FilePath);
        store.SetVaults(new VaultList([new RegisteredVault(new VaultLocation(@"D:\a")), new RegisteredVault(new VaultLocation(@"D:\b"))]));
        store.SelectVault(new VaultLocation(@"D:\b"));

        Assert.Contains("\"VaultFolder\":\"D:\\\\b\"", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("{\"Vaults\":[{\"Folder\":\"D:\\\\a\"}],\"SelectedVault\":\"D:\\\\elsewhere\"}")]
    [InlineData("{\"Vaults\":[{\"Folder\":\"D:\\\\a\"}]}")]
    [InlineData("{\"Vaults\":[{\"Folder\":\"D:\\\\a\"}],\"VaultFolder\":\"D:\\\\b\"}")]
    public void ASelectedVaultNotListed_SelectsTheFirst(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, json);

        Assert.Equal(new VaultLocation(@"D:\a"), new SettingsStore(FilePath).SelectedVault);
    }

    [Fact]
    public void DamagedVaultEntries_AreSkipped_AndAnInvalidIdOrHotkeyIsNone()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            FilePath,
            "{\"Vaults\":[null,{\"Folder\":\" \"},{\"Id\":\"" + Guid.NewGuid() + "\"},"
            + "{\"Folder\":\"D:\\\\a\",\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Hotkey\":\"K\"}]}");

        var vault = Assert.Single(new SettingsStore(FilePath).Vaults.Vaults);

        Assert.Equal(new RegisteredVault(new VaultLocation(@"D:\a")), vault);
    }

    [Fact]
    public void TheSelectedVault_IsNoneOnceRemoved()
    {
        var location = new VaultLocation(@"D:\a");
        var store = new SettingsStore(FilePath);
        store.SetVaults(new VaultList([new RegisteredVault(location)]));
        store.SelectVault(location);

        store.SetVaults(store.Vaults.Remove(location));

        Assert.Null(store.SelectedVault);
        Assert.Null(new SettingsStore(FilePath).SelectedVault);
    }

    [Fact]
    public void AVaultNotRegistered_CannotBeSelected()
    {
        var store = new SettingsStore(FilePath);

        Assert.Throws<ArgumentException>(() => store.SelectVault(new VaultLocation(@"D:\a")));
        store.SelectVault(null);
        Assert.Null(store.SelectedVault);
    }

    [Fact]
    public void TheHotkey_IsPersisted()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift, "F9");

        new SettingsStore(FilePath).SetHotkey(hotkey);

        Assert.Equal(hotkey, new SettingsStore(FilePath).Hotkey);
    }

    [Theory]
    [InlineData("{\"Hotkey\":\"K\"}")]
    [InlineData("{\"Hotkey\":\"nonsense\"}")]
    [InlineData("{\"VaultFolder\":\"  \"}")]
    public void AnInvalidHotkeyOrFolder_MeansTheDefault(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, json);

        var store = new SettingsStore(FilePath);

        Assert.Equal(Hotkey.Default, store.Hotkey);
        Assert.Empty(store.Vaults.Vaults);
    }

    [Fact]
    public void DebugLogging_IsPersisted()
    {
        new SettingsStore(FilePath).SetDebugLogging(true);

        Assert.True(new SettingsStore(FilePath).DebugLogging);
    }

    [Fact]
    public void UpdateCheck_CanBeSwitchedOff()
    {
        new SettingsStore(FilePath).SetCheckForUpdates(false);

        Assert.False(new SettingsStore(FilePath).CheckForUpdates);
    }

    [Theory]
    [InlineData(ThemeChoice.Light)]
    [InlineData(ThemeChoice.Dark)]
    [InlineData(ThemeChoice.System)]
    public void Theme_IsPersisted(ThemeChoice theme)
    {
        new SettingsStore(FilePath).SetTheme(theme);

        Assert.Equal(theme, new SettingsStore(FilePath).Theme);
    }

    [Theory]
    [InlineData("{\"Theme\":\"Purple\"}")]
    [InlineData("{\"Theme\":\"7\"}")]
    [InlineData("{\"Theme\":null}")]
    [InlineData("{}")]
    public void UnknownOrMissingTheme_FollowsWindows(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, json);

        Assert.Equal(ThemeChoice.System, new SettingsStore(FilePath).Theme);
    }

    [Theory]
    [InlineData(MainWindowPosition.Centered)]
    [InlineData(MainWindowPosition.AtMouse)]
    [InlineData(MainWindowPosition.LastPosition)]
    public void MainWindowPosition_IsPersisted(MainWindowPosition position)
    {
        new SettingsStore(FilePath).SetMainWindowPosition(position);

        Assert.Equal(position, new SettingsStore(FilePath).MainWindowPosition);
    }

    [Theory]
    [InlineData("{\"MainWindowPosition\":\"Elsewhere\"}")]
    [InlineData("{\"MainWindowPosition\":\"7\"}")]
    [InlineData("{}")]
    public void UnknownOrMissingMainWindowPosition_IsCentered(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, json);

        Assert.Equal(MainWindowPosition.Centered, new SettingsStore(FilePath).MainWindowPosition);
    }

    [Fact]
    public void StoredTheme_IgnoresCase()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{\"Theme\":\"dark\"}");

        Assert.Equal(ThemeChoice.Dark, new SettingsStore(FilePath).Theme);
    }

    [Fact]
    public void CorruptFile_MeansDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ not json");

        var store = new SettingsStore(FilePath);

        Assert.Equal(ThemeChoice.System, store.Theme);
        Assert.True(store.CheckForUpdates);
    }

    [Fact]
    public void ChangingOneSetting_KeepsTheOthers()
    {
        var store = new SettingsStore(FilePath);
        store.SetTheme(ThemeChoice.Dark);
        store.SetDebugLogging(true);
        store.SetCheckForUpdates(false);

        var reloaded = new SettingsStore(FilePath);

        Assert.Equal(ThemeChoice.Dark, reloaded.Theme);
        Assert.True(reloaded.DebugLogging);
        Assert.False(reloaded.CheckForUpdates);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
