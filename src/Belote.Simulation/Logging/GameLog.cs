using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Belote.Core;

namespace Belote.Simulation;

/// <summary>A rejected action seen by the runner (kept for debugging; not part of the log hash).</summary>
public sealed record RejectionRecord(int AfterSequence, Seat Seat, PlayerAction Action, RejectionReason Reason);

/// <summary>
/// The omniscient, ordered record of one game: header (seed, full rule set, seat controllers) and
/// every event. It is a developer/test artifact and must never be sent to a seat controller.
/// </summary>
public sealed class GameLog
{
    public const int LogVersion = 1;

    private string? _hash;
    private string? _lines;

    public GameLog(
        ulong seed,
        RuleSet ruleSet,
        IReadOnlyDictionary<Seat, string> seats,
        IReadOnlyList<GameEvent> events,
        IReadOnlyList<RejectionRecord>? rejections = null)
    {
        Seed = seed;
        RuleSet = ruleSet;
        Seats = seats;
        Events = events;
        Rejections = rejections ?? Array.Empty<RejectionRecord>();
    }

    public ulong Seed { get; }

    public RuleSet RuleSet { get; }

    public IReadOnlyDictionary<Seat, string> Seats { get; }

    public IReadOnlyList<GameEvent> Events { get; }

    public IReadOnlyList<RejectionRecord> Rejections { get; }

    /// <summary>SHA-256 (hex) of the canonical header and event lines; rejections are excluded.</summary>
    public string Hash => _hash ??= Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToJsonLines())));

    /// <summary>Canonical JSON Lines: header first, then one event per line.</summary>
    public string ToJsonLines() => _lines ??= Render(includeRejections: false);

    /// <summary>JSON Lines with rejection records interleaved after the event they follow.</summary>
    public string ToJsonLinesWithRejections() => Render(includeRejections: true);

    private string Render(bool includeRejections)
    {
        var sb = new StringBuilder();
        sb.Append(HeaderLine()).Append('\n');

        int rejectionIndex = 0;
        void FlushRejections(int sequence)
        {
            while (includeRejections && rejectionIndex < Rejections.Count
                && Rejections[rejectionIndex].AfterSequence <= sequence)
            {
                sb.Append(RejectionLine(Rejections[rejectionIndex++])).Append('\n');
            }
        }

        foreach (var e in Events)
        {
            sb.Append(Line(w => EventJson.Write(w, e))).Append('\n');
            FlushRejections(e.Sequence);
        }

        FlushRejections(int.MaxValue);
        return sb.ToString();
    }

    private string HeaderLine() => Line(w =>
    {
        w.WriteStartObject();
        w.WriteString("type", "header");
        w.WriteNumber("logVersion", LogVersion);
        w.WriteNumber("seed", Seed);
        w.WritePropertyName("ruleSet");
        RuleSetJson.Write(w, RuleSet);
        w.WriteStartObject("seats");
        foreach (var seat in Belote.Core.Seats.All)
        {
            w.WriteString(JsonNames.Of(seat), Seats.TryGetValue(seat, out var name) ? name : "unknown");
        }

        w.WriteEndObject();
        w.WriteEndObject();
    });

    private static string RejectionLine(RejectionRecord r) => Line(w =>
    {
        w.WriteStartObject();
        w.WriteString("type", "ActionRejected");
        w.WriteNumber("afterSeq", r.AfterSequence);
        w.WriteString("seat", JsonNames.Of(r.Seat));
        EventJson.WriteAction(w, "action", r.Action);
        w.WriteString("reason", JsonNames.Of(r.Reason));
        w.WriteEndObject();
    });

    private static string Line(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            write(w);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Canonical JSON line of a single event (used by the auditor to compare streams).</summary>
    public static string EventLine(GameEvent e) => Line(w => EventJson.Write(w, e));

    /// <summary>Parses a JSON Lines log; rejection records are read back too.</summary>
    public static GameLog FromJsonLines(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            throw new FormatException("Empty log.");
        }

        using var header = JsonDocument.Parse(lines[0]);
        var root = header.RootElement;
        if (root.GetProperty("type").GetString() != "header")
        {
            throw new FormatException("First line must be the log header.");
        }

        ulong seed = root.GetProperty("seed").GetUInt64();
        var ruleSet = RuleSetJson.Read(root.GetProperty("ruleSet"));
        var seats = new Dictionary<Seat, string>();
        foreach (var prop in root.GetProperty("seats").EnumerateObject())
        {
            seats[JsonNames.ParseSeat(prop.Name)] = prop.Value.GetString()!;
        }

        var events = new List<GameEvent>();
        var rejections = new List<RejectionRecord>();
        foreach (var line in lines.Skip(1))
        {
            using var doc = JsonDocument.Parse(line);
            var e = doc.RootElement;
            if (e.GetProperty("type").GetString() == "ActionRejected")
            {
                rejections.Add(new RejectionRecord(
                    e.GetProperty("afterSeq").GetInt32(),
                    JsonNames.ParseSeat(e.GetProperty("seat").GetString()!),
                    EventJson.ReadAction(e.GetProperty("action")),
                    Enum.Parse<RejectionReason>(e.GetProperty("reason").GetString()!)));
            }
            else
            {
                events.Add(EventJson.Read(e));
            }
        }

        return new GameLog(seed, ruleSet, seats, events, rejections);
    }
}
