namespace Kofrenot.Core.Tests.Notes;

public sealed class TaskListEditingTests
{
    // In the note: checked, open, checked, open.
    private static TaskListEditing Mixed() => new([new("a", true), new("b", false), new("c", true), new("d", false)]);

    [Fact]
    public void Shown_ListsTheOpenItems_TheCheckedOnesCollapsed()
    {
        var editing = Mixed();

        Assert.Equal([1, 3], editing.Shown);
        Assert.Equal(2, editing.CheckedCount);
        Assert.False(editing.ShowsChecked);
    }

    [Fact]
    public void ToggleChecked_ShowsTheCheckedItemsAfterTheOpenOnes_ThenHidesThem()
    {
        var editing = Mixed();

        editing.ToggleChecked();
        Assert.Equal([1, 3, 0, 2], editing.Shown);

        editing.ToggleChecked();
        Assert.Equal([1, 3], editing.Shown);
    }

    [Fact]
    public void Add_PutsAnEmptyItemAtTheEnd_ShownLastOfTheOpenItems()
    {
        var editing = Mixed();
        editing.ToggleChecked();

        var added = editing.Add();

        Assert.Equal(4, added);
        Assert.Equal(new TaskItem(string.Empty, false), editing.Items[added]);
        Assert.Equal([1, 3, 4, 0, 2], editing.Shown);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(-3, 0)]
    [InlineData(9, 4)]
    public void Insert_PutsAnEmptyItemAtTheIndex_KeptWithinTheList(int index, int inserted)
    {
        var editing = Mixed();

        Assert.Equal(inserted, editing.Insert(index));
        Assert.Equal(new TaskItem(string.Empty, false), editing.Items[inserted]);
        Assert.Equal(5, editing.Items.Count);
    }

    [Fact]
    public void Check_MovesTheItemToItsPart()
    {
        var editing = Mixed();

        editing.Check(1, true);

        Assert.Equal([3], editing.Shown);
        Assert.Equal(3, editing.CheckedCount);
    }

    [Fact]
    public void Edit_ChangesTheText_AndKeepsTheCheck()
    {
        var editing = Mixed();

        editing.Edit(0, "changed");

        Assert.Equal(new TaskItem("changed", true), editing.Items[0]);
    }

    [Fact]
    public void Remove_GivesTheItemShownBeforeIt_AtItsIndexOnceGone()
    {
        var editing = Mixed();
        editing.ToggleChecked();

        // Shown: b, d, a, c. Removing a (index 0): d goes from 3 to 2.
        Assert.Equal(2, editing.Remove(0));
        Assert.Equal(["b", "c", "d"], editing.Items.Select(item => item.Text));
    }

    [Fact]
    public void Remove_GivesTheItemShownBeforeIt_WhenItComesFirstInTheNote()
    {
        var editing = Mixed();

        Assert.Equal(1, editing.Remove(3));
    }

    [Fact]
    public void Remove_GivesNothing_ForTheItemShownFirst()
    {
        var editing = Mixed();

        Assert.Null(editing.Remove(1));
        Assert.Equal(3, editing.Items.Count);
    }

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(3, -1, 1)]
    [InlineData(1, -1, null)]
    [InlineData(3, 1, null)]
    public void Neighbour_IsTheItemShownNextToIt_InItsPart(int index, int step, int? neighbour) =>
        Assert.Equal(neighbour, Mixed().Neighbour(index, step));

    [Fact]
    public void Neighbour_IsNothing_AcrossTheParts()
    {
        var editing = Mixed();
        editing.ToggleChecked();

        Assert.Null(editing.Neighbour(3, 1));
        Assert.Null(editing.Neighbour(0, -1));
    }

    [Fact]
    public void Neighbour_IsNothing_ForAnItemNotShown() =>
        Assert.Null(Mixed().Neighbour(0, 1));

    [Fact]
    public void Move_SwapsWithItsNeighbour_InItsPart()
    {
        var editing = Mixed();

        Assert.True(editing.Move(3, 1));
        Assert.Equal(["a", "d", "b", "c"], editing.Items.Select(item => item.Text));
    }

    [Fact]
    public void Move_LeavesTheItems_AcrossTheParts_OrInPlace()
    {
        var editing = Mixed();

        Assert.False(editing.Move(1, 2));
        Assert.False(editing.Move(1, 1));
        Assert.Equal(["a", "b", "c", "d"], editing.Items.Select(item => item.Text));
    }

    [Fact]
    public void Drop_PutsTheItemBeforeTheTarget()
    {
        var editing = Mixed();

        Assert.Equal(1, editing.Drop(3, 1, before: true));
        Assert.Equal(["a", "d", "b", "c"], editing.Items.Select(item => item.Text));
    }

    [Fact]
    public void Drop_PutsTheItemAfterTheTarget_OverItemsOfTheOtherPart()
    {
        var editing = Mixed();

        Assert.Equal(3, editing.Drop(1, 3, before: false));
        Assert.Equal(["a", "c", "d", "b"], editing.Items.Select(item => item.Text));
    }

    [Fact]
    public void Drop_PutsTheItemBeforeTheTarget_OverItemsOfTheOtherPart()
    {
        var editing = Mixed();

        Assert.Equal(2, editing.Drop(1, 3, before: true));
        Assert.Equal(["a", "c", "b", "d"], editing.Items.Select(item => item.Text));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 9)]
    [InlineData(-1, 1)]
    public void Drop_MovesNothing_OntoTheOtherPart_OrOutOfTheList(int from, int target)
    {
        var editing = Mixed();

        Assert.False(editing.CanDrop(from, target));
        Assert.Null(editing.Drop(from, target, before: true));
    }

    [Fact]
    public void Drop_MovesNothing_InItsOwnPlace() =>
        Assert.Null(Mixed().Drop(1, 1, before: true));
}
