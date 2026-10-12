using System;
using Belote.Core;

namespace Belote.Table;

/// <summary>Everything that fixes one table: rules, seed, and bot pacing.</summary>
public sealed record TableOptions
{
    public static readonly TimeSpan MaxBotDelay = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _botDelay = TimeSpan.FromMilliseconds(600);

    public RuleSet RuleSet { get; init; } = RuleSets.Bulgarian;

    /// <summary>The deterministic game seed. The caller (not the table) chooses it.</summary>
    public ulong Seed { get; init; }

    /// <summary>Pause before each bot move, clamped to "0 to 5 s". Zero means no waiting.</summary>
    public TimeSpan BotDelay
    {
        get => _botDelay;
        init => _botDelay = Clamp(value);
    }

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    public static TimeSpan Clamp(TimeSpan delay) =>
        delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaxBotDelay ? MaxBotDelay : delay;
}
