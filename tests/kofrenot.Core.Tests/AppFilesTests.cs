namespace Kofrenot.Core.Tests;

public class AppFilesTests
{
    private static readonly string Data = Path.Combine(Path.GetTempPath(), "kofrenot-data");
    private static readonly string Program = Path.Combine(Path.GetTempPath(), "kofrenot-program");

    [Fact]
    public void TheUserFiles_AreInTheDataFolder()
    {
        var files = new AppFiles(Data, Program);

        Assert.Equal(Path.Combine(Data, "settings.json"), files.Settings);
        Assert.Equal(Path.Combine(Data, "windows.json"), files.WindowPositions);
        Assert.Equal(Path.Combine(Data, "updates.json"), files.UpdateCheck);
        Assert.Equal(Path.Combine(Data, "logs"), files.Logs);
        Assert.Equal(Path.Combine(Data, "keys.json"), files.RememberedKeys);
        Assert.Equal(Path.Combine(Data, "vault-names.json"), files.VaultNames);
        Assert.Equal(Path.Combine(Data, "cache"), files.Cache);
        Assert.Equal(Data, files.DataDirectory);
    }

    [Fact]
    public void AnInstalledCopy_KeepsItsDataInLocalAppData()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), "local");

        var files = AppFiles.For(Program, localAppData, _ => false);

        Assert.Equal(Path.Combine(localAppData, "kofrenot"), files.DataDirectory);
        Assert.Equal(Program, files.ProgramDirectory);
        Assert.False(files.IsPortable);
    }

    [Fact]
    public void WithTheMarker_TheDataIsKeptBesideTheProgram()
    {
        var asked = new List<string>();

        var files = AppFiles.For(Program, Data, path => { asked.Add(path); return true; });

        Assert.Equal(Path.Combine(Program, "portable.txt"), Assert.Single(asked));
        Assert.Equal(Path.Combine(Program, "data"), files.DataDirectory);
        Assert.True(files.IsPortable);
    }

    [Fact]
    public void ByDefault_TheMarkerIsLookedForOnDisk()
    {
        Assert.False(AppFiles.For(Program, Data).IsPortable);
    }

    [Fact]
    public void TheChangelog_IsNextToTheProgram()
    {
        Assert.Equal(Path.Combine(Program, "CHANGELOG.md"), new AppFiles(Data, Program).Changelog);
    }

    [Fact]
    public void All_ListsEveryFileOnce_ThenTheProgram()
    {
        var files = new AppFiles(Data, Program);

        Assert.Equal(12, files.All.Count);
        Assert.Equal(files.All.Count, files.All.Select(file => file.Path).Distinct().Count());
        Assert.Equal(new AppFile("Settings", files.Settings), files.All[0]);
        Assert.Equal(new AppFile("Program", Program), files.All[^1]);
    }

    [Fact]
    public void AnExistingFile_IsSelectedInItsFolder()
    {
        var path = Path.Combine(Data, "settings.json");

        Assert.Equal($"/select,\"{path}\"", AppFiles.ShowInFolderArguments(path, _ => true));
    }

    [Fact]
    public void AFolder_IsSelectedWithoutItsTrailingSeparator()
    {
        Assert.Equal($"/select,\"{Program}\"", AppFiles.ShowInFolderArguments(Program + Path.DirectorySeparatorChar, _ => true));
    }

    [Fact]
    public void AMissingFile_OpensTheFolderItWillBeIn()
    {
        var path = Path.Combine(Data, "updates.json");

        Assert.Equal($"\"{Data}\"", AppFiles.ShowInFolderArguments(path, _ => false));
    }

    [Fact]
    public void ByDefault_TheFileSystemIsAsked()
    {
        var missing = Path.Combine(Data, Guid.NewGuid().ToString("N"));

        Assert.Equal($"\"{Data}\"", AppFiles.ShowInFolderArguments(missing));
        Assert.StartsWith("/select,", AppFiles.ShowInFolderArguments(Path.GetTempPath()));
    }
}
