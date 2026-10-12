using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Belote.Core;

namespace Belote.Core.Tests;

/// <summary>Helpers that build states, hands and tricks from short card strings such as "JH 9H AS".</summary>
internal static class T
{
    public static readonly RuleSet Rules = RuleSets.Bulgarian;

    public static ImmutableArray<Card> Hand(string cards) =>
        cards.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToImmutableArray();

    public static ImmutableArray<PlayedCard> Plays(params (Seat Seat, string Card)[] plays) =>
        plays.Select(p => new PlayedCard(p.Seat, Card.Parse(p.Card))).ToImmutableArray();

    /// <summary>A trick whose winner is assigned directly (scoring tests choose who wins what).</summary>
    public static Trick Trick(Seat winner, string cards)
    {
        var parsed = Hand(cards);
        var plays = parsed
            .Select((c, i) => new PlayedCard(winner.After(i), c))
            .ToImmutableArray();
        return new Trick(winner, plays, winner);
    }

    public static ImmutableArray<Trick> Tricks(params (Seat Winner, string Cards)[] tricks) =>
        tricks.Select(t => Trick(t.Winner, t.Cards)).ToImmutableArray();

    /// <summary>A hand in progress: all four seats hold the given cards and the contract is fixed.</summary>
    public static GameState Playing(
        ContractKind kind,
        Seat declarer,
        string north,
        string east,
        string south,
        string west,
        Seat? leader = null,
        int multiplier = 1,
        int heldOver = 0,
        int northSouthScore = 0,
        int eastWestScore = 0,
        RuleSet? rules = null)
    {
        var first = leader ?? Seat.North;
        var hands = ImmutableArray.Create(Hand(north), Hand(east), Hand(south), Hand(west));
        var hand = new HandState(
            Dealer: first.After(3),
            DealNumber: 1,
            Hands: hands,
            DealtCount: 8,
            Bidding: BiddingState.Empty,
            Contract: new Contract(kind, declarer, multiplier),
            Tricks: ImmutableArray<Trick>.Empty,
            Leader: first,
            CurrentPlays: ImmutableArray<PlayedCard>.Empty,
            ToAct: first,
            Declared: ImmutableArray<DeclaredAnnouncement>.Empty,
            AnnouncementsRevealed: false,
            SequenceTeam: null,
            CarreTeam: null,
            Belotes: ImmutableArray<BeloteAward>.Empty);
        return new GameState(
            rules ?? Rules,
            Seed: 1,
            Phase.Playing,
            hand,
            ImmutableArray.Create(northSouthScore, eastWestScore),
            heldOver,
            Winner: null,
            HandsScored: 0,
            NextSequence: 100);
    }

    public static GameState Accept(GameState state, Seat seat, PlayerAction action, List<GameEvent>? events = null)
    {
        var result = GameEngine.Apply(state, seat, action);
        var accepted = Assert_IsAccepted(result, seat, action);
        events?.AddRange(accepted.Events);
        return accepted.State;
    }

    public static Accepted Assert_IsAccepted(ApplyResult result, Seat seat, PlayerAction action) =>
        result as Accepted
        ?? throw new Xunit.Sdk.XunitException($"{seat} {action} was rejected: {((Rejected)result).Reason}");

    public static Rejected Reject(GameState state, Seat seat, PlayerAction action) =>
        GameEngine.Apply(state, seat, action) as Rejected
        ?? throw new Xunit.Sdk.XunitException($"{seat} {action} was accepted but should be rejected.");

    public static PlayCardAction Play(string card) => new(Card.Parse(card));

    /// <summary>Plays the given cards in order, each by the seat that is to act.</summary>
    public static GameState PlayCards(GameState state, string cards, List<GameEvent>? events = null)
    {
        foreach (var text in cards.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            state = Accept(state, state.ToAct, Play(text), events);
        }

        return state;
    }
}

/// <summary>Plays whole games with a seeded random-legal policy, reporting every step to a callback.</summary>
internal static class RandomGames
{
    public static GameState Play(
        ulong seed,
        Action<GameState, Seat, PlayerAction, Accepted>? onStep = null,
        RuleSet? rules = null,
        int maxActions = 20000)
    {
        var start = GameEngine.Start(rules ?? T.Rules, seed);
        var state = start.State;
        var rng = new SeededRandom(seed ^ 0xABCDEF);
        int steps = 0;
        while (state.Phase != Phase.Finished)
        {
            if (++steps > maxActions)
            {
                throw new InvalidOperationException("Game did not finish.");
            }

            var seat = state.ToAct;
            var legal = LegalActions.For(Projection.For(state, seat));
            if (legal.Count == 0)
            {
                throw new InvalidOperationException($"{seat} has no legal action in deal {state.DealNumber}.");
            }

            var action = legal[rng.NextInt(legal.Count)];
            var result = GameEngine.Apply(state, seat, action);
            var accepted = result as Accepted
                ?? throw new InvalidOperationException($"Legal action rejected: {action}.");
            onStep?.Invoke(state, seat, action, accepted);
            state = accepted.State;
        }

        return state;
    }
}
