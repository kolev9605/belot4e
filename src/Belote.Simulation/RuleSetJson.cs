using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Belote.Core;

namespace Belote.Simulation;

/// <summary>
/// JSON form of a <see cref="RuleSet"/> (see contracts/rule-set.md). Point arrays are indexed by rank in
/// natural order: 7, 8, 9, 10, J, Q, K, A. Orders are listed strongest first.
/// </summary>
public static class RuleSetJson
{
    public static string ToJson(RuleSet rules, bool indented = false)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            Write(w, rules);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static RuleSet Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Read(doc.RootElement);
    }

    public static void Write(Utf8JsonWriter w, RuleSet r)
    {
        w.WriteStartObject();
        w.WriteString("name", r.Name);
        w.WriteNumber("version", r.Version);

        w.WriteStartObject("deal");
        WriteInts(w, "pattern", r.Deal.Pattern);
        w.WriteNumber("biddingAfterPacket", r.Deal.BiddingAfterPacket);
        w.WriteEndObject();

        w.WriteStartObject("bidding");
        w.WriteStartArray("ranking");
        foreach (var kind in r.Bidding.Ranking)
        {
            w.WriteStringValue(JsonNames.Of(kind));
        }

        w.WriteEndArray();
        w.WriteBoolean("contra", r.Bidding.Contra);
        w.WriteBoolean("recontra", r.Bidding.Recontra);
        w.WriteNumber("closeAfterConsecutivePasses", r.Bidding.CloseAfterConsecutivePasses);
        w.WriteBoolean("closeOnRecontra", r.Bidding.CloseOnRecontra);
        w.WriteString("allPass", r.Bidding.AllPass.ToString());
        w.WriteEndObject();

        w.WriteStartObject("contracts");
        w.WriteStartObject("suit");
        WriteTable(w, "trump", r.Contracts.Suit.Trump);
        WriteTable(w, "plain", r.Contracts.Suit.Plain);
        w.WriteNumber("lastTrickBonus", r.Contracts.Suit.LastTrickBonus);
        w.WriteEndObject();
        w.WriteStartObject("noTrump");
        WriteTable(w, "table", r.Contracts.NoTrump.Table);
        w.WriteNumber("lastTrickBonus", r.Contracts.NoTrump.LastTrickBonus);
        w.WriteEndObject();
        w.WriteStartObject("allTrump");
        WriteTable(w, "table", r.Contracts.AllTrump.Table);
        w.WriteNumber("lastTrickBonus", r.Contracts.AllTrump.LastTrickBonus);
        w.WriteEndObject();
        w.WriteEndObject();

        w.WriteStartObject("play");
        w.WriteBoolean("mustFollowSuit", r.Play.MustFollowSuit);
        w.WriteBoolean("cutOnlyWhenOpponentWinning", r.Play.CutOnlyWhenOpponentWinning);
        w.WriteBoolean("mustOvertrump", r.Play.MustOvertrump);
        w.WriteBoolean("mustOvertakeInAllTrump", r.Play.MustOvertakeInAllTrump);
        w.WriteBoolean("anyCardWhenCannotOvertrump", r.Play.AnyCardWhenCannotOvertrump);
        w.WriteEndObject();

        var a = r.Announcements;
        w.WriteStartObject("announcements");
        w.WriteBoolean("allowedInSuit", a.AllowedInSuit);
        w.WriteBoolean("allowedInNoTrump", a.AllowedInNoTrump);
        w.WriteBoolean("allowedInAllTrump", a.AllowedInAllTrump);
        w.WriteStartArray("sequences");
        foreach (var s in a.Sequences)
        {
            w.WriteStartObject();
            w.WriteNumber("minLength", s.MinLength);
            w.WriteNumber("points", s.Points);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        WriteInts(w, "carrePoints", a.CarrePoints);
        WriteRanks(w, "carreOrder", a.CarreOrder);
        w.WriteNumber("belotePoints", a.BelotePoints);
        w.WriteBoolean("beloteInSuit", a.BeloteInSuit);
        w.WriteBoolean("beloteInNoTrump", a.BeloteInNoTrump);
        w.WriteBoolean("beloteInAllTrump", a.BeloteInAllTrump);
        w.WriteString("comparison", a.Comparison.ToString());
        w.WriteNumber("revealAfterTrick", a.RevealAfterTrick);
        w.WriteEndObject();

        var s2 = r.Scoring;
        w.WriteStartObject("scoring");
        w.WriteNumber("contraMultiplier", s2.ContraMultiplier);
        w.WriteNumber("recontraMultiplier", s2.RecontraMultiplier);
        w.WriteBoolean("winnerTakesAllUnderMultiplier", s2.WinnerTakesAllUnderMultiplier);
        w.WriteNumber("allTricksBonus", s2.AllTricksBonus);
        w.WriteNumber("gamePointDivisor", s2.GamePointDivisor);
        w.WriteString("rounding", s2.Rounding.ToString());
        w.WriteBoolean("roundingTieFavorsFewerPoints", s2.RoundingTieFavorsFewerPoints);
        w.WriteEndObject();

        w.WriteStartObject("match");
        w.WriteNumber("targetScore", r.Match.TargetScore);
        w.WriteBoolean("allTricksCannotEndMatch", r.Match.AllTricksCannotEndMatch);
        w.WriteEndObject();

        w.WriteEndObject();
    }

    public static RuleSet Read(JsonElement e)
    {
        var deal = e.GetProperty("deal");
        var bidding = e.GetProperty("bidding");
        var contracts = e.GetProperty("contracts");
        var play = e.GetProperty("play");
        var ann = e.GetProperty("announcements");
        var scoring = e.GetProperty("scoring");
        var match = e.GetProperty("match");

        var suit = contracts.GetProperty("suit");
        var noTrump = contracts.GetProperty("noTrump");
        var allTrump = contracts.GetProperty("allTrump");

        return new RuleSet(
            Name: e.GetProperty("name").GetString()!,
            Version: e.GetProperty("version").GetInt32(),
            Deal: new DealRules(ReadInts(deal, "pattern"), deal.GetProperty("biddingAfterPacket").GetInt32()),
            Bidding: new BiddingRules(
                bidding.GetProperty("ranking").EnumerateArray()
                    .Select(x => JsonNames.ParseKind(x.GetString()!)).ToImmutableArray(),
                bidding.GetProperty("contra").GetBoolean(),
                bidding.GetProperty("recontra").GetBoolean(),
                bidding.GetProperty("closeAfterConsecutivePasses").GetInt32(),
                bidding.GetProperty("closeOnRecontra").GetBoolean(),
                Enum.Parse<AllPassPolicy>(bidding.GetProperty("allPass").GetString()!)),
            Contracts: new ContractRules(
                new SuitContractRules(
                    ReadTable(suit.GetProperty("trump")),
                    ReadTable(suit.GetProperty("plain")),
                    suit.GetProperty("lastTrickBonus").GetInt32()),
                new TableContractRules(
                    ReadTable(noTrump.GetProperty("table")),
                    noTrump.GetProperty("lastTrickBonus").GetInt32()),
                new TableContractRules(
                    ReadTable(allTrump.GetProperty("table")),
                    allTrump.GetProperty("lastTrickBonus").GetInt32())),
            Play: new PlayRules(
                play.GetProperty("mustFollowSuit").GetBoolean(),
                play.GetProperty("cutOnlyWhenOpponentWinning").GetBoolean(),
                play.GetProperty("mustOvertrump").GetBoolean(),
                play.GetProperty("mustOvertakeInAllTrump").GetBoolean(),
                play.GetProperty("anyCardWhenCannotOvertrump").GetBoolean()),
            Announcements: new AnnouncementRules(
                ann.GetProperty("allowedInSuit").GetBoolean(),
                ann.GetProperty("allowedInNoTrump").GetBoolean(),
                ann.GetProperty("allowedInAllTrump").GetBoolean(),
                ann.GetProperty("sequences").EnumerateArray()
                    .Select(x => new SequenceValue(x.GetProperty("minLength").GetInt32(), x.GetProperty("points").GetInt32()))
                    .ToImmutableArray(),
                ReadInts(ann, "carrePoints"),
                ReadRanks(ann, "carreOrder"),
                ann.GetProperty("belotePoints").GetInt32(),
                ann.GetProperty("beloteInSuit").GetBoolean(),
                ann.GetProperty("beloteInNoTrump").GetBoolean(),
                ann.GetProperty("beloteInAllTrump").GetBoolean(),
                Enum.Parse<AnnouncementComparison>(ann.GetProperty("comparison").GetString()!),
                ann.GetProperty("revealAfterTrick").GetInt32()),
            Scoring: new ScoringRules(
                scoring.GetProperty("contraMultiplier").GetInt32(),
                scoring.GetProperty("recontraMultiplier").GetInt32(),
                scoring.GetProperty("winnerTakesAllUnderMultiplier").GetBoolean(),
                scoring.GetProperty("allTricksBonus").GetInt32(),
                scoring.GetProperty("gamePointDivisor").GetInt32(),
                Enum.Parse<RoundingMode>(scoring.GetProperty("rounding").GetString()!),
                scoring.GetProperty("roundingTieFavorsFewerPoints").GetBoolean()),
            Match: new MatchRules(
                match.GetProperty("targetScore").GetInt32(),
                match.GetProperty("allTricksCannotEndMatch").GetBoolean()));
    }

    private static void WriteInts(Utf8JsonWriter w, string name, ImmutableArray<int> values)
    {
        w.WriteStartArray(name);
        foreach (int v in values)
        {
            w.WriteNumberValue(v);
        }

        w.WriteEndArray();
    }

    private static void WriteRanks(Utf8JsonWriter w, string name, ImmutableArray<Rank> ranks)
    {
        w.WriteStartArray(name);
        foreach (var rank in ranks)
        {
            w.WriteStringValue(Card.RankText(rank));
        }

        w.WriteEndArray();
    }

    private static void WriteTable(Utf8JsonWriter w, string name, CardTable table)
    {
        w.WriteStartObject(name);
        WriteRanks(w, "order", table.Order);
        WriteInts(w, "points", table.Points);
        w.WriteEndObject();
    }

    private static ImmutableArray<int> ReadInts(JsonElement e, string name) =>
        e.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray();

    private static ImmutableArray<Rank> ReadRanks(JsonElement e, string name) =>
        e.GetProperty(name).EnumerateArray().Select(x => JsonNames.ParseRank(x.GetString()!)).ToImmutableArray();

    private static CardTable ReadTable(JsonElement e) => new(ReadRanks(e, "order"), ReadInts(e, "points"));
}
