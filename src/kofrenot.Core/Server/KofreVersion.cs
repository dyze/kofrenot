namespace Kofrenot.Core.Server;

/// <summary>The Kofre server versions this kofrenot works with.</summary>
public static class KofreVersion
{
    /// <summary>The first version keeping account data (the account's identity): 0.2.</summary>
    public static readonly Version Minimum = new(0, 2);

    /// <summary>Whether a server of <paramref name="version"/> (null: older than 0.2, it did not say) is recent enough.</summary>
    public static bool IsSupported(string? version) => Version.TryParse(version, out var parsed) && parsed >= Minimum;

    /// <summary>What the user is told about a server too old.</summary>
    public static string TooOld(string? version) =>
        $"This server runs Kofre {version ?? "0.1"}: kofrenot needs {Minimum} or later for its account's pass phrase. Ask its admin to upgrade it.";
}
