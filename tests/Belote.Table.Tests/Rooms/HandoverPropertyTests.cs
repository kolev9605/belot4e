using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

/// <summary>
/// Property: whatever order disconnects, returns, skipped waits and elapsed minutes arrive in, a game of
/// two humans and two bots always reaches a final result and nobody ever sees the table go backwards.
/// </summary>
public class HandoverPropertyTests
{
    public static IEnumerable<object[]> Schedules() => Enumerable.Range(1, 12).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(Schedules))]
    public async Task Any_schedule_of_drops_returns_and_skips_still_finishes_the_game(int scheduleSeed)
    {
        var random = new Random(scheduleSeed * 7919);
        await using var rig = new RoomRig(TimeSpan.Zero, seed: (ulong)(100 + scheduleSeed));
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        var people = new[] { ivo, mira };
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);

        var connected = people.ToDictionary(p => p, _ => true);
        var submitted = people.ToDictionary(p => p, _ => -1);
        var seenVersions = people.ToDictionary(p => p, _ => new List<int>());
        var until = DateTime.UtcNow.AddSeconds(90);
        int step = 0;

        while ((await rig.Room.InfoAsync()).Lifecycle == RoomLifecycle.InGame)
        {
            Assert.True(DateTime.UtcNow < until, $"Schedule {scheduleSeed} did not finish.");
            step++;

            // Disturb the room now and then, but always end up with everyone back.
            if (step % 7 == 0 && step < 400)
            {
                var victim = people[random.Next(people.Length)];
                var other = people.First(p => p != victim);
                switch (random.Next(5))
                {
                    case 0 when connected[victim]:
                        await rig.DisconnectAsync(victim);
                        connected[victim] = false;
                        break;
                    case 1 when !connected[victim]:
                        var back = await rig.RejoinAsync(victim);
                        Assert.True(back.Ok, back.Rejection?.Code);
                        connected[victim] = true;
                        break;
                    case 2:
                        await rig.AdvanceAsync(TimeSpan.FromSeconds(61));
                        break;
                    case 3 when connected[other] && !connected[victim]:
                        await rig.Room.SkipWaitAsync(other.Token, null);
                        break;
                    default:
                        await rig.AdvanceAsync(TimeSpan.FromSeconds(random.Next(1, 30)));
                        break;
                }
            }

            foreach (var person in people)
            {
                var update = person.Sink.LastUpdate;
                if (update is null)
                {
                    continue;
                }

                var list = seenVersions[person];
                if (list.Count == 0 || list[^1] != update.Version)
                {
                    list.Add(update.Version);
                }

                if (!connected[person] || update.Finished || update.View.LegalActions.Count == 0
                    || submitted[person] == update.Version)
                {
                    continue;
                }

                submitted[person] = update.Version;
                var outcome = await rig.Room.SubmitIntentAsync(person.Token, update.View.LegalActions[0]);
                if (outcome is IntentRejected rejected)
                {
                    Assert.True(
                        rejected.Rejection.Category is RejectionCategory.Paused or RejectionCategory.NotOwner
                            or RejectionCategory.NotSeated,
                        $"Unexpected rejection {rejected.Rejection.Code}");
                    submitted[person] = -1;
                }
            }

            // Bring everyone back if the schedule has run long, so the game can finish.
            if (step >= 400)
            {
                foreach (var person in people.Where(p => !connected[p]))
                {
                    await rig.RejoinAsync(person);
                    connected[person] = true;
                }
            }

            await Task.Delay(1);
        }

        await rig.Room.SyncAsync();
        Assert.Equal(RoomLifecycle.Finished, (await rig.Room.InfoAsync()).Lifecycle);
        foreach (var person in people)
        {
            var versions = seenVersions[person];
            Assert.Equal(versions.OrderBy(v => v).ToList(), versions);
        }
    }
}
