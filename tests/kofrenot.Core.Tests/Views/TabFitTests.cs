using Kofrenot.Core.Views;

namespace Kofrenot.Core.Tests.Views;

public sealed class TabFitTests
{
    [Fact]
    public void Fitting_ShowsEveryTab_WhenAllFit() =>
        Assert.Equal([0, 1, 2], TabFit.Fitting([10, 20, 30], 0, 60));

    [Fact]
    public void Fitting_LeavesOutTheTabsThatDoNotFitWhole() =>
        Assert.Equal([0, 1], TabFit.Fitting([10, 20, 30], 1, 59));

    [Fact]
    public void Fitting_PutsTheSelectedTabInPlaceOfTheLastOnes() =>
        Assert.Equal([0, 3], TabFit.Fitting([10, 20, 20, 25], 3, 50));

    [Fact]
    public void Fitting_ShowsTheSelectedTabAlone_WhenNothingElseFits() =>
        Assert.Equal([2], TabFit.Fitting([10, 20, 80], 2, 50));

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(5)]
    public void Fitting_KeepsTheFirstTabsThatFit_WhenNoneIsSelected(int? selected) =>
        Assert.Equal([0], TabFit.Fitting([10, 20], selected, 25));

    [Fact]
    public void Fitting_ShowsNothing_WithoutTabs() =>
        Assert.Empty(TabFit.Fitting([], null, 100));
}
