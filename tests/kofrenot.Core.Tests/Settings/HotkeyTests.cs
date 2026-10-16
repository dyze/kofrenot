namespace Kofrenot.Core.Tests.Settings;

public sealed class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+K", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "K")]
    [InlineData(" control + alt + k ", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "K")]
    [InlineData("Win+Shift+F9", HotkeyModifiers.Win | HotkeyModifiers.Shift, "F9")]
    [InlineData("Windows+F24", HotkeyModifiers.Win, "F24")]
    [InlineData("Alt+1", HotkeyModifiers.Alt, "1")]
    [InlineData("Ctrl+F1", HotkeyModifiers.Ctrl, "F1")]
    public void AValidHotkey_IsRead(string text, HotkeyModifiers modifiers, string key)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(modifiers, key), hotkey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K")]
    [InlineData("Shift+K")]
    [InlineData("Ctrl+Ctrl+K")]
    [InlineData("Ctrl+Hyper+K")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+KK")]
    [InlineData("Ctrl+F0")]
    [InlineData("Ctrl+F01")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+FX")]
    [InlineData("Ctrl+É")]
    public void AnInvalidHotkey_IsRefused(string? text)
    {
        Assert.False(Hotkey.TryParse(text, out var hotkey));
        Assert.Null(hotkey);
    }

    [Fact]
    public void TheDefault_IsCtrlAltK() =>
        Assert.Equal("Ctrl+Alt+K", Hotkey.Default.ToString());

    [Fact]
    public void ModifiersAreWritten_InTheUsualOrder() =>
        Assert.Equal("Ctrl+Alt+Shift+Win+F9", new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Ctrl, "F9").ToString());

    [Fact]
    public void TheWrittenForm_ReadsBack()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "7");

        Assert.True(Hotkey.TryParse(hotkey.ToString(), out var read));
        Assert.Equal(hotkey, read);
    }
}
