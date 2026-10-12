using Belote.Core;
using Belote.Simulation;

namespace Belote.Table.Tests;

internal static class Lines
{
    public static string[] Of(IEnumerable<GameEvent> events) =>
        events.Select(GameLog.EventLine).ToArray();
}

internal static class Replay
{
    private static (Seat Seat, PlayerAction Action)? ActionOf(GameEvent e) => e switch
    {
        BidMade b => (b.Seat, new BidAction(b.Kind)),
        PassMade p => (p.Seat, new PassAction()),
        ContraMade c => (c.Seat, new ContraAction()),
        RecontraMade r => (r.Seat, new RecontraAction()),
        AnnouncementDeclared a => (a.Seat, new AnnounceAction(a.Spec)),
        CardPlayed c => (c.Seat, new PlayCardAction(c.Card)),
        _ => null,
    };

    /// <summary>Replays the accepted actions of a (possibly partial) log and returns the rebuilt state and events.</summary>
    public static (GameState State, List<GameEvent> Events) Run(RuleSet rules, ulong seed, IEnumerable<GameEvent> events)
    {
        var start = GameEngine.Start(rules, seed);
        var state = start.State;
        var produced = new List<GameEvent>(start.Events);
        foreach (var e in events)
        {
            if (ActionOf(e) is not { } step)
            {
                continue;
            }

            var result = GameEngine.Apply(state, step.Seat, step.Action);
            var accepted = result as Belote.Core.Accepted
                ?? throw new InvalidOperationException($"Replay rejected {step.Seat} {step.Action}.");
            state = accepted.State;
            produced.AddRange(accepted.Events);
        }

        return (state, produced);
    }
}
