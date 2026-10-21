namespace Kofrenot.Core.Tests.Server;

public sealed class KofreVersionTests
{
    [Theory]
    [InlineData("0.2", true)]
    [InlineData("0.10", true)]
    [InlineData("1.0", true)]
    [InlineData("0.1", false)]
    [InlineData(null, false)]
    [InlineData("junk", false)]
    public void OnlyAServerFrom0_2_IsSupported(string? version, bool supported) =>
        Assert.Equal(supported, KofreVersion.IsSupported(version));

    [Fact]
    public void AServerTooOld_IsNamed_WithTheVersionNeeded()
    {
        Assert.Contains("runs Kofre 0.1: kofrenot needs 0.2", KofreVersion.TooOld(null));
        Assert.Contains("runs Kofre 0.1.5", KofreVersion.TooOld("0.1.5"));
    }
}
