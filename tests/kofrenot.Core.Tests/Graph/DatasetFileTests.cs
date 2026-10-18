namespace Kofrenot.Core.Tests.Graph;

public sealed class DatasetFileTests
{
    [Theory]
    [InlineData("survey.csv", "survey.csv")]
    [InlineData("a:b|c?.txt", "a_b_c_.txt")]
    [InlineData("tab\there", "tab_here")]
    [InlineData("name. ", "name")]
    [InlineData("...", "dataset")]
    public void TheSafeName_ExportsOnWindows(string name, string safe) =>
        Assert.Equal(safe, new DatasetFile(name, 0, string.Empty).SafeName);
}
