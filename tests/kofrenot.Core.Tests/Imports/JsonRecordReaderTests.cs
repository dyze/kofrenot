namespace Kofrenot.Core.Tests.Imports;

public sealed class JsonRecordReaderTests
{
    [Fact]
    public void EachObject_IsARecord_ItsNestedKeysDotted_ItsArraysSeveralValues()
    {
        var file = BookVault.Read("""
            [
              { "title": " Dune ", "year": 1965, "read": true, "publisher": { "name": "Ace", "city": null }, "authors": ["Frank Herbert"] },
              { "title": "Good Omens", "authors": [{ "name": "Terry Pratchett" }, { "name": "Neil Gaiman" }], "notes": "", "tags": [] }
            ]
            """);

        Assert.Equal([1, 2], file.Records.Select(record => record.Number));
        Assert.Equal(["Dune"], file.Records[0].ValuesOf("title"));
        Assert.Equal(("1965", "true", "Ace"), (file.Records[0].FirstOf("year"), file.Records[0].FirstOf("read"), file.Records[0].FirstOf("publisher.name")));
        Assert.Equal(["Terry Pratchett", "Neil Gaiman"], file.Records[1].ValuesOf("authors.name"));
        Assert.Empty(file.Records[1].ValuesOf("notes"));
        Assert.Null(file.Records[1].FirstOf("year"));
        Assert.Equal(["title", "year", "read", "publisher.name", "authors", "authors.name"], file.Keys);
    }

    [Fact]
    public void FalseIsAValue() =>
        Assert.Equal("false", BookVault.Read("[{ \"read\": false }]").Records[0].FirstOf("read"));

    [Fact]
    public void AnEmptyArray_HasNoRecord() =>
        Assert.Empty(BookVault.Read("[]").Records);

    [Theory]
    [InlineData("{ \"title\": \"Dune\" }", "does not hold an array")]
    [InlineData("[{ \"title\": \"Dune\" }, 3]", "Record 2 is not an object")]
    [InlineData("[{ \"title\": ", "not valid JSON")]
    public void AFileNotAnArrayOfObjects_IsRefused(string json, string message) =>
        Assert.Contains(message, Assert.Throws<InvalidDataException>(() => BookVault.Read(json)).Message);
}
