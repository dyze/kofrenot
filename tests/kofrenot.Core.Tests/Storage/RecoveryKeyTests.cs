using System.Text.RegularExpressions;

namespace Kofrenot.Core.Tests.Storage;

public sealed partial class RecoveryKeyTests
{
    [Fact]
    public void AKey_IsShownAs13GroupsOf4()
    {
        var shown = RecoveryKey.New().ToString();

        Assert.Matches(Groups(), shown);
    }

    [Fact]
    public void AShownKey_IsReadBack_AsTheSameKey()
    {
        var key = RecoveryKey.New();

        Assert.Equal(key.ToString(), RecoveryKey.Parse(key.ToString())?.ToString());
    }

    [Fact]
    public void Case_Spaces_AndDashes_DoNotMatter()
    {
        var shown = RecoveryKey.New().ToString();
        var typed = " " + shown.ToLowerInvariant().Replace("-", " ") + "\n";

        Assert.Equal(shown, RecoveryKey.Parse(typed)?.ToString());
    }

    [Fact]
    public void DigitsLookingLikeLetters_AreReadAsThose()
    {
        var shown = "OIB" + RecoveryKey.New().ToString().Replace("-", string.Empty)[3..^1] + "A";

        Assert.Equal(RecoveryKey.Parse(shown)?.ToString(), RecoveryKey.Parse("018" + shown[3..])?.ToString());
        Assert.NotNull(RecoveryKey.Parse(shown));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCD-EFGH")]
    [InlineData("ABCD-EFGH-IJKL-MNOP-QRST-UVWX-YZ23-4567-ABCD-EFGH-IJKL-MNOP-QRS9")]
    [InlineData("ABCD-EFGH-IJKL-MNOP-QRST-UVWX-YZ23-4567-ABCD-EFGH-IJKL-MNOP-QRSB")]
    public void SomethingElse_IsNoKey(string typed) =>
        Assert.Null(RecoveryKey.Parse(typed));

    [Fact]
    public void TwoNewKeys_Differ() =>
        Assert.NotEqual(RecoveryKey.New().ToString(), RecoveryKey.New().ToString());

    [GeneratedRegex("^([A-Z2-7]{4}-){12}[A-Z2-7]{4}$")]
    private static partial Regex Groups();
}
