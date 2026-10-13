using Microsoft.Extensions.Time.Testing;
using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class PerSeatUpdateTests
{
    private static LiveTable TwoHumans() => LiveTable.CreateStarted(
        new TableOptions { Seed = 21, BotDelay = TimeSpan.FromHours(1), TimeProvider = new FakeTimeProvider() },
        new Dictionary<Seat, Principal>
        {
            [Seat.South] = new HumanPrincipal(Guid.NewGuid()),
            [Seat.North] = new HumanPrincipal(Guid.NewGuid()),
        });

    [Fact]
    public async Task Each_seat_gets_only_its_own_hand_and_the_same_version()
    {
        await using var table = TwoHumans();

        var south = table.BuildUpdateFor(Seat.South)!;
        var north = table.BuildUpdateFor(Seat.North)!;
        var east = table.BuildUpdateFor(Seat.East)!;

        Assert.Equal("south", south.View.Seat);
        Assert.Equal("north", north.View.Seat);
        Assert.Equal("east", east.View.Seat);
        Assert.Equal(south.Version, north.Version);
        Assert.Empty(south.View.OwnHand.Intersect(north.View.OwnHand));
        Assert.Empty(south.View.OwnHand.Intersect(east.View.OwnHand));
        Assert.NotEmpty(south.View.OwnHand);
    }

    [Fact]
    public async Task Legal_actions_are_only_offered_to_the_seat_to_act()
    {
        await using var table = TwoHumans();
        var toAct = table.Snapshot.State!.ToAct;

        foreach (var seat in Seats.All)
        {
            var update = table.BuildUpdateFor(seat)!;
            if (seat == toAct)
            {
                Assert.NotEmpty(update.View.LegalActions);
            }
            else
            {
                Assert.Empty(update.View.LegalActions);
            }
        }
    }

    [Fact]
    public async Task Hidden_information_never_appears_in_any_seat_update_json()
    {
        await using var table = TwoHumans();
        var snap = table.Snapshot;

        foreach (var seat in Seats.All)
        {
            var json = Harness.Json(table.BuildUpdateFor(seat, snap));
            Assert.DoesNotContain("seed", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"deck\"", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
