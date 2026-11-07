namespace Kofrenot.Core.Tests.Browse;

public sealed class IconPathsTests
{
    public static TheoryData<string> Svgs()
    {
        var assembly = typeof(IconPaths).Assembly;
        var data = new TheoryData<string>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith("icons/", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            data.Add(reader.ReadToEnd());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Svgs))]
    public void EachSvg_Is24By24_WithOneFilledPath(string svg)
    {
        Assert.Contains("viewBox=\"0 0 24 24\"", svg, StringComparison.Ordinal);
        Assert.Single(svg.Split("<path").Skip(1));
        Assert.DoesNotContain("stroke", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("transform", svg, StringComparison.Ordinal);
        Assert.NotNull(IconPaths.PathIn(svg));
    }

    [Fact]
    public void EveryNodeIcon_AndEveryTypeIcon_HasItsIcon()
    {
        var names = IconPaths.Names.ToHashSet();

        Assert.All(Enum.GetValues<NodeIcon>(), icon => Assert.Contains(IconPaths.NameOf(icon), names));
        Assert.All(TypeIcons.All, icon => Assert.Contains(IconPaths.NameOf(new NodeVisual(NodeIcon.Generic, TypeIcon: icon)), names));
    }

    [Fact]
    public void ANode_ShowsItsTypesIcon_ElseThatOfWhatItHolds()
    {
        Assert.Equal("contact", IconPaths.NameOf(new NodeVisual(NodeIcon.Note, TypeIcon: "Contact")));
        Assert.Equal("note", IconPaths.NameOf(new NodeVisual(NodeIcon.Note)));
        Assert.Equal("data-object", IconPaths.NameOf(new NodeVisual(NodeIcon.DataObject)));
        Assert.Equal("note", IconPaths.NameOf(new NodeVisual(NodeIcon.Note, TypeIcon: "Unicorn")));
    }

    [Fact]
    public void ATypeIcon_IsNamedInLowerCase_NoneOrUnknownHasNone()
    {
        Assert.Equal("airplane", IconPaths.NameOfType("Airplane"));
        Assert.Equal("birthday-cake", IconPaths.NameOfType("Birthday-cake"));
        Assert.Null(IconPaths.NameOfType(null));
        Assert.Null(IconPaths.NameOfType("Unicorn"));
    }

    [Fact]
    public void AnUnknownName_GivesTheFallback()
    {
        Assert.Equal(IconPaths.PathOf(IconPaths.Fallback), IconPaths.PathOf("no such icon"));
        Assert.NotEqual(IconPaths.PathOf("delete"), IconPaths.PathOf(IconPaths.Fallback));
        Assert.StartsWith("M", IconPaths.PathOf("delete"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnSvgWithoutPath_HasNoPathData()
    {
        Assert.Null(IconPaths.PathIn("<svg viewBox=\"0 0 24 24\"></svg>"));
        Assert.Null(IconPaths.PathIn("<svg><path d=\"\"/></svg>"));
        Assert.Null(IconPaths.PathIn("<svg><path d=\"M 1 1"));
    }

    [Fact]
    public void ThePathData_IsWhatIsBetweenItsQuotes() =>
        Assert.Equal("M 1 1 L 2 2 z", IconPaths.PathIn("<svg viewBox=\"0 0 24 24\"><path fill=\"#000000\" d=\"M 1 1 L 2 2 z\"/></svg>"));
}
