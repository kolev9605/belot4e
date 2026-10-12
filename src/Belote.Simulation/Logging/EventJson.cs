using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Belote.Core;

namespace Belote.Simulation;

/// <summary>Canonical JSON for game events (one compact object per event).</summary>
internal static class EventJson
{
    public static void WriteCards(Utf8JsonWriter w, string name, System.Collections.Generic.IEnumerable<Card> cards)
    {
        w.WriteStartArray(name);
        foreach (var card in cards)
        {
            w.WriteStringValue(card.ToString());
        }

        w.WriteEndArray();
    }

    public static void WriteSpec(Utf8JsonWriter w, string name, AnnouncementSpec spec)
    {
        w.WriteStartObject(name);
        w.WriteString("kind", spec.Kind == AnnouncementKind.Sequence ? "sequence" : "carre");
        if (spec.Kind == AnnouncementKind.Sequence)
        {
            w.WriteString("suit", JsonNames.Of(spec.Suit));
        }

        w.WriteString("rank", Card.RankText(spec.Rank));
        w.WriteNumber("length", spec.Length);
        w.WriteEndObject();
    }

    public static AnnouncementSpec ReadSpec(JsonElement e)
    {
        var rank = JsonNames.ParseRank(e.GetProperty("rank").GetString()!);
        int length = e.GetProperty("length").GetInt32();
        return e.GetProperty("kind").GetString() == "carre"
            ? AnnouncementSpec.Carre(rank)
            : AnnouncementSpec.Sequence(JsonNames.ParseSuit(e.GetProperty("suit").GetString()!), rank, length);
    }

    public static void WriteAction(Utf8JsonWriter w, string name, PlayerAction action)
    {
        w.WriteStartObject(name);
        switch (action)
        {
            case PassAction:
                w.WriteString("type", "pass");
                break;
            case BidAction b:
                w.WriteString("type", "bid");
                w.WriteString("bid", JsonNames.Of(b.Kind));
                break;
            case ContraAction:
                w.WriteString("type", "contra");
                break;
            case RecontraAction:
                w.WriteString("type", "recontra");
                break;
            case AnnounceAction a:
                w.WriteString("type", "announce");
                WriteSpec(w, "announcement", a.Spec);
                break;
            case PlayCardAction p:
                w.WriteString("type", "play");
                w.WriteString("card", p.Card.ToString());
                break;
            default:
                throw new ArgumentException("Unknown action type.", nameof(action));
        }

        w.WriteEndObject();
    }

    public static PlayerAction ReadAction(JsonElement e) => e.GetProperty("type").GetString() switch
    {
        "pass" => new PassAction(),
        "bid" => new BidAction(JsonNames.ParseKind(e.GetProperty("bid").GetString()!)),
        "contra" => new ContraAction(),
        "recontra" => new RecontraAction(),
        "announce" => new AnnounceAction(ReadSpec(e.GetProperty("announcement"))),
        "play" => new PlayCardAction(Card.Parse(e.GetProperty("card").GetString()!)),
        var other => throw new FormatException($"Unknown action '{other}'."),
    };

    private static void WriteIntArray(Utf8JsonWriter w, string name, ImmutableArray<int> values)
    {
        w.WriteStartArray(name);
        foreach (int v in values)
        {
            w.WriteNumberValue(v);
        }

        w.WriteEndArray();
    }

    private static void WriteNullableTeam(Utf8JsonWriter w, string name, Team? team)
    {
        if (team is { } t)
        {
            w.WriteString(name, JsonNames.Of(t));
        }
        else
        {
            w.WriteNull(name);
        }
    }

    private static Team? ReadNullableTeam(JsonElement e, string name)
    {
        var p = e.GetProperty(name);
        return p.ValueKind == JsonValueKind.Null ? null : JsonNames.ParseTeam(p.GetString()!);
    }

    private static ImmutableArray<int> ReadIntArray(JsonElement e, string name) =>
        e.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray();

    public static void WriteResult(Utf8JsonWriter w, string name, HandResult r)
    {
        w.WriteStartObject(name);
        w.WriteString("contract", JsonNames.Of(r.Kind));
        w.WriteString("declaringTeam", JsonNames.Of(r.DeclaringTeam));
        w.WriteNumber("multiplier", r.Multiplier);
        WriteIntArray(w, "cardPoints", r.CardPoints);
        w.WriteString("lastTrickTeam", JsonNames.Of(r.LastTrickTeam));
        w.WriteNumber("lastTrickBonus", r.LastTrickBonus);
        WriteIntArray(w, "announcementPoints", r.AnnouncementPoints);
        WriteIntArray(w, "belotePoints", r.BelotePoints);
        WriteNullableTeam(w, "allTricksTeam", r.AllTricksTeam);
        w.WriteNumber("allTricksBonus", r.AllTricksBonus);
        WriteIntArray(w, "rawTotals", r.RawTotals);
        w.WriteString("outcome", r.Outcome.ToString().ToLowerInvariant());
        w.WriteNumber("poolGamePoints", r.PoolGamePoints);
        WriteIntArray(w, "gamePoints", r.GamePoints);
        w.WriteNumber("heldOverBefore", r.HeldOverBefore);
        w.WriteNumber("awardedFromHeldOver", r.AwardedFromHeldOver);
        WriteNullableTeam(w, "heldOverAwardedTo", r.HeldOverAwardedTo);
        w.WriteNumber("newlyHeld", r.NewlyHeld);
        w.WriteNumber("heldOverAfter", r.HeldOverAfter);
        w.WriteEndObject();
    }

    public static HandResult ReadResult(JsonElement e) => new(
        JsonNames.ParseKind(e.GetProperty("contract").GetString()!),
        JsonNames.ParseTeam(e.GetProperty("declaringTeam").GetString()!),
        e.GetProperty("multiplier").GetInt32(),
        ReadIntArray(e, "cardPoints"),
        JsonNames.ParseTeam(e.GetProperty("lastTrickTeam").GetString()!),
        e.GetProperty("lastTrickBonus").GetInt32(),
        ReadIntArray(e, "announcementPoints"),
        ReadIntArray(e, "belotePoints"),
        ReadNullableTeam(e, "allTricksTeam"),
        e.GetProperty("allTricksBonus").GetInt32(),
        ReadIntArray(e, "rawTotals"),
        Enum.Parse<HandOutcome>(e.GetProperty("outcome").GetString()!, ignoreCase: true),
        e.GetProperty("poolGamePoints").GetInt32(),
        ReadIntArray(e, "gamePoints"),
        e.GetProperty("heldOverBefore").GetInt32(),
        e.GetProperty("awardedFromHeldOver").GetInt32(),
        ReadNullableTeam(e, "heldOverAwardedTo"),
        e.GetProperty("newlyHeld").GetInt32(),
        e.GetProperty("heldOverAfter").GetInt32());

    public static void Write(Utf8JsonWriter w, GameEvent e)
    {
        w.WriteStartObject();
        w.WriteNumber("seq", e.Sequence);
        w.WriteNumber("deal", e.Deal);
        w.WriteString("type", e.Type);
        switch (e)
        {
            case GameStarted g:
                w.WriteNumber("seed", g.Seed);
                w.WriteString("ruleSet", g.RuleSetName);
                w.WriteNumber("ruleSetVersion", g.RuleSetVersion);
                break;
            case HandDealt h:
                w.WriteString("dealer", JsonNames.Of(h.Dealer));
                w.WriteNumber("dealtBeforeBidding", h.DealtBeforeBidding);
                w.WriteStartObject("hands");
                foreach (var seat in Seats.All)
                {
                    WriteCards(w, JsonNames.Of(seat), h.Hands[(int)seat]);
                }

                w.WriteEndObject();
                break;
            case BidMade b:
                w.WriteString("seat", JsonNames.Of(b.Seat));
                w.WriteString("bid", JsonNames.Of(b.Kind));
                break;
            case PassMade p:
                w.WriteString("seat", JsonNames.Of(p.Seat));
                break;
            case ContraMade c:
                w.WriteString("seat", JsonNames.Of(c.Seat));
                break;
            case RecontraMade r:
                w.WriteString("seat", JsonNames.Of(r.Seat));
                break;
            case BiddingClosed bc:
                w.WriteString("contract", JsonNames.Of(bc.Kind));
                w.WriteString("declarer", JsonNames.Of(bc.Declarer));
                w.WriteNumber("multiplier", bc.Multiplier);
                break;
            case HandVoided hv:
                w.WriteString("nextDealer", JsonNames.Of(hv.NextDealer));
                break;
            case AnnouncementDeclared ad:
                w.WriteString("seat", JsonNames.Of(ad.Seat));
                WriteSpec(w, "announcement", ad.Spec);
                w.WriteNumber("points", ad.Points);
                break;
            case AnnouncementsRevealed ar:
                w.WriteStartArray("announcements");
                foreach (var d in ar.Announcements)
                {
                    w.WriteStartObject();
                    w.WriteString("seat", JsonNames.Of(d.Seat));
                    WriteSpec(w, "announcement", d.Spec);
                    w.WriteNumber("points", d.Points);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                break;
            case AnnouncementsCompared ac:
                WriteNullableTeam(w, "sequenceTeam", ac.SequenceTeam);
                WriteNullableTeam(w, "carreTeam", ac.CarreTeam);
                break;
            case CardPlayed cp:
                w.WriteString("seat", JsonNames.Of(cp.Seat));
                w.WriteString("card", cp.Card.ToString());
                w.WriteNumber("trick", cp.TrickNumber);
                break;
            case BeloteAwarded ba:
                w.WriteString("seat", JsonNames.Of(ba.Seat));
                w.WriteString("suit", JsonNames.Of(ba.Suit));
                w.WriteNumber("points", ba.Points);
                break;
            case TrickWon tw:
                w.WriteString("winner", JsonNames.Of(tw.Winner));
                w.WriteNumber("trick", tw.TrickNumber);
                w.WriteNumber("points", tw.Points);
                break;
            case HandScored hs:
                WriteResult(w, "result", hs.Result);
                break;
            case PointsHeldOver ph:
                w.WriteNumber("points", ph.Points);
                w.WriteNumber("total", ph.Total);
                break;
            case HeldOverAwarded ho:
                w.WriteString("team", JsonNames.Of(ho.Team));
                w.WriteNumber("points", ho.Points);
                break;
            case MatchScoreUpdated ms:
                w.WriteNumber("northSouth", ms.NorthSouth);
                w.WriteNumber("eastWest", ms.EastWest);
                break;
            case GameFinished gf:
                w.WriteString("winner", JsonNames.Of(gf.Winner));
                w.WriteNumber("northSouth", gf.NorthSouth);
                w.WriteNumber("eastWest", gf.EastWest);
                break;
            default:
                throw new ArgumentException($"Unknown event type {e.GetType().Name}.", nameof(e));
        }

        w.WriteEndObject();
    }

    public static GameEvent Read(JsonElement e)
    {
        int seq = e.GetProperty("seq").GetInt32();
        int deal = e.GetProperty("deal").GetInt32();
        string type = e.GetProperty("type").GetString()!;

        Seat S(string name) => JsonNames.ParseSeat(e.GetProperty(name).GetString()!);

        GameEvent ev = type switch
        {
            nameof(GameStarted) => new GameStarted(
                e.GetProperty("seed").GetUInt64(),
                e.GetProperty("ruleSet").GetString()!,
                e.GetProperty("ruleSetVersion").GetInt32()),
            nameof(HandDealt) => new HandDealt(
                S("dealer"),
                Seats.All
                    .Select(seat => e.GetProperty("hands").GetProperty(JsonNames.Of(seat)).EnumerateArray()
                        .Select(c => Card.Parse(c.GetString()!)).ToImmutableArray())
                    .ToImmutableArray(),
                e.GetProperty("dealtBeforeBidding").GetInt32()),
            nameof(BidMade) => new BidMade(S("seat"), JsonNames.ParseKind(e.GetProperty("bid").GetString()!)),
            nameof(PassMade) => new PassMade(S("seat")),
            nameof(ContraMade) => new ContraMade(S("seat")),
            nameof(RecontraMade) => new RecontraMade(S("seat")),
            nameof(BiddingClosed) => new BiddingClosed(
                JsonNames.ParseKind(e.GetProperty("contract").GetString()!),
                S("declarer"),
                e.GetProperty("multiplier").GetInt32()),
            nameof(HandVoided) => new HandVoided(S("nextDealer")),
            nameof(AnnouncementDeclared) => new AnnouncementDeclared(
                S("seat"),
                ReadSpec(e.GetProperty("announcement")),
                e.GetProperty("points").GetInt32()),
            nameof(AnnouncementsRevealed) => new AnnouncementsRevealed(
                e.GetProperty("announcements").EnumerateArray()
                    .Select(a => new DeclaredAnnouncement(
                        JsonNames.ParseSeat(a.GetProperty("seat").GetString()!),
                        ReadSpec(a.GetProperty("announcement")),
                        a.GetProperty("points").GetInt32()))
                    .ToImmutableArray()),
            nameof(AnnouncementsCompared) => new AnnouncementsCompared(
                ReadNullableTeam(e, "sequenceTeam"),
                ReadNullableTeam(e, "carreTeam")),
            nameof(CardPlayed) => new CardPlayed(
                S("seat"),
                Card.Parse(e.GetProperty("card").GetString()!),
                e.GetProperty("trick").GetInt32()),
            nameof(BeloteAwarded) => new BeloteAwarded(
                S("seat"),
                JsonNames.ParseSuit(e.GetProperty("suit").GetString()!),
                e.GetProperty("points").GetInt32()),
            nameof(TrickWon) => new TrickWon(
                S("winner"),
                e.GetProperty("trick").GetInt32(),
                e.GetProperty("points").GetInt32()),
            nameof(HandScored) => new HandScored(ReadResult(e.GetProperty("result"))),
            nameof(PointsHeldOver) => new PointsHeldOver(
                e.GetProperty("points").GetInt32(),
                e.GetProperty("total").GetInt32()),
            nameof(HeldOverAwarded) => new HeldOverAwarded(
                JsonNames.ParseTeam(e.GetProperty("team").GetString()!),
                e.GetProperty("points").GetInt32()),
            nameof(MatchScoreUpdated) => new MatchScoreUpdated(
                e.GetProperty("northSouth").GetInt32(),
                e.GetProperty("eastWest").GetInt32()),
            nameof(GameFinished) => new GameFinished(
                JsonNames.ParseTeam(e.GetProperty("winner").GetString()!),
                e.GetProperty("northSouth").GetInt32(),
                e.GetProperty("eastWest").GetInt32()),
            _ => throw new FormatException($"Unknown event type '{type}'."),
        };

        return ev with { Sequence = seq, Deal = deal };
    }
}
