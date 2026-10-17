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
        Assert.Null(store.VaultFolder);
        Assert.Equal(Hotkey.Default, store.Hotkey);
    }

    [Fact]
    public void TheVaultFolder_IsPersisted_AndCanBeCleared()
    {
        new SettingsStore(FilePath).SetVaultFolder(@"D:\Sync\kofrenot");
        Assert.Equal(@"D:\Sync\kofrenot", new SettingsStore(FilePath).VaultFolder);

        new SettingsStore(FilePath).SetVaultFolder(null);
        Assert.Null(new SettingsStore(FilePath).VaultFolder);
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
        Assert.Null(store.VaultFolder);
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
