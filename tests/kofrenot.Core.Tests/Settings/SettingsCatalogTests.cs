namespace Kofrenot.Core.Tests.Settings;

public class SettingsCatalogTests
{
    [Fact]
    public void EveryEntryBelongsToAKnownTab()
    {
        Assert.All(SettingsCatalog.All, entry => Assert.Contains(entry.Tab, SettingsCatalog.Tabs));
    }

    [Fact]
    public void IdsAreUnique()
    {
        var ids = SettingsCatalog.All.Select(entry => entry.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Tabs_AreInTheirDisplayOrder()
    {
        Assert.Equal(
            new[] { SettingsCatalog.GeneralTab, SettingsCatalog.KeyBindingsTab, SettingsCatalog.VaultTab, SettingsCatalog.LinkTypesTab, SettingsCatalog.AppearanceTab, SettingsCatalog.LoggingTab, SettingsCatalog.AboutTab },
            SettingsCatalog.Tabs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQuery_FindsNothing(string? query)
    {
        Assert.Empty(SettingsCatalog.Find(query));
    }

    [Fact]
    public void Find_MatchesTheLabel_IgnoringCase()
    {
        Assert.Equal(SettingsCatalog.ThemeId, Assert.Single(SettingsCatalog.Find("THEME")).Id);
    }

    [Fact]
    public void Find_MatchesAKeyword()
    {
        Assert.Equal(SettingsCatalog.ThemeId, Assert.Single(SettingsCatalog.Find("dark")).Id);
    }

    [Fact]
    public void Find_MatchesTheTabName()
    {
        var found = SettingsCatalog.Find("logging");

        Assert.Contains(found, entry => entry.Id == SettingsCatalog.DebugLoggingId);
        Assert.Contains(found, entry => entry.Id == SettingsCatalog.LogWindowId);
    }

    [Fact]
    public void Find_NeedsEveryWord()
    {
        Assert.Equal(SettingsCatalog.LogWindowId, Assert.Single(SettingsCatalog.Find("log window")).Id);
        Assert.Empty(SettingsCatalog.Find("dark log"));
    }

    [Theory]
    [InlineData("github", SettingsCatalog.AboutId)]
    [InlineData("issues", SettingsCatalog.AboutId)]
    [InlineData("version", SettingsCatalog.AboutId)]
    [InlineData("changelog", SettingsCatalog.AboutId)]
    [InlineData("release notes", SettingsCatalog.AboutId)]
    [InlineData("path", SettingsCatalog.FilesId)]
    [InlineData("json", SettingsCatalog.FilesId)]
    [InlineData("update", SettingsCatalog.UpdatesId)]
    [InlineData("download", SettingsCatalog.UpdatesId)]
    public void Find_FindsTheAboutTab(string query, string id)
    {
        Assert.Equal(id, Assert.Single(SettingsCatalog.Find(query)).Id);
        Assert.Equal(SettingsCatalog.AboutTab, SettingsCatalog.All.Single(entry => entry.Id == id).Tab);
    }

    [Theory]
    [InlineData("summon", SettingsCatalog.HotkeyId)]
    [InlineData("bindings", SettingsCatalog.KeyBindingsId)]
    [InlineData("dropbox", SettingsCatalog.VaultsId)]
    [InlineData("password", SettingsCatalog.VaultsId)]
    [InlineData("rename", SettingsCatalog.VaultsId)]
    public void Find_FindsTheVaultAndHotkeySettings(string query, string id)
    {
        Assert.Equal(id, Assert.Single(SettingsCatalog.Find(query)).Id);
    }

    [Fact]
    public void Find_KeepsTheCatalogOrder()
    {
        // "log" is also in "changelog", on the About row.
        var found = SettingsCatalog.Find("log");

        Assert.Equal(new[] { SettingsCatalog.DebugLoggingId, SettingsCatalog.LogWindowId, SettingsCatalog.AboutId }, found.Select(entry => entry.Id));
    }
}
