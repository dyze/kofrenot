namespace Kofrenot.Core.Tests;

public sealed class RunningCopyTests
{
    private static readonly AppCopy Installed = new(@"C:\Users\me\AppData\Local\Programs\kofrenot\kofrenot.exe", "0.3+4e9c3137c078155d");
    private static readonly AppCopy Built = new(@"D:\dev\kofrenot.exe", "0.4+abc1234def567890.dirty");

    [Fact]
    public void TheSameExeAndBuild_IsTheSameCopy_WhateverTheCase() =>
        Assert.True(Installed.IsSameAs(Installed with { Path = Installed.Path.ToUpperInvariant() }));

    [Fact]
    public void AnotherPath_IsAnotherCopy_EvenOfTheSameBuild() =>
        Assert.False(Installed.IsSameAs(Installed with { Path = @"E:\kofrenot\kofrenot.exe" }));

    [Theory]
    [InlineData("0.4+4e9c3137c078155d")]
    [InlineData("0.3+abc1234def567890")]
    [InlineData("0.3+4e9c3137c078155d.dirty")]
    public void AnotherVersion_CommitOrDirtyMark_IsAnotherCopy(string build) =>
        Assert.False(Installed.IsSameAs(Installed with { Build = build }));

    [Fact]
    public void Describe_ShowsTheShortBuildAndThePath() =>
        Assert.Equal(@"kofrenot 0.3 (4e9c313), from C:\Users\me\AppData\Local\Programs\kofrenot\kofrenot.exe", Installed.Describe());

    [Fact]
    public void TheWarning_NamesBothCopies_AndHowToSwitch()
    {
        var warning = RunningCopy.Warning(Built, Installed);

        Assert.StartsWith($"{Installed.Describe()}, is already running.", warning);
        Assert.Contains(@"This one is kofrenot 0.4 (abc1234-dirty), from D:\dev\kofrenot.exe.", warning);
        Assert.Contains("tray icon > Quit", warning);
    }

    [Fact]
    public void TheReplaceQuestion_NamesBothCopies()
    {
        Assert.Equal(
            $"{Installed.Describe()}, is already running.\n\nThis one is {Built.Describe()}.\n\nClose the running one and start this one?",
            RunningCopy.ReplaceQuestion(Built, Installed));
    }
}
