using System.Globalization;

namespace Kofrenot.Core.Tests.Fields;

public sealed class FieldValueTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static string? Parsed(FieldKind kind, string text, CultureInfo? culture = null, params string[] choices) =>
        FieldValue.Parse(kind, text, choices, culture ?? English).Value;

    [Theory]
    [InlineData(FieldKind.Text)]
    [InlineData(FieldKind.Number)]
    [InlineData(FieldKind.Checkbox)]
    public void AnEmptyValue_IsRefused(FieldKind kind)
    {
        var input = FieldValue.Parse(kind, "   ");

        Assert.False(input.IsValid);
        Assert.Null(input.Value);
        Assert.Equal("Type a value", input.Error);
    }

    [Fact]
    public void AText_IsTrimmed()
    {
        Assert.Equal("Frank Herbert", Parsed(FieldKind.Text, "  Frank Herbert "));
    }

    [Theory]
    [InlineData("1234.5", "1234.5")]
    [InlineData("1,234.50", "1234.5")]
    [InlineData("-3", "-3")]
    [InlineData("0.000", "0")]
    public void ANumber_IsWrittenTheSameEverywhere(string typed, string written)
    {
        Assert.Equal(written, Parsed(FieldKind.Number, typed));
    }

    [Fact]
    public void ANumber_IsReadInTheCultureFirst()
    {
        Assert.Equal("1.5", Parsed(FieldKind.Number, "1,5", French));
        Assert.Equal("1.5", Parsed(FieldKind.Number, "1.5", French));
    }

    [Fact]
    public void NotANumber_IsRefused()
    {
        Assert.Equal("Type a number, like 1234.5", FieldValue.Parse(FieldKind.Number, "many", culture: English).Error);
    }

    [Theory]
    [InlineData("2026-10-27")]
    [InlineData("10/27/2026")]
    [InlineData("Oct 27, 2026")]
    public void ADate_IsWrittenAsIso(string typed)
    {
        Assert.Equal("2026-10-27", Parsed(FieldKind.Date, typed));
    }

    [Fact]
    public void ADate_IsReadInTheCulture()
    {
        Assert.Equal("2026-10-27", Parsed(FieldKind.Date, "27/10/2026", French));
    }

    [Fact]
    public void NotADate_IsRefused()
    {
        Assert.Equal("Type a date, like 2026-10-27", FieldValue.Parse(FieldKind.Date, "soon", culture: English).Error);
    }

    [Theory]
    [InlineData("https://example.com/dune", "https://example.com/dune")]
    [InlineData("mailto:frank@example.com", "mailto:frank@example.com")]
    [InlineData("example.com", "https://example.com")]
    [InlineData("www.example.com/dune?x=1", "https://www.example.com/dune?x=1")]
    public void AnAddress_KeepsOrGetsItsScheme(string typed, string written)
    {
        Assert.Equal(written, Parsed(FieldKind.Url, typed));
    }

    [Theory]
    [InlineData("dune")]
    [InlineData("an address")]
    [InlineData("/home/frank")]
    [InlineData("C:\\dune.txt")]
    [InlineData("file:///C:/dune.txt")]
    public void NotAnAddress_IsRefused(string typed)
    {
        Assert.Equal("Type an address, like https://example.com", FieldValue.Parse(FieldKind.Url, typed).Error);
    }

    [Theory]
    [InlineData("yes", "true")]
    [InlineData("TRUE", "true")]
    [InlineData("1", "true")]
    [InlineData("no", "false")]
    [InlineData("false", "false")]
    [InlineData("0", "false")]
    public void ACheckbox_IsTrueOrFalse(string typed, string written)
    {
        Assert.Equal(written, Parsed(FieldKind.Checkbox, typed));
    }

    [Fact]
    public void NotACheckbox_IsRefused()
    {
        Assert.Equal("Type yes or no", FieldValue.Parse(FieldKind.Checkbox, "maybe").Error);
    }

    [Fact]
    public void ACheckbox_IsWrittenAndRead()
    {
        Assert.Equal("true", FieldValue.Of(true));
        Assert.Equal("false", FieldValue.Of(false));
        Assert.True(FieldValue.IsChecked("true"));
        Assert.False(FieldValue.IsChecked("false"));
        Assert.False(FieldValue.IsChecked(null));
    }

    [Fact]
    public void AChoice_IsOneOfItsOptions_AsWritten()
    {
        Assert.Equal("Novel", Parsed(FieldKind.Choice, "novel", null, "Novel", "Short story"));
    }

    [Fact]
    public void AnotherChoice_IsRefused()
    {
        Assert.Equal("Choose one of: Novel, Short story", FieldValue.Parse(FieldKind.Choice, "Poem", ["Novel", "Short story"]).Error);
        Assert.Equal("This field has no options", FieldValue.Parse(FieldKind.Choice, "Poem").Error);
    }

    [Theory]
    [InlineData(FieldKind.Markdown)]
    [InlineData(FieldKind.File)]
    [InlineData(FieldKind.View)]
    public void Content_IsNotTypedIn(FieldKind kind)
    {
        Assert.Equal($"A {FieldKinds.NameOf(kind)} is not typed in", FieldValue.Parse(kind, "text").Error);
    }

    [Theory]
    [InlineData(FieldKind.Text, "Dune", true)]
    [InlineData(FieldKind.Text, "", false)]
    [InlineData(FieldKind.Number, "1234.5", true)]
    [InlineData(FieldKind.Number, "1,234.5", false)]
    [InlineData(FieldKind.Number, "1.50", false)]
    [InlineData(FieldKind.Date, "2026-10-27", true)]
    [InlineData(FieldKind.Date, "27/10/2026", false)]
    [InlineData(FieldKind.Url, "https://example.com", true)]
    [InlineData(FieldKind.Url, "example.com", false)]
    [InlineData(FieldKind.Checkbox, "true", true)]
    [InlineData(FieldKind.Checkbox, "yes", false)]
    [InlineData(FieldKind.Choice, "Removed option", true)]
    [InlineData(FieldKind.Choice, "", false)]
    [InlineData(FieldKind.Markdown, "# Dune", false)]
    public void AValueRead_IsValid_OnlyAsWritten(FieldKind kind, string value, bool isValid)
    {
        Assert.Equal(isValid, FieldValue.IsValid(kind, value));
    }

    [Fact]
    public void AValue_IsShownAsWritten_ACheckboxAsYesOrNo()
    {
        Assert.Equal("1234.5", FieldValue.Display(FieldKind.Number, "1234.5"));
        Assert.Equal("Yes", FieldValue.Display(FieldKind.Checkbox, "true"));
        Assert.Equal("No", FieldValue.Display(FieldKind.Checkbox, "false"));
    }

    [Fact]
    public void TheCurrentCulture_IsUsedByDefault()
    {
        Assert.Equal("12", FieldValue.Parse(FieldKind.Number, "12").Value);
    }
}

public sealed class FieldKindsTests
{
    [Fact]
    public void TheSmallKinds_AreWrittenInLowerCase_AndReadBack()
    {
        foreach (var kind in FieldKinds.Small)
        {
            Assert.True(kind.IsSmall());
            Assert.Equal(kind, FieldKinds.Parse(FieldKinds.NameOf(kind)));
        }

        Assert.Equal("checkbox", FieldKinds.NameOf(FieldKind.Checkbox));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("markdown")]
    [InlineData("Text")]
    [InlineData("colour")]
    public void AContentKindOrAnUnknownName_IsNotRead(string? name)
    {
        Assert.Null(FieldKinds.Parse(name));
    }

    [Theory]
    [InlineData(FieldKind.Markdown)]
    [InlineData(FieldKind.File)]
    [InlineData(FieldKind.View)]
    public void ContentKinds_AreNotSmall(FieldKind kind)
    {
        Assert.False(kind.IsSmall());
    }
}
