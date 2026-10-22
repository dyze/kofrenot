using System.Text.RegularExpressions;

namespace Kofrenot.Core.Tests.Storage;

public sealed partial class FingerprintTests
{
    [Fact]
    public void AKeysFingerprint_Is8GroupsOf4_AndTheSameEachTime()
    {
        var (identity, _, _) = Identity.Create("secret", iterations: 10);

        var fingerprint = Fingerprint.Of(identity.PublicKey);

        Assert.Matches(Groups(), fingerprint);
        Assert.Equal(fingerprint, Fingerprint.Of(identity.PublicKey.ToArray()));
    }

    [Fact]
    public void AnotherKey_HasAnotherFingerprint()
    {
        var (first, _, _) = Identity.Create("secret", iterations: 10);
        var (second, _, _) = Identity.Create("secret", iterations: 10);

        Assert.NotEqual(Fingerprint.Of(first.PublicKey), Fingerprint.Of(second.PublicKey));
    }

    [GeneratedRegex("^([A-Z2-7]{4}-){7}[A-Z2-7]{4}$")]
    private static partial Regex Groups();
}
