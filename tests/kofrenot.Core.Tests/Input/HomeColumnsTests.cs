namespace Kofrenot.Core.Tests.Input;

public sealed class HomeColumnsTests
{
    // Node rows 40 high, headers 20: a 200 high column holds 5 node rows.
    private static HomeRoom Room(double width, double height = 200) => new(width, height, RowHeight: 40, HeaderHeight: 20);

    [Theory]
    [InlineData(100, 1)]
    [InlineData(639, 1)]
    [InlineData(640, 2)]
    [InlineData(960, 3)]
    [InlineData(5000, HomeRoom.MaxColumns)]
    public void TheWidth_GivesTheColumns_EachAtLeastTheMinimum_AtMostTheMaximum(double width, int columns) =>
        Assert.Equal(columns, Room(width).Columns);

    [Fact]
    public void TheMostRows_FillEveryColumn_AndAtLeastOne()
    {
        Assert.Equal(10, Room(640).MaxRows);
        Assert.Equal(1, Room(300, height: 10).MaxRows);
        Assert.Equal(10, new HomeRoom(300, 10, RowHeight: 0, HeaderHeight: 0).MaxRows);
    }

    [Fact]
    public void RowsThatFit_StayInOneColumn()
    {
        var columns = HomeColumns.Of(Rows("H n n n"), Room(960), kept: 0);

        Assert.Equal([0], columns.Starts);
        Assert.Equal(4, columns.Shown);
    }

    [Fact]
    public void RowsFlowDown_ThenIntoTheNextColumn_AndStopWhenTheLastIsFull()
    {
        // 20 + 4 × 40 = 180: the fifth node goes to the next column.
        var columns = HomeColumns.Of(Rows("H n n n n n n n n n n n"), Room(640), kept: 0);

        Assert.Equal([0, 5], columns.Starts);
        Assert.Equal(10, columns.Shown);
    }

    [Fact]
    public void AHeader_IsNeverLeftAlone_AtAColumnsBottom()
    {
        // 20 + 3 × 40 = 140: a header (160) would fit, not with its node under it (200).
        var columns = HomeColumns.Of(Rows("H n n n H n n"), Room(640, height: 190), kept: 4);

        Assert.Equal([0, 4], columns.Starts);
        Assert.Equal(7, columns.Shown);
    }

    [Fact]
    public void AHeader_WithNoRoomForItsFirstRow_IsNotShown()
    {
        var columns = HomeColumns.Of(Rows("H n n n H n n"), Room(300, height: 190), kept: 4);

        Assert.Equal([0], columns.Starts);
        Assert.Equal(4, columns.Shown);
    }

    [Fact]
    public void TheKeptRows_AreAllShown_EvenPastTheRoom()
    {
        var columns = HomeColumns.Of(Rows("H n n n n n n n H n"), Room(300), kept: 8);

        Assert.Equal([0], columns.Starts);
        Assert.Equal(8, columns.Shown);
    }

    [Fact]
    public void AColumnTooShort_StillHoldsARow()
    {
        var columns = HomeColumns.Of(Rows("n n n"), Room(640, height: 10), kept: 0);

        Assert.Equal([0, 1], columns.Starts);
        Assert.Equal(2, columns.Shown);
    }

    [Theory]
    [InlineData(2, 1, 6)]
    [InlineData(3, 1, 7)]
    [InlineData(4, 1, 9)] // level with a header: the row under it
    [InlineData(7, 1, 9)] // past the column's end: its last row
    [InlineData(4, -1, 1)]
    [InlineData(9, -1, 5)]
    public void Beside_IsTheRowLevelWithIt_OrTheNearest(int index, int step, int beside) =>
        Assert.Equal(beside, HomeColumns.Beside([0, 4, 8], Selectable("H n n n n n n n H n"), index, step));

    [Fact]
    public void Beside_TwoRowsAsNear_IsTheUpperOne() =>
        Assert.Equal(3, HomeColumns.Beside([0, 3], Selectable("n n n n H n"), 1, 1));

    [Theory]
    [InlineData(1, -1)]
    [InlineData(9, 1)]
    public void Beside_TheFirstOrLastColumn_IsNothing(int index, int step) =>
        Assert.Null(HomeColumns.Beside([0, 4, 8], Selectable("H n n n n n n n H n"), index, step));

    [Fact]
    public void Beside_AColumnWithNoRowToSelect_IsNothing() =>
        Assert.Null(HomeColumns.Beside([0, 2], Selectable("n n H"), 0, 1));

    private static List<bool> Selectable(string kinds) => Rows(kinds).Select(header => !header).ToList();

    private static List<bool> Rows(string kinds) => kinds.Split(' ').Select(kind => kind == "H").ToList();
}
