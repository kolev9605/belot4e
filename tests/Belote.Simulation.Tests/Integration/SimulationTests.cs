using System.Collections.Immutable;
using Belote.Bots;
using Belote.Core;
using Belote.Simulation;
using Belote.Simulation.Cli;
using Xunit;

namespace Belote.Simulation.Tests;

public class SimulationRunnerTests
{
    [Fact]
    public async Task N_games_produce_N_ordered_outcomes_with_seed_winner_score_and_hash()
    {
        var report = await Sim.RunBatchAsync(25, baseSeed: 9);

        Assert.Equal(25, report.Games.Count);
        Assert.Equal(25, report.Passed);
        Assert.Equal(0, report.Failed);
        Assert.Empty(report.ByFailure);
        for (int i = 0; i < 25; i++)
        {
            var game = report.Games[i];
            Assert.Equal(i, game.Index);
            Assert.Equal(Seeds.ForGame(9, i), game.Seed);
            Assert.True(game.FinishedCorrectly);
            Assert.NotNull(game.Winner);
            Assert.False(string.IsNullOrEmpty(game.LogHash));
            Assert.Null(game.Log);
        }
    }

    [Fact]
    public async Task SC_006_Any_single_game_of_a_batch_can_be_rerun_in_isolation_from_its_seed()
    {
        var report = await Sim.RunBatchAsync(10, baseSeed: 3);
        var target = report.Games[6];

        var run = await Sim.RunGameAsync(target.Seed);
        Assert.Equal(target.LogHash, run.Log.Hash);

        var again = await SimulationRunner.RunOneAsync(
            new RunOptions { BaseSeed = 3, GameCount = 10 }, 6);
        Assert.Equal(target.LogHash, again.LogHash);
    }

    [Fact]
    public async Task Failed_games_are_reported_with_kind_detail_seed_and_retained_log()
    {
        var report = await SimulationRunner.RunAsync(new RunOptions
        {
            BaseSeed = 1,
            GameCount = 6,
            Parallelism = 2,
            KeepLogs = KeepLogs.FailuresOnly,
            SeatFactory = (index, seed) =>
            {
                var seats = SeatPresets.Create(SeatPresets.AllBot, seed).ToDictionary(kv => kv.Key, kv => kv.Value);
                if (index == 2)
                {
                    seats[Seat.East] = new SeatController("Broken", new ThrowingPlayer());
                }

                return seats;
            },
        });

        Assert.Equal(5, report.Passed);
        Assert.Equal(1, report.Failed);
        var failed = Assert.Single(report.Games, g => !g.FinishedCorrectly);
        Assert.Equal(2, failed.Index);
        Assert.Equal(FailureKind.ControllerException, failed.Failure);
        Assert.Contains("East", failed.FailureDetail);
        Assert.NotNull(failed.Log);
        Assert.Equal(1, report.ByFailure[FailureKind.ControllerException]);
        Assert.All(report.Games.Where(g => g.FinishedCorrectly), g => Assert.Null(g.Log));
    }

    [Fact]
    public async Task Keep_logs_all_retains_every_log()
    {
        var report = await Sim.RunBatchAsync(5, keepLogs: KeepLogs.All);
        Assert.All(report.Games, g => Assert.NotNull(g.Log));
    }

    [Fact]
    public async Task An_invalid_rule_set_or_preset_is_refused_before_any_game_runs()
    {
        var broken = RuleSets.Bulgarian with { Match = RuleSets.Bulgarian.Match with { TargetScore = 0 } };
        await Assert.ThrowsAsync<ArgumentException>(() => Sim.RunBatchAsync(1, rules: broken));
        await Assert.ThrowsAsync<ArgumentException>(() => Sim.RunBatchAsync(1, preset: "nope"));
    }

    [Fact]
    public async Task The_report_serializes_with_summary_and_per_game_rows()
    {
        var report = await Sim.RunBatchAsync(3);
        string json = ReportJson.ToJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(3, root.GetProperty("summary").GetProperty("games").GetInt32());
        Assert.Equal(3, root.GetProperty("summary").GetProperty("finishedCorrectly").GetInt32());
        Assert.Equal(3, root.GetProperty("games").GetArrayLength());
        Assert.True(root.GetProperty("games")[0].GetProperty("finished").GetBoolean());
    }
}

internal sealed class ThrowingPlayer : IPlayer
{
    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}

internal sealed class NeverRespondingPlayer : IPlayer
{
    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken) =>
        new(new TaskCompletionSource<PlayerAction>().Task);
}

internal sealed class AlwaysIllegalPlayer : IPlayer
{
    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken) =>
        new(view.Phase == Phase.Bidding
            ? new ContraAction()
            : new PlayCardAction(new Card(Suit.Clubs, Rank.Seven)));
}

public class MixedSeatGameTests
{
    [Theory]
    [InlineData("mixed-1")]
    [InlineData("mixed-2")]
    [InlineData("mixed-3")]
    [InlineData("mixed-4")]
    [InlineData("scripted-illegal")]
    public async Task US7_Games_with_scripted_seats_finish_and_pass_the_audit(string preset)
    {
        var report = await Sim.RunBatchAsync(200, preset, baseSeed: 5);
        Assert.Equal(200, report.Passed);
    }

    [Theory]
    [InlineData("mixed-1")]
    [InlineData("mixed-2")]
    [InlineData("mixed-3")]
    [InlineData("mixed-4")]
    [InlineData("scripted-illegal")]
    public async Task The_engine_is_seat_blind_the_same_choices_give_the_same_game_whoever_makes_them(string preset)
    {
        var bots = await Sim.RunBatchAsync(40, SeatPresets.AllBot, baseSeed: 5, keepLogs: KeepLogs.All);
        var mixed = await Sim.RunBatchAsync(40, preset, baseSeed: 5, keepLogs: KeepLogs.All);

        for (int i = 0; i < 40; i++)
        {
            Assert.Equal(Sim.EventsHash(bots.Games[i].Log!), Sim.EventsHash(mixed.Games[i].Log!));
            Assert.Equal(bots.Games[i].Winner, mixed.Games[i].Winner);
        }
    }

    [Fact]
    public async Task The_header_records_which_seats_were_not_bots()
    {
        var run = await Sim.RunGameAsync(3, "mixed-2");
        Assert.Equal("ScriptedHuman", run.Log.Seats[Seat.North]);
        Assert.Equal("ScriptedHuman", run.Log.Seats[Seat.East]);
        Assert.Equal("RandomLegalBot", run.Log.Seats[Seat.South]);
    }

    [Fact]
    public async Task A_scripted_controller_sees_only_its_own_view()
    {
        // Wrap a scripted seat and capture every view it was handed: none may expose other hands.
        var captured = new List<SeatView>();
        var seed = 99UL;
        var bot = new RandomLegalBot(Seeds.ForSeat(seed, Seat.South));
        var spy = new ScriptedPlayer(view =>
        {
            captured.Add(view);
            return bot.DecideAsync(view, default).Result;
        });

        var seats = SeatPresets.Create(SeatPresets.AllBot, seed).ToDictionary(kv => kv.Key, kv => kv.Value);
        seats[Seat.South] = new SeatController("Spy", spy);
        var run = await GameRunner.RunAsync(RuleSets.Bulgarian, seed, seats);

        Assert.True(run.Finished);
        Assert.NotEmpty(captured);
        Assert.All(captured, v => Assert.Equal(Seat.South, v.Seat));
        // Every card the spy ever held was dealt to South in that deal.
        var dealt = run.Log.Events.OfType<HandDealt>().ToDictionary(e => e.Deal);
        Assert.All(captured, v => Assert.All(v.OwnHand, c => Assert.Contains(c, dealt[v.DealNumber].Hands[(int)Seat.South])));
    }
}

public class IllegalMoveInjectionTests
{
    [Fact]
    public async Task Illegal_attempts_are_rejected_recorded_and_never_change_the_game()
    {
        var run = await Sim.RunGameAsync(14, SeatPresets.ScriptedIllegal);
        var reference = await Sim.RunGameAsync(14, SeatPresets.AllBot);

        Assert.True(run.Finished, run.FailureDetail);
        Assert.NotEmpty(run.Log.Rejections);
        Assert.Equal(Sim.EventsHash(reference.Log), Sim.EventsHash(run.Log));
        Assert.All(run.Log.Rejections, r => Assert.Contains(r.Seat, new[] { Seat.North, Seat.East }));
    }

    [Fact]
    public async Task A_controller_that_keeps_submitting_illegal_actions_fails_only_its_game()
    {
        var seats = Sim.Bots(1).ToDictionary(kv => kv.Key, kv => kv.Value);
        seats[Seat.West] = new SeatController("Cheater", new AlwaysIllegalPlayer());

        var run = await GameRunner.RunAsync(RuleSets.Bulgarian, 1, seats, new GameRunOptions { MaxIllegalAttempts = 3 });

        Assert.False(run.Finished);
        Assert.Equal(FailureKind.TooManyIllegalAttempts, run.Failure);
        Assert.Equal(3, run.Log.Rejections.Count(r => r.Seat == Seat.West));
        Assert.Contains("West", run.FailureDetail);
    }
}

public class ControllerFailureTests
{
    [Fact]
    public async Task A_controller_that_throws_fails_the_game_with_controller_exception()
    {
        var seats = Sim.Bots(2).ToDictionary(kv => kv.Key, kv => kv.Value);
        seats[Seat.North] = new SeatController("Thrower", new ThrowingPlayer());
        var run = await GameRunner.RunAsync(RuleSets.Bulgarian, 2, seats);

        Assert.False(run.Finished);
        Assert.Equal(FailureKind.ControllerException, run.Failure);
        Assert.Contains("boom", run.FailureDetail);
    }

    [Fact]
    public async Task A_controller_that_never_responds_times_out_instead_of_hanging()
    {
        var seats = Sim.Bots(2).ToDictionary(kv => kv.Key, kv => kv.Value);
        seats[Seat.South] = new SeatController("Silent", new NeverRespondingPlayer());

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var run = await GameRunner.RunAsync(
            RuleSets.Bulgarian,
            2,
            seats,
            new GameRunOptions { DecisionTimeout = TimeSpan.FromMilliseconds(150) });
        watch.Stop();

        Assert.False(run.Finished);
        Assert.Equal(FailureKind.ControllerTimeout, run.Failure);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task One_failing_controller_does_not_stop_the_rest_of_a_batch()
    {
        var report = await SimulationRunner.RunAsync(new RunOptions
        {
            GameCount = 6,
            Parallelism = 3,
            Game = new GameRunOptions { DecisionTimeout = TimeSpan.FromMilliseconds(100) },
            SeatFactory = (index, seed) =>
            {
                var seats = SeatPresets.Create(SeatPresets.AllBot, seed).ToDictionary(kv => kv.Key, kv => kv.Value);
                if (index % 3 == 0)
                {
                    seats[Seat.West] = new SeatController("Silent", new NeverRespondingPlayer());
                }

                return seats;
            },
        });

        Assert.Equal(4, report.Passed);
        Assert.Equal(2, report.ByFailure[FailureKind.ControllerTimeout]);
    }

    [Fact]
    public async Task A_game_that_cannot_end_fails_with_exceeded_hand_limit()
    {
        var rules = RuleSets.Bulgarian with { Match = RuleSets.Bulgarian.Match with { TargetScore = 1_000_000 } };
        var run = await GameRunner.RunAsync(
            rules, 1, Sim.Bots(1), new GameRunOptions { MaxDeals = 25 });
        Assert.False(run.Finished);
        Assert.Equal(FailureKind.ExceededHandLimit, run.Failure);
    }
}

public class VariantRuleSetTests
{
    // Principle IV: variants are configuration. Each variant below is built only with `with` on the
    // rule-set object; no engine code changes, and every game still passes the independent audit.
    private static readonly RuleSet Bulgarian = RuleSets.Bulgarian;

    [Fact]
    public async Task A_lower_target_score_ends_games_sooner()
    {
        var variant = Bulgarian with { Match = Bulgarian.Match with { TargetScore = 31 } };
        var normal = await Sim.RunBatchAsync(60, keepLogs: KeepLogs.None);
        var shorter = await Sim.RunBatchAsync(60, keepLogs: KeepLogs.None, rules: variant);

        Assert.Equal(60, shorter.Passed);
        Assert.True(shorter.Games.Average(g => g.HandsScored) < normal.Games.Average(g => g.HandsScored));
        Assert.All(shorter.Games, g => Assert.True(Math.Max(g.NorthSouth, g.EastWest) >= 31));
    }

    [Fact]
    public async Task Disabling_contra_removes_all_doubling_from_games()
    {
        var variant = Bulgarian with { Bidding = Bulgarian.Bidding with { Contra = false, Recontra = false } };
        var report = await Sim.RunBatchAsync(60, keepLogs: KeepLogs.All, rules: variant);

        Assert.Equal(60, report.Passed);
        var events = report.Games.SelectMany(g => g.Log!.Events).ToList();
        Assert.Empty(events.OfType<ContraMade>());
        Assert.Empty(events.OfType<RecontraMade>());
        Assert.All(events.OfType<HandScored>(), e => Assert.Equal(1, e.Result.Multiplier));
    }

    [Fact]
    public async Task Disabling_announcements_removes_them_from_games()
    {
        var off = Bulgarian.Announcements with { AllowedInSuit = false, AllowedInAllTrump = false };
        var variant = Bulgarian with { Announcements = off };
        var report = await Sim.RunBatchAsync(60, keepLogs: KeepLogs.All, rules: variant);

        Assert.Equal(60, report.Passed);
        Assert.Empty(report.Games.SelectMany(g => g.Log!.Events).OfType<AnnouncementDeclared>());
    }

    [Fact]
    public void A_different_bid_ranking_changes_which_bids_are_legal()
    {
        var reversed = Bulgarian with
        {
            Bidding = Bulgarian.Bidding with { Ranking = Bulgarian.Bidding.Ranking.Reverse().ToImmutableArray() },
        };

        var state = GameEngine.Start(reversed, 1).State;
        state = Assert.IsType<Accepted>(GameEngine.Apply(state, state.ToAct, new BidAction(ContractKind.Clubs))).State;
        // Clubs is now the top bid, so nothing can outbid it...
        var legal = LegalActions.For(Projection.For(state, state.ToAct));
        Assert.DoesNotContain(legal, a => a is BidAction);
        // ...while under the standard ranking AllTrump would still be available.
        var standard = GameEngine.Start(Bulgarian, 1).State;
        standard = Assert.IsType<Accepted>(GameEngine.Apply(standard, standard.ToAct, new BidAction(ContractKind.Clubs))).State;
        Assert.Contains(new BidAction(ContractKind.AllTrump), LegalActions.For(Projection.For(standard, standard.ToAct)));
    }

    [Fact]
    public async Task A_different_deal_pattern_changes_when_bidding_happens()
    {
        var variant = Bulgarian with { Deal = new DealRules(ImmutableArray.Create(4, 4), BiddingAfterPacket: 1) };
        var state = GameEngine.Start(variant, 3).State;
        Assert.Equal(4, Projection.For(state, state.ToAct).OwnHand.Length);

        var report = await Sim.RunBatchAsync(40, rules: variant, keepLogs: KeepLogs.None);
        Assert.Equal(40, report.Passed);
    }

    [Fact]
    public void Turning_off_the_overtrump_duty_changes_the_legal_cards()
    {
        var variant = Bulgarian with { Play = Bulgarian.Play with { MustOvertrump = false } };
        var hand = ImmutableArray.Create(Card.Parse("JH"), Card.Parse("7H"), Card.Parse("8D"));
        var trick = ImmutableArray.Create(
            new PlayedCard(Seat.West, Card.Parse("7S")),
            new PlayedCard(Seat.North, Card.Parse("8S")),
            new PlayedCard(Seat.East, Card.Parse("9H")));

        var standard = LegalCards.For(Bulgarian, ContractKind.Hearts, Seat.South, hand, trick);
        var relaxed = LegalCards.For(variant, ContractKind.Hearts, Seat.South, hand, trick);

        Assert.Equal(new[] { Card.Parse("JH") }, standard.ToArray());
        Assert.Equal(2, relaxed.Length);
        Assert.Contains(Card.Parse("7H"), relaxed);
    }

    [Fact]
    public async Task Different_card_point_tables_flow_through_scoring_and_the_audit()
    {
        // A variant where aces are worth 12 instead of 11 in trump/plain suits changes points in play.
        var trumpPoints = Bulgarian.Contracts.Suit.Trump.Points.SetItem((int)Rank.Ace, 12);
        var plainPoints = Bulgarian.Contracts.Suit.Plain.Points.SetItem((int)Rank.Ace, 12);
        var variant = Bulgarian with
        {
            Contracts = Bulgarian.Contracts with
            {
                Suit = Bulgarian.Contracts.Suit with
                {
                    Trump = Bulgarian.Contracts.Suit.Trump with { Points = trumpPoints },
                    Plain = Bulgarian.Contracts.Suit.Plain with { Points = plainPoints },
                },
            },
        };

        Assert.Equal(166, variant.PointsInPlay(ContractKind.Hearts));
        var report = await Sim.RunBatchAsync(40, rules: variant, keepLogs: KeepLogs.All);
        Assert.Equal(40, report.Passed);
        var suitHands = report.Games.SelectMany(g => g.Log!.Events).OfType<HandScored>()
            .Where(h => h.Result.Kind.Family() == ContractFamily.Suit).ToList();
        Assert.NotEmpty(suitHands);
        Assert.All(suitHands, h => Assert.Equal(166, h.Result.CardPoints.Sum() + h.Result.LastTrickBonus));
    }

    [Fact]
    public async Task A_different_all_tricks_bonus_and_rounding_tie_rule_are_honoured()
    {
        var variant = Bulgarian with
        {
            Scoring = Bulgarian.Scoring with { AllTricksBonus = 0, RoundingTieFavorsFewerPoints = false },
        };
        var report = await Sim.RunBatchAsync(80, rules: variant, keepLogs: KeepLogs.All);
        Assert.Equal(80, report.Passed);
        Assert.All(
            report.Games.SelectMany(g => g.Log!.Events).OfType<HandScored>(),
            h => Assert.Equal(0, h.Result.AllTricksBonus));
    }
}

public class CliTests
{
    private static async Task<(int Exit, string Out, string Err)> Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = await CliApp.RunAsync(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "belote-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Run_writes_a_report_and_exits_zero_when_every_game_is_correct()
    {
        string dir = TempDir();
        var (exit, output, _) = await Run("run", "--games", "30", "--seed", "4", "--out", dir);

        Assert.Equal(CliApp.Ok, exit);
        Assert.Contains("finished correctly: 30", output);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "report.json")));
        Assert.Equal(30, doc.RootElement.GetProperty("summary").GetProperty("finishedCorrectly").GetInt32());
    }

    [Fact]
    public async Task Keep_logs_all_writes_one_jsonl_file_per_game()
    {
        string dir = TempDir();
        var (exit, _, _) = await Run("run", "--games", "3", "--keep-logs", "all", "--out", dir);
        Assert.Equal(CliApp.Ok, exit);
        Assert.Equal(3, Directory.GetFiles(dir, "game-*.jsonl").Length);
    }

    [Fact]
    public async Task A_failing_game_makes_the_exit_code_one_and_keeps_its_log()
    {
        string dir = TempDir();
        var impossible = RuleSets.Bulgarian with { Match = RuleSets.Bulgarian.Match with { TargetScore = 1_000_000 } };
        string rulesPath = Path.Combine(dir, "impossible.json");
        File.WriteAllText(rulesPath, RuleSetJson.ToJson(impossible, indented: true));

        var (exit, output, error) = await Run(
            "run", "--games", "1", "--rules", rulesPath, "--out", dir, "--parallelism", "1");

        Assert.Equal(CliApp.GameFailed, exit);
        Assert.Contains("ExceededHandLimit", output + error);
        Assert.NotEmpty(Directory.GetFiles(dir, "game-*.jsonl"));
    }

    [Theory]
    [InlineData("run")]
    [InlineData("run", "--games", "0")]
    [InlineData("run", "--games", "5", "--seats", "nope")]
    [InlineData("run", "--games", "5", "--keep-logs", "sometimes")]
    [InlineData("frobnicate")]
    [InlineData("replay")]
    [InlineData("audit")]
    public async Task Invalid_arguments_exit_with_code_two(params string[] args)
    {
        var (exit, _, error) = await Run(args);
        Assert.Equal(CliApp.BadUsage, exit);
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task An_invalid_rule_set_file_exits_with_code_two()
    {
        string dir = TempDir();
        string path = Path.Combine(dir, "bad.json");
        var bad = RuleSets.Bulgarian with { Match = RuleSets.Bulgarian.Match with { TargetScore = 0 } };
        File.WriteAllText(path, RuleSetJson.ToJson(bad));

        var (exit, _, error) = await Run("run", "--games", "1", "--rules", path, "--out", dir);
        Assert.Equal(CliApp.BadUsage, exit);
        Assert.Contains("targetScore", error);
    }

    [Fact]
    public async Task Replay_of_a_seed_produces_identical_files_and_audit_accepts_them()
    {
        string dir = TempDir();
        string a = Path.Combine(dir, "a.jsonl");
        string b = Path.Combine(dir, "b.jsonl");

        Assert.Equal(CliApp.Ok, (await Run("replay", "--seed", "1234", "--out", a)).Exit);
        Assert.Equal(CliApp.Ok, (await Run("replay", "--seed", "1234", "--out", b)).Exit);
        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));

        var (exit, output, _) = await Run("audit", "--log", a);
        Assert.Equal(CliApp.Ok, exit);
        Assert.Contains("audit passed", output);
    }

    [Fact]
    public async Task Audit_names_the_first_offending_event_of_a_tampered_log()
    {
        string dir = TempDir();
        string path = Path.Combine(dir, "game.jsonl");
        await Run("replay", "--seed", "55", "--out", path);

        var lines = File.ReadAllLines(path).ToList();
        int index = lines.FindIndex(l => l.Contains("\"type\":\"TrickWon\""));
        lines[index] = System.Text.RegularExpressions.Regex.Replace(
            lines[index],
            "\"winner\":\"(north|east|south|west)\"",
            m => m.Groups[1].Value switch
            {
                "north" => "\"winner\":\"east\"",
                "east" => "\"winner\":\"south\"",
                "south" => "\"winner\":\"west\"",
                _ => "\"winner\":\"north\"",
            });
        File.WriteAllLines(path, lines);

        var (exit, _, error) = await Run("audit", "--log", path);
        Assert.Equal(CliApp.GameFailed, exit);
        Assert.Contains("first offending event", error);
    }

    [Fact]
    public async Task Replay_with_a_mixed_preset_works()
    {
        string dir = TempDir();
        var (exit, _, _) = await Run("replay", "--seed", "8", "--seats", "mixed-2", "--out", Path.Combine(dir, "m.jsonl"));
        Assert.Equal(CliApp.Ok, exit);
        Assert.Contains("ScriptedHuman", File.ReadAllText(Path.Combine(dir, "m.jsonl")));
    }
}
