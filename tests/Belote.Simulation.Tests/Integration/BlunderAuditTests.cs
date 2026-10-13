using System.Collections.Immutable;
using Belote.Bots;
using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

/// <summary>
/// Automated stand-in for "no obvious blunders" (research.md D10): objective predicates checked on
/// every decision of the smart bot in recorded games.
/// </summary>
public class BlunderAuditTests
{
    private sealed class Auditor : IPlayer
    {
        private readonly SmartBot _inner = new();
        public int TrumpOverPartnerChances;
        public int FailedToWinWorthwhileChances;
        public readonly List<string> Violations = new();

        public async ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken)
        {
            var action = await _inner.DecideAsync(view, cancellationToken);
            if (view.Phase != Phase.Playing || action is not PlayCardAction play || view.CurrentTrick.Length != 3)
            {
                return action;
            }

            var rules = view.RuleSet;
            var kind = view.Contract!.Kind;
            var best = TrickResolver.Best(rules, kind, view.CurrentTrick);
            var legal = LegalActions.For(view).OfType<PlayCardAction>().Select(a => a.Card).ToList();
            var trump = kind.TrumpSuit();
            bool partnerWinning = best.Seat == view.Seat.Partner();
            var led = view.CurrentTrick[0].Card.Suit;

            // (1) Last to play, partner already winning: do not spend a trump when a non-trump is legal.
            if (partnerWinning && trump is { } t && legal.Any(c => c.Suit != t))
            {
                TrumpOverPartnerChances++;
                if (play.Card.Suit == t && best.Card.Suit != t)
                {
                    Violations.Add($"trumped partner: {view.Seat} played {play.Card} over {best.Card} (hand {string.Join(' ', view.OwnHand)})");
                }
            }

            // (2) Last to play, an opponent winning a trick worth points, a legal card wins: win it.
            if (!partnerWinning)
            {
                int points = view.CurrentTrick.Sum(p => rules.CardPoints(kind, p.Card));
                int bestStrength = CardStrength.Of(rules, kind, led, best.Card);
                bool canWin = legal.Any(c => CardStrength.Of(rules, kind, led, c) > bestStrength);
                if (points > 0 && canWin)
                {
                    FailedToWinWorthwhileChances++;
                    if (CardStrength.Of(rules, kind, led, play.Card) <= bestStrength)
                    {
                        Violations.Add($"declined a {points}-point trick: {view.Seat} played {play.Card} against {best.Card}");
                    }
                }
            }

            return action;
        }
    }

    [Fact]
    public async Task Smart_bots_commit_no_objective_blunders_in_two_hundred_games()
    {
        var auditors = new Dictionary<Seat, Auditor>();
        int chances1 = 0;
        int chances2 = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            auditors = Seats.All.ToDictionary(s => s, _ => new Auditor());
            var seats = Seats.All.ToDictionary(s => s, s => new SeatController("SmartBot", auditors[s]));
            var run = await GameRunner.RunAsync(RuleSets.Bulgarian, seed, seats);
            Assert.True(run.Finished, run.FailureDetail);

            foreach (var auditor in auditors.Values)
            {
                Assert.Empty(auditor.Violations);
                chances1 += auditor.TrumpOverPartnerChances;
                chances2 += auditor.FailedToWinWorthwhileChances;
            }
        }

        // The predicates must actually have been exercised, not vacuously true.
        Assert.True(chances1 > 50, $"trump-over-partner chances: {chances1}");
        Assert.True(chances2 > 50, $"worthwhile-win chances: {chances2}");
    }
}
