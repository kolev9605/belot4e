using System.Collections.Immutable;
using System.Text.Json;
using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

public class GameLogTests
{
    [Fact]
    public async Task The_log_has_a_header_and_strictly_ordered_events_that_start_and_end_correctly()
    {
        var run = await Sim.RunGameAsync(11);
        var log = run.Log;

        Assert.Equal(11UL, log.Seed);
        Assert.Equal("RandomLegalBot", log.Seats[Seat.North]);
        Assert.Equal(Enumerable.Range(0, log.Events.Count), log.Events.Select(e => e.Sequence));
        Assert.IsType<GameStarted>(log.Events[0]);
        Assert.IsType<HandDealt>(log.Events[1]);
        Assert.IsType<GameFinished>(log.Events[^1]);

        var header = JsonDocument.Parse(log.ToJsonLines().Split('\n')[0]).RootElement;
        Assert.Equal("header", header.GetProperty("type").GetString());
        Assert.Equal(11UL, header.GetProperty("seed").GetUInt64());
        Assert.Equal("bulgarian", header.GetProperty("ruleSet").GetProperty("name").GetString());
    }

    [Fact]
    public async Task FR_024_The_log_lists_every_kind_of_event_in_play_order()
    {
        var types = new HashSet<string>();
        var wanted = new[]
                 {
                     nameof(GameStarted), nameof(HandDealt), nameof(BidMade), nameof(PassMade), nameof(BiddingClosed),
                     nameof(CardPlayed), nameof(TrickWon), nameof(HandScored), nameof(MatchScoreUpdated),
                     nameof(GameFinished), nameof(AnnouncementDeclared), nameof(AnnouncementsRevealed),
                     nameof(AnnouncementsCompared), nameof(BeloteAwarded), nameof(ContraMade), nameof(RecontraMade),
                     nameof(HandVoided),
                 };

        // All-pass hands are rare with uniformly random bots, so keep going until every type is seen.
        for (ulong seed = 1; seed <= 2000 && !wanted.All(types.Contains); seed++)
        {
            foreach (var e in (await Sim.RunGameAsync(seed)).Log.Events)
            {
                types.Add(e.Type);
            }
        }

        foreach (var expected in wanted)
        {
            Assert.Contains(expected, types);
        }
    }

    [Fact]
    public async Task JSON_lines_round_trip_losslessly_and_the_hash_is_stable()
    {
        var run = await Sim.RunGameAsync(23);
        string text = run.Log.ToJsonLines();
        var parsed = GameLog.FromJsonLines(text);

        Assert.Equal(text, parsed.ToJsonLines());
        Assert.Equal(run.Log.Hash, parsed.Hash);
        Assert.Equal(run.Log.Events.Count, parsed.Events.Count);
        Assert.Equal(run.Log.Seed, parsed.Seed);
        Assert.True(GameAuditor.AuditJsonLines(text).Ok);
    }

    [Fact]
    public async Task The_log_contains_only_language_neutral_codes()
    {
        string text = (await Sim.RunGameAsync(5)).Log.ToJsonLines();
        // Every event line is a JSON object whose "type" is an identifier, and nothing is prose.
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var doc = JsonDocument.Parse(line);
            string type = doc.RootElement.GetProperty("type").GetString()!;
            Assert.Matches("^[A-Za-z]+$", type);
        }

        Assert.DoesNotContain(" the ", text);
    }

    [Fact]
    public async Task Rejections_are_kept_beside_the_log_but_are_not_part_of_the_hash()
    {
        var run = await Sim.RunGameAsync(31, SeatPresets.ScriptedIllegal);
        Assert.NotEmpty(run.Log.Rejections);

        string withRejections = run.Log.ToJsonLinesWithRejections();
        Assert.Contains("ActionRejected", withRejections);
        Assert.DoesNotContain("ActionRejected", run.Log.ToJsonLines());

        var parsed = GameLog.FromJsonLines(withRejections);
        Assert.Equal(run.Log.Hash, parsed.Hash);
        Assert.Equal(run.Log.Rejections.Count, parsed.Rejections.Count);
    }
}

public class RuleSetJsonTests
{
    [Fact]
    public void The_bulgarian_rule_set_round_trips_through_json()
    {
        string json = RuleSetJson.ToJson(RuleSets.Bulgarian);
        var parsed = RuleSetJson.Parse(json);

        Assert.Equal(json, RuleSetJson.ToJson(parsed));
        Assert.Empty(RuleSetValidator.Validate(parsed));
        Assert.Equal(RuleSets.Bulgarian.PointsInPlay(ContractKind.AllTrump), parsed.PointsInPlay(ContractKind.AllTrump));
        Assert.Equal(RuleSets.Bulgarian.Bidding.Ranking.ToArray(), parsed.Bidding.Ranking.ToArray());
    }

    [Fact]
    public async Task A_rule_set_read_back_from_json_plays_exactly_the_same_game()
    {
        var parsed = RuleSetJson.Parse(RuleSetJson.ToJson(RuleSets.Bulgarian, indented: true));
        var direct = await Sim.RunGameAsync(77);
        var viaJson = await Sim.RunGameAsync(77, rules: parsed);
        Assert.Equal(direct.Log.Hash, viaJson.Log.Hash);
    }

    [Fact]
    public void Malformed_rule_set_json_is_rejected()
    {
        Assert.ThrowsAny<Exception>(() => RuleSetJson.Parse("{ \"name\": \"x\" }"));
        Assert.ThrowsAny<Exception>(() => RuleSetJson.Parse("not json"));
    }
}

public class GameAuditorTests
{
    private static async Task<GameLog> SampleLog() => (await Sim.RunGameAsync(19)).Log;

    private static GameLog Tamper(GameLog log, Func<IReadOnlyList<GameEvent>, IReadOnlyList<GameEvent>> change) =>
        new(log.Seed, log.RuleSet, log.Seats, change(log.Events));

    [Fact]
    public async Task A_valid_log_passes_every_check()
    {
        var report = GameAuditor.Audit(await SampleLog());
        Assert.True(report.Ok, string.Join("; ", report.Issues));
    }

    [Fact]
    public async Task An_illegal_card_in_the_log_is_reported_at_that_event()
    {
        var log = await SampleLog();
        int index = log.Events.ToList().FindIndex(e => e is CardPlayed);
        var original = (CardPlayed)log.Events[index];
        var elsewhere = Deck.Standard32.First(c => c != original.Card);

        var tampered = Tamper(log, events =>
        {
            var copy = events.ToList();
            copy[index] = original with { Card = elsewhere };
            return copy;
        });

        var report = GameAuditor.Audit(tampered);
        Assert.False(report.Ok);
        Assert.Equal(original.Sequence, report.First!.Sequence);
    }

    [Fact]
    public async Task A_wrong_trick_winner_is_reported()
    {
        var log = await SampleLog();
        int index = log.Events.ToList().FindIndex(e => e is TrickWon);
        var original = (TrickWon)log.Events[index];

        var report = GameAuditor.Audit(Tamper(log, events =>
        {
            var copy = events.ToList();
            copy[index] = original with { Winner = original.Winner.Next() };
            return copy;
        }));

        Assert.False(report.Ok);
        Assert.Equal(original.Sequence, report.First!.Sequence);
    }

    [Fact]
    public async Task A_wrong_hand_score_is_reported()
    {
        var log = await SampleLog();
        int index = log.Events.ToList().FindIndex(e => e is HandScored);
        var original = (HandScored)log.Events[index];
        var bad = original.Result with { RawTotals = ImmutableArray.Create(original.Result.RawTotals[0] + 10, original.Result.RawTotals[1]) };

        var report = GameAuditor.Audit(Tamper(log, events =>
        {
            var copy = events.ToList();
            copy[index] = original with { Result = bad };
            return copy;
        }));

        Assert.False(report.Ok);
        Assert.Equal(original.Sequence, report.First!.Sequence);
    }

    [Fact]
    public async Task A_duplicated_card_in_a_deal_is_reported()
    {
        var log = await SampleLog();
        var original = (HandDealt)log.Events[1];
        var hands = original.Hands.ToArray();
        hands[1] = hands[1].SetItem(0, hands[0][0]);

        var report = GameAuditor.Audit(Tamper(log, events =>
        {
            var copy = events.ToList();
            copy[1] = original with { Hands = ImmutableArray.Create(hands) };
            return copy;
        }));

        Assert.False(report.Ok);
        Assert.Equal(original.Sequence, report.First!.Sequence);
    }

    [Fact]
    public async Task A_log_that_does_not_end_with_a_winner_is_reported()
    {
        var log = await SampleLog();
        var report = GameAuditor.Audit(Tamper(log, events => events.Take(events.Count - 1).ToList()));
        Assert.False(report.Ok);
    }

    [Fact]
    public async Task Events_added_after_the_game_finished_are_reported()
    {
        var log = await SampleLog();
        var last = log.Events[^1];
        var report = GameAuditor.Audit(Tamper(log, events =>
        {
            var copy = events.ToList();
            copy.Add(new PassMade(Seat.North) with { Sequence = last.Sequence + 1, Deal = last.Deal });
            return copy;
        }));
        Assert.False(report.Ok);
    }

    [Fact]
    public async Task The_auditor_follows_the_rule_set_in_the_log_header()
    {
        // The same log judged under a different rule set must not silently pass.
        var log = await SampleLog();
        var other = RuleSets.Bulgarian with { Match = RuleSets.Bulgarian.Match with { TargetScore = 31 } };
        var wrongHeader = new GameLog(log.Seed, other, log.Seats, log.Events);
        Assert.False(GameAuditor.Audit(wrongHeader).Ok);
    }
}
