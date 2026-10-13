using System;

namespace Belote.Bots;

/// <summary>
/// The tunable weights and thresholds of <see cref="SmartBot"/>: the single shipped difficulty is
/// <see cref="Standard"/>. Scores are expressed in estimated card points so the numbers can be read
/// against the rule set's point tables; they are heuristics, not rules (see research.md D4/D5).
/// </summary>
public sealed record SmartBotOptions
{
    public static SmartBotOptions Standard { get; } = new();

    /// <summary>Minimum best-contract score to bid instead of pass.</summary>
    public double BidThreshold { get; init; } = 36;

    /// <summary>Extra score required to outbid the partner's standing bid.</summary>
    public double OvercallPartnerMargin { get; init; } = 14;

    /// <summary>Minimum own score in the standing contract's kind to double an opposing contract.</summary>
    public double ContraThreshold { get; init; } = 60;

    /// <summary>Minimum own score in the standing contract's kind to redouble the team's contract.</summary>
    public double RecontraThreshold { get; init; } = 62;

    /// <summary>Score per trump beyond the first in a suit contract.</summary>
    public double TrumpLengthWeight { get; init; } = 8;

    /// <summary>Score for holding both king and queen of the trump suit.</summary>
    public double BeloteWeight { get; init; } = 10;

    /// <summary>Score per plain ace (and half of it per ten protected by its ace) in a suit contract.</summary>
    public double SideAceWeight { get; init; } = 8;

    /// <summary>Scale applied to the no-trump point total (declarer chooses no trump less often than a suit).</summary>
    public double NoTrumpScale { get; init; } = 0.6;

    /// <summary>Scale applied to the all-trump point total.</summary>
    public double AllTrumpScale { get; init; } = 0.5;

    /// <summary>Card points at or above which an unprotected (non-master) card is not led.</summary>
    public int RiskyLeadPoints { get; init; } = 4;

    internal void Validate()
    {
        foreach (var (name, value) in new (string, double)[]
        {
            (nameof(BidThreshold), BidThreshold),
            (nameof(OvercallPartnerMargin), OvercallPartnerMargin),
            (nameof(ContraThreshold), ContraThreshold),
            (nameof(RecontraThreshold), RecontraThreshold),
            (nameof(TrumpLengthWeight), TrumpLengthWeight),
            (nameof(BeloteWeight), BeloteWeight),
            (nameof(SideAceWeight), SideAceWeight),
            (nameof(NoTrumpScale), NoTrumpScale),
            (nameof(AllTrumpScale), AllTrumpScale),
            (nameof(RiskyLeadPoints), RiskyLeadPoints),
        })
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentException($"{name} must be finite and non-negative.", nameof(SmartBotOptions));
            }
        }
    }
}
