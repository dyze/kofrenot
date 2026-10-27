using System.Globalization;
using System.Reflection;

namespace Kofrenot.Core;

/// <summary>
/// Reads the product version stamped into an assembly by the build
/// (the single <c>Version</c> property in <c>Directory.Build.props</c>).
/// The build adds the git commit after '+', then its date in seconds since 1970 (UTC), and ".dirty"
/// when the working tree had uncommitted changes: "0.4+4e9c3137….1793110700.dirty".
/// </summary>
public static class AppVersion
{
    private const int ShortCommitLength = 7;
    private const string DirtyPart = "dirty";
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Version of the given assembly, as shown to the user (for example "0.1").</summary>
    public static string Of(Assembly assembly) => Format(BuildOf(assembly));

    /// <summary>Version of the given assembly with the commit it was built from (for example "0.4 (4e9c313, 2026-10-27 14:05)").</summary>
    public static string WithCommitOf(Assembly assembly) => FormatWithCommit(BuildOf(assembly));

    /// <summary>The full build of the given assembly, to tell builds apart: "0.4+&lt;commit&gt;[.&lt;date&gt;][.dirty]".</summary>
    public static string? BuildOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString();

    /// <summary>Drops the build metadata the SDK appends after '+' (the source revision).</summary>
    public static string Format(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unknown";
        }

        var plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }

    /// <summary>
    /// The version followed by the short commit, "-dirty" for a build with uncommitted changes, and
    /// the commit's date in <paramref name="zone"/> (local by default): "0.4 (4e9c313, 2026-10-27 14:05)",
    /// "0.4 (4e9c313-dirty, 2026-10-27 14:05)". Built before the date was stamped, without it: "0.4 (4e9c313)".
    /// Without a commit (built outside git), the version alone.
    /// </summary>
    public static string FormatWithCommit(string? raw, TimeZoneInfo? zone = null)
    {
        var version = Format(raw);
        var plus = raw?.IndexOf('+') ?? -1;
        if (plus < 0)
        {
            return version;
        }

        var parts = raw![(plus + 1)..].Split('.');
        var commit = parts[0];
        if (commit.Length == 0)
        {
            return version;
        }

        var isDirty = parts[^1] == DirtyPart;
        var date = parts.Length > 1 ? DateOf(parts[1], zone ?? TimeZoneInfo.Local) : null;
        var shortCommit = commit.Length > ShortCommitLength ? commit[..ShortCommitLength] : commit;
        return $"{version} ({shortCommit}{(isDirty ? "-dirty" : string.Empty)}{(date is null ? string.Empty : $", {date}")})";
    }

    // The commit's date stamped by the build (seconds since 1970, UTC), shown in the zone; null when it is not one.
    private static string? DateOf(string seconds, TimeZoneInfo zone) =>
        long.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            ? TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(value), zone).ToString(DateFormat, CultureInfo.InvariantCulture)
            : null;
}
