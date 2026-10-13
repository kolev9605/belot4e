using Belote.Bots;
using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

public class MixedBotTests
{
    [Fact]
    public async Task Every_combination_of_smart_and_random_seats_finishes_audited_games()
    {
        for (int mask = 0; mask < 16; mask++)
        {
            int captured = mask;
            var report = await SimulationRunner.RunAsync(new RunOptions
            {
                BaseSeed = 100,
                GameCount = 20,
                Audit = true,
                KeepLogs = KeepLogs.None,
                SeatFactory = (_, seed) => Seats.All.ToDictionary(
                    s => s,
                    s => ((captured >> (int)s) & 1) == 1
                        ? new SeatController("SmartBot", new SmartBot())
                        : new SeatController("RandomLegalBot", new RandomLegalBot(Seeds.ForSeat(seed, s)))),
            });

            Assert.True(
                report.Failed == 0,
                $"mask {mask}: {report.Games.FirstOrDefault(g => !g.FinishedCorrectly)?.FailureDetail}");
            Assert.Equal(20, report.Passed);
        }
    }
}
