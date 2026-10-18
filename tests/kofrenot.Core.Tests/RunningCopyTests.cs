namespace Kofrenot.Core.Tests;

public sealed class RunningCopyTests
{
    private static readonly AppCopy Installed = new(@"C:\Users\me\AppData\Local\Programs\kofrenot\kofrenot.exe", "0.1");

    [Fact]
    public void TheSameExeAndVersion_IsTheSameCopy_WhateverTheCase() =>
        Assert.True(Installed.IsSameAs(Installed with { Path = Installed.Path.ToUpperInvariant() }));

    [Fact]
    public void AnotherPath_OrAnotherVersion_IsAnotherCopy()
    {
        Assert.False(Installed.IsSameAs(Installed with { Path = @"D:\code\kofrenot\artifacts\bin\kofrenot.App\release\kofrenot.exe" }));
        Assert.False(Installed.IsSameAs(Installed with { Version = "0.2" }));
    }

    [Fact]
    public void TheWarning_NamesBothCopies_AndHowToSwitch()
    {
        var starting = new AppCopy(@"D:\dev\kofrenot.exe", "0.2");

        var warning = RunningCopy.Warning(starting, Installed);

        Assert.Contains($"version 0.1, from {Installed.Path}", warning);
        Assert.Contains(@"version 0.2, from D:\dev\kofrenot.exe", warning);
        Assert.Contains("tray icon > Quit", warning);
    }
}
