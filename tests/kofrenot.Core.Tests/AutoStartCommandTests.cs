namespace Kofrenot.Core.Tests;

public class AutoStartCommandTests
{
    private const string Exe = @"C:\Program Files\kofrenot\kofrenot.exe";

    [Fact]
    public void Build_QuotesThePath_AndStartsHidden()
    {
        Assert.Equal("\"C:\\Program Files\\kofrenot\\kofrenot.exe\" --hidden", AutoStartCommand.Build(Exe));
    }

    [Fact]
    public void Status_ThisCopy_ForTheBuiltCommand()
    {
        Assert.Equal(AutoStartStatus.ThisCopy, AutoStartCommand.Status(AutoStartCommand.Build(Exe), Exe));
    }

    [Fact]
    public void Status_IgnoresCaseAndSurroundingSpaces()
    {
        var stored = "  " + AutoStartCommand.Build(Exe).ToUpperInvariant() + " ";

        Assert.Equal(AutoStartStatus.ThisCopy, AutoStartCommand.Status(stored, Exe));
    }

    [Fact]
    public void Status_AnotherLocation_IsAnotherCopy()
    {
        Assert.Equal(AutoStartStatus.OtherCopy, AutoStartCommand.Status(AutoStartCommand.Build(@"D:\build\kofrenot.exe"), Exe));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Status_OffWhenNothingIsStored(string? stored)
    {
        Assert.Equal(AutoStartStatus.Off, AutoStartCommand.Status(stored, Exe));
    }

    [Theory]
    [InlineData("\"D:\\my build\\kofrenot.exe\" --hidden", @"D:\my build\kofrenot.exe")]
    [InlineData("\"D:\\my build\\kofrenot.exe", @"D:\my build\kofrenot.exe")]
    [InlineData(@"D:\build\kofrenot.exe --hidden", @"D:\build\kofrenot.exe")]
    [InlineData(@"D:\build\kofrenot.exe", @"D:\build\kofrenot.exe")]
    public void ProgramOf_IsThePathWithoutArguments(string stored, string expected)
    {
        Assert.Equal(expected, AutoStartCommand.ProgramOf(stored));
    }

    [Fact]
    public void ProgramOf_NullWhenNothingIsStored()
    {
        Assert.Null(AutoStartCommand.ProgramOf(" "));
        Assert.Null(AutoStartCommand.ProgramOf(null));
    }

    [Theory]
    [InlineData(new[] { "--hidden" }, true)]
    [InlineData(new[] { "--HIDDEN" }, true)]
    [InlineData(new[] { "--data", @"D:\test", "--hidden" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--hide" }, false)]
    public void StartsHidden_LooksForTheHiddenArgument(string[] arguments, bool expected)
    {
        Assert.Equal(expected, AutoStartCommand.StartsHidden(arguments));
    }
}
