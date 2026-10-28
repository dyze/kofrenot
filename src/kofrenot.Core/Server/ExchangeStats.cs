using System.Globalization;
using System.Text.Json.Serialization;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>What a request to the server exchanged, read from its address.</summary>
public enum ExchangeKind
{
    /// <summary>Change files.</summary>
    Changes,

    /// <summary>Notes' content.</summary>
    Notes,

    /// <summary>Datasets' files, an image field's included.</summary>
    Files,

    Thumbnails,

    /// <summary>The feed, and the vault's other files (its header, its keys).</summary>
    Sync,

    /// <summary>Sign-in, account data, stores and their members.</summary>
    Account,
}

/// <summary>Requests of a kind, those that failed, and their bytes sent (up) and received (down).</summary>
public sealed record ExchangeCount(int Requests, int Failed, long Up, long Down)
{
    public static readonly ExchangeCount None = new(0, 0, 0, 0);

    public ExchangeCount Add(ExchangeCount other) =>
        new(Requests + other.Requests, Failed + other.Failed, Up + other.Up, Down + other.Down);
}

/// <summary>The exchanges of one local day, by kind.</summary>
public sealed record ExchangeDay(DateOnly Day, Dictionary<ExchangeKind, ExchangeCount> Kinds)
{
    [JsonIgnore]
    public ExchangeCount Total => Kinds.Values.Aggregate(ExchangeCount.None, (sum, count) => sum.Add(count));
}

/// <summary>A line of the statistics as shown: a day or a kind, then its numbers, sizes formatted (1.2 MB).</summary>
public sealed record ExchangeRow(string Label, int Requests, int Failed, string Up, string Down)
{
    public static ExchangeRow Of(string label, ExchangeCount count) =>
        new(label, count.Requests, count.Failed, ByteSize.Format(count.Up), ByteSize.Format(count.Down));
}

/// <summary>
/// Statistics of this device's exchanges with the server, to analyse a performance problem:
/// requests, failures and bytes, per local day and per kind, for the last <see cref="DaysKept"/>
/// days. Kept on this device only, in a file of its own (<see cref="Save"/>); numbers only, never
/// an address, a name or an id. Recorded from the threads the requests run on.
/// </summary>
/// <param name="path">Where they are kept; null to keep them in memory only.</param>
public sealed class ExchangeStats(string? path = null, TimeProvider? clock = null, ILog? log = null)
{
    public const int DaysKept = 30;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly LocalJsonFile<List<ExchangeDay>>? _file = path is null ? null : new(path, "Server", "exchange statistics", log);
    private readonly Lock _lock = new();
    private List<ExchangeDay>? _days;
    private bool _changed;

    /// <summary>The days with exchanges, newest first, at most <see cref="DaysKept"/>.</summary>
    public IReadOnlyList<ExchangeDay> Days
    {
        get
        {
            lock (_lock)
            {
                var oldest = Today.AddDays(1 - DaysKept);
                return [.. Loaded().Where(day => day.Day >= oldest).OrderByDescending(day => day.Day).Select(day => day with { Kinds = new(day.Kinds) })];
            }
        }
    }

    /// <summary>A request to <paramref name="address"/> (relative to the server), with its bytes, or one that failed.</summary>
    public void Record(string address, long up, long down, bool failed)
    {
        var kind = KindOf(address);
        var today = Today;
        lock (_lock)
        {
            var days = Loaded();
            if (days.FirstOrDefault(day => day.Day == today) is not { } day)
            {
                days.Add(day = new ExchangeDay(today, []));
                days.RemoveAll(old => old.Day <= today.AddDays(-DaysKept));
            }

            day.Kinds[kind] = day.Kinds.GetValueOrDefault(kind, ExchangeCount.None).Add(new ExchangeCount(1, failed ? 1 : 0, up, down));
            _changed = true;
        }
    }

    /// <summary>Writes them to their file, when they changed since.</summary>
    public void Save()
    {
        List<ExchangeDay> days;
        lock (_lock)
        {
            if (!_changed || _file is null)
            {
                return;
            }

            days = [.. Loaded().Select(day => day with { Kinds = new(day.Kinds) })];
            _changed = false;
        }

        _file.Save(days);
    }

    /// <summary>A line per day, newest first, then the total.</summary>
    public IReadOnlyList<ExchangeRow> DayRows()
    {
        var days = Days;
        return
        [
            .. days.Select(day => ExchangeRow.Of(day.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), day.Total)),
            ExchangeRow.Of("Total", days.Aggregate(ExchangeCount.None, (sum, day) => sum.Add(day.Total))),
        ];
    }

    /// <summary>A line per kind with exchanges, over the days kept.</summary>
    public IReadOnlyList<ExchangeRow> KindRows()
    {
        var days = Days;
        return Enum.GetValues<ExchangeKind>()
            .Select(kind => (Kind: kind, Count: days.Aggregate(ExchangeCount.None, (sum, day) => sum.Add(day.Kinds.GetValueOrDefault(kind, ExchangeCount.None)))))
            .Where(kind => kind.Count.Requests > 0)
            .Select(kind => ExchangeRow.Of(kind.Kind.ToString(), kind.Count))
            .ToList();
    }

    /// <summary>The statistics as plain text, to paste in a message.</summary>
    public string Text()
    {
        static string Line(ExchangeRow row) =>
            string.Create(CultureInfo.InvariantCulture, $"{row.Label,-12} {row.Requests,7} requests  {row.Failed,4} failed  up {row.Up,10}  down {row.Down,10}");
        return string.Join(
            Environment.NewLine,
            [$"Exchanges with the server, last {DaysKept} days, on this device", .. DayRows().Select(Line), string.Empty, "By kind", .. KindRows().Select(Line)]);
    }

    /// <summary>The kind of a request, from its address: a store's file by its folder and extension.</summary>
    public static ExchangeKind KindOf(string address)
    {
        var path = address.Split('?')[0];
        const string Files = "/files/";
        var files = path.IndexOf(Files, StringComparison.Ordinal);
        if (files < 0)
        {
            return path.EndsWith("/feed", StringComparison.Ordinal) ? ExchangeKind.Sync : ExchangeKind.Account;
        }

        var file = path[(files + Files.Length)..];
        return file.StartsWith(VaultFolder.ChangesFolder + "/", StringComparison.Ordinal) ? ExchangeKind.Changes
            : file.EndsWith(VaultFolder.ContentExtension, StringComparison.Ordinal) ? ExchangeKind.Notes
            : file.EndsWith(VaultFolder.DatasetExtension, StringComparison.Ordinal) ? ExchangeKind.Files
            : file.EndsWith(VaultFolder.ThumbnailExtension, StringComparison.Ordinal) ? ExchangeKind.Thumbnails
            : ExchangeKind.Sync;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    // Read from the file the first time: the statistics cost nothing until used.
    private List<ExchangeDay> Loaded() => _days ??= _file?.Load() ?? [];
}
