using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class SeatingTests
{
    [Fact]
    public async Task FR004_OnlyTheCreatorMaySitAndOnlyOnce()
    {
        await using var table = Harness.NewTable(3);

        var stranger = await table.TakeSeatAsync(new ConnectionPrincipal("someone-else"));
        Assert.False(stranger.Ok);
        Assert.Equal(RejectionCategory.NotOwner, stranger.Rejection!.Category);
        Assert.Equal(TableLifecycle.AwaitingSeat, table.Snapshot.Lifecycle);

        Assert.True((await table.TakeSeatAsync(Harness.Client)).Ok);

        var again = await table.TakeSeatAsync(Harness.Client);
        Assert.False(again.Ok);
        Assert.Equal("SeatTaken", again.Rejection!.Code);
    }

    [Fact]
    public async Task FR003_ClientIsSouthAndBotsHoldTheOtherSeats()
    {
        var seen = new List<(Seat Seat, string View)>();
        IPlayer Recording(Seat seat) => new RecordingBot(seat, seen, new Belote.Bots.RandomLegalBot(Seeds.ForSeat(11, seat)));
        await using var table = Harness.NewTable(11, bots: Recording);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.All(recorder.Updates, u => Assert.Equal("south", u.View.Seat));
        Assert.Equal(
            new[] { Seat.North, Seat.East, Seat.West },
            seen.Select(x => x.Seat).Distinct().OrderBy(x => (int)x).ToArray());
        Assert.All(seen, x => Assert.Equal(x.Seat.ToString(), x.View));
        Assert.DoesNotContain(seen, s => s.Seat == Seat.South);
        Assert.Equal(Seat.North, Seat.South.Partner());
    }

    private sealed class RecordingBot : IPlayer
    {
        private readonly Seat _seat;
        private readonly List<(Seat, string)> _seen;
        private readonly IPlayer _inner;

        public RecordingBot(Seat seat, List<(Seat, string)> seen, IPlayer inner)
        {
            _seat = seat;
            _seen = seen;
            _inner = inner;
        }

        public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken)
        {
            lock (_seen)
            {
                _seen.Add((_seat, view.Seat.ToString()));
            }

            return _inner.DecideAsync(view, cancellationToken);
        }
    }
}
