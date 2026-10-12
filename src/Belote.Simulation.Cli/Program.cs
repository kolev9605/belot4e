using System;
using System.Threading.Tasks;

namespace Belote.Simulation.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args) =>
        await CliApp.RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
}
