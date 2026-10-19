using System.Reflection;

namespace Kofrenot.Core;

/// <summary>
/// Reads the product version stamped into an assembly by the build
/// (the single <c>Version</c> property in <c>Directory.Build.props</c>).
/// The build adds the git commit after '+', and ".dirty" when the working tree had uncommitted changes:
/// "0.4+4e9c3137….dirty".
/// </summary>
public static class AppVersion
{
    private const int ShortCommitLength = 7;
    private const string DirtySuffix = ".dirty";

    /// <summary>Version of the given assembly, as shown to the user (for example "0.1").</summary>
    public static string Of(Assembly assembly) => Format(BuildOf(assembly));

    /// <summary>Version of the given assembly with the commit it was built from (for example "0.4 (4e9c313)").</summary>
    public static string WithCommitOf(Assembly assembly) => FormatWithCommit(BuildOf(assembly));

    /// <summary>The full build of the given assembly, to tell builds apart: "0.4+&lt;commit&gt;[.dirty]".</summary>
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
    /// The version followed by the short commit, and "-dirty" for a build with uncommitted changes:
    /// "0.4 (4e9c313)", "0.4 (4e9c313-dirty)". Without a commit (built outside git), the version alone.
    /// </summary>
    public static string FormatWithCommit(string? raw)
    {
        var version = Format(raw);
        var plus = raw?.IndexOf('+') ?? -1;
        if (plus < 0)
        {
            return version;
        }

        var metadata = raw![(plus + 1)..];
        var isDirty = metadata.EndsWith(DirtySuffix, StringComparison.Ordinal);
        var commit = isDirty ? metadata[..^DirtySuffix.Length] : metadata;
        if (commit.Length == 0)
        {
            return version;
        }

        var shortCommit = commit.Length > ShortCommitLength ? commit[..ShortCommitLength] : commit;
        return $"{version} ({shortCommit}{(isDirty ? "-dirty" : string.Empty)})";
    }
}
