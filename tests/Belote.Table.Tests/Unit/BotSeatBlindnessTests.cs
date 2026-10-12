using Microsoft.Extensions.Time.Testing;
using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class BotSeatBlindnessTests
{
    [Fact]
    public async Task FR021_EachBotOnlyEverReceivesItsOwnSeatView()
    {
        var calls = new List<(Seat Expected, Seat Got)>();
        IPlayer Make(Seat seat) => new Spy(seat, calls, new Belote.Bots.RandomLegalBot(Seeds.ForSeat(15, seat)));
        await using var table = Harness.NewTable(15, bots: Make);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.True(calls.Count > 100);
        Assert.All(calls, c => Assert.Equal(c.Expected, c.Got));
    }

    [Fact]
    public async Task FR020_BotsUseTheSameSubmitPathAndTheSameOwnershipChecks()
    {
        var time = new FakeTimeProvider();
        Seat toAct = default;
        LiveTable? table = null;
        for (ulong seed = 1; seed < 60 && table is null; seed++)
        {
            var candidate = Harness.NewTable(seed, TimeSpan.FromSeconds(5), time);
            await candidate.TakeSeatAsync(Harness.Client);
            toAct = candidate.Snapshot.State!.ToAct;
            if (toAct != Seat.South)
            {
                table = candidate;
            }
            else
            {
                await candidate.DisposeAsync();
            }
        }

        Assert.NotNull(table);
        await using var _ = table;
        var view = Projection.For(table.Snapshot.State!, toAct);
        var legal = LegalActions.For(view)[0];
        var before = table.Snapshot.Version;

        // Another bot's principal cannot act for this seat: ownership is plain equality.
        var wrong = await table.SubmitAsync(new Intent(toAct, legal, new BotPrincipal(toAct.Next())));
        Assert.Equal(RejectionCategory.NotOwner, Assert.IsType<IntentRejected>(wrong).Rejection.Category);

        // The seat's own principal goes through the identical pipeline and is accepted.
        var right = await table.SubmitAsync(new Intent(toAct, legal, new BotPrincipal(toAct)));
        Assert.Equal(before + 1, Assert.IsType<IntentAccepted>(right).Version);
    }

    private sealed class Spy : IPlayer
    {
        private readonly Seat _seat;
        private readonly List<(Seat, Seat)> _calls;
        private readonly IPlayer _inner;

        public Spy(Seat seat, List<(Seat, Seat)> calls, IPlayer inner)
        {
            _seat = seat;
            _calls = calls;
            _inner = inner;
        }

        public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken)
        {
            lock (_calls)
            {
                _calls.Add((_seat, view.Seat));
            }

            return _inner.DecideAsync(view, cancellationToken);
        }
    }
}
