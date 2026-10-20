namespace Kofrenot.Core.Tests.Settings;

public sealed class KeyBindingsTests : IDisposable
{
    private readonly TempFolder _temp = new();

    private SettingsStore Settings() => new(_temp.Sub("settings.json"));

    [Fact]
    public void EveryArea_HasShortcuts_InTheOrderOfTheAreas()
    {
        var all = KeyBindings.All(Settings(), vault => vault.Location.FolderName);

        Assert.Equal(KeyBindings.Areas, all.Select(binding => binding.Area).Distinct());
    }

    [Fact]
    public void NoShortcut_IsListedTwiceInAnArea()
    {
        var all = KeyBindings.All(Settings(), vault => vault.Location.FolderName);

        Assert.Equal(all.Count, all.Select(binding => (binding.Area, binding.Keys)).Distinct().Count());
    }

    [Fact]
    public void TheGlobalHotkeys_FollowTheSettings()
    {
        var settings = Settings();
        var ctrlAltP = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "P");
        settings.SetHotkey(new Hotkey(HotkeyModifiers.Win, "K"));
        settings.SetVaults(new VaultList([new RegisteredVault(new VaultLocation(@"D:\Personal"), Hotkey: ctrlAltP), new RegisteredVault(new VaultLocation(@"D:\Work"))]));

        var anywhere = KeyBindings.All(settings, vault => vault.Location.FolderName).Where(binding => binding.Area == KeyBindings.Anywhere).ToList();

        Assert.Equal(
            [new KeyBinding(KeyBindings.Anywhere, "Win+K", "Show kofrenot"), new KeyBinding(KeyBindings.Anywhere, "Ctrl+Alt+P", "Show kofrenot on the vault “Personal”")],
            anywhere);
    }

    public void Dispose() => _temp.Dispose();
}
