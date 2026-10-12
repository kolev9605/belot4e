using System.Diagnostics;
using Belote.Core;
using Belote.Simulation;
using Belote.Table;
using Belote.Table.Tests;

namespace Belote.Server.Tests;

public class HundredSeededGames
{
    private const int Games = 100;

    /// <summary>Events visible at <paramref name="version"/>: everything before the (version+1)-th action.</summary>
    private static IReadOnlyList<GameEvent> EventsAt(IReadOnlyList<GameEvent> events, int version)
    {
        int actions = 0;
        for (int i = 0; i < events.Count; i++)
        {
            bool isAction = events[i] is BidMade or PassMade or ContraMade or RecontraMade
                or AnnouncementDeclared or CardPlayed;
            if (!isAction)
            {
                continue;
            }

            if (actions == version)
            {
                return events.Take(i).ToList();
            }

            actions++;
        }

        return events;
    }

    [Fact]
    public async Task SC001_SC002_SC004_SC006_HundredGamesOverOneConnection()
    {
        await using var server = await LiveServer.StartAsync();
        await using var client = await TableClient.ConnectAsync(server.HubUrl);
        var clock = Stopwatch.StartNew();

        for (int game = 0; game < Games; game++)
        {
            ulong seed = Seeds.ForGame(2026, game);
            var created = await client.CreateTableAsync(seed, botDelayMs: 0);
            Assert.True(created.Ok, $"game {game}: {created.Rejection?.Code}");
            Assert.True((await client.TakeSeatAsync("south")).Ok);

            // PlayToEndAsync asserts contiguous versions and that no legal intent is rejected.
            var updates = await client.PlayToEndAsync(ClientPolicies.SeededSouth(seed));
            Assert.True(updates[^1].Finished, $"game {game} (seed {seed}) did not finish");

            var finished = server.Registry.Finished.All.Single(g => g.Log.Seed == seed);
            var events = finished.Log.Events;

            // Privacy: nothing outside the own hand or the public table in any pushed update.
            foreach (var update in updates)
            {
                var problems = PrivacyOracle.Violations(update, EventsAt(events, update.Version));
                Assert.True(problems.Count == 0, $"game {game} (seed {seed}): {string.Join("; ", problems)}");
            }

            // The on-demand view after the game is the final update, and is also clean.
            var view = await client.GetViewAsync();
            Assert.Null(view.Rejection);
            Assert.Equal(updates[^1].Version, view.Update!.Version);
            Assert.Empty(PrivacyOracle.Violations(view.Update, events));

            // Same game as the 001 all-bot simulation of this seed.
            var simulated = await GameRunner.RunAsync(
                RuleSets.Bulgarian, seed, SeatPresets.Create(SeatPresets.AllBot, seed));
            Assert.True(simulated.Finished, simulated.FailureDetail);
            Assert.Equal(
                simulated.Log.Events.Select(GameLog.EventLine),
                events.Select(GameLog.EventLine));
            Assert.Equal(simulated.NorthSouth, finished.NorthSouth);
            Assert.Equal(simulated.EastWest, finished.EastWest);

            // Replaying the recorded log reproduces the final state.
            var audit = GameAuditor.Audit(finished.Log);
            Assert.True(audit.Ok, $"game {game} (seed {seed}): {audit.First}");
        }

        Assert.Equal(Games, server.Registry.Finished.All.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"100 games took {clock.Elapsed}");
    }
}
