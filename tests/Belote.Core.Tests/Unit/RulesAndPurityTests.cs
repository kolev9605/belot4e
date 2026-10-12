using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Belote.Core;
using Xunit;

namespace Belote.Core.Tests;

public class RuleSetTests
{
    [Fact]
    public void Bulgarian_rule_set_is_valid()
    {
        Assert.Empty(RuleSetValidator.Validate(RuleSets.Bulgarian));
    }

    [Fact]
    public void R8_R9_R10_Points_in_play_match_the_published_totals()
    {
        var rules = RuleSets.Bulgarian;
        Assert.Equal(162, rules.PointsInPlay(ContractKind.Hearts));
        Assert.Equal(162, rules.PointsInPlay(ContractKind.Clubs));
        Assert.Equal(260, rules.PointsInPlay(ContractKind.NoTrump));
        Assert.Equal(258, rules.PointsInPlay(ContractKind.AllTrump));
    }

    [Fact]
    public void R7_Trump_and_plain_strength_orders()
    {
        var rules = RuleSets.Bulgarian;
        var trump = rules.Contracts.Suit.Trump;
        Assert.True(trump.Strength(Rank.Jack) > trump.Strength(Rank.Nine));
        Assert.True(trump.Strength(Rank.Nine) > trump.Strength(Rank.Ace));
        Assert.True(trump.Strength(Rank.Ace) > trump.Strength(Rank.Ten));
        var plain = rules.Contracts.Suit.Plain;
        Assert.True(plain.Strength(Rank.Ace) > plain.Strength(Rank.Ten));
        Assert.True(plain.Strength(Rank.Jack) > plain.Strength(Rank.Nine));
    }

    [Theory]
    [InlineData(Suit.Hearts, Rank.Jack, 20)]
    [InlineData(Suit.Hearts, Rank.Nine, 14)]
    [InlineData(Suit.Spades, Rank.Jack, 2)]
    [InlineData(Suit.Spades, Rank.Nine, 0)]
    [InlineData(Suit.Spades, Rank.Ten, 10)]
    public void R8_Card_points_in_a_hearts_contract(Suit suit, Rank rank, int expected)
    {
        Assert.Equal(expected, RuleSets.Bulgarian.CardPoints(ContractKind.Hearts, new Card(suit, rank)));
    }

    [Fact]
    public void R9_No_trump_values_are_the_plain_values_doubled()
    {
        var rules = RuleSets.Bulgarian;
        foreach (var rank in Enum.GetValues<Rank>())
        {
            Assert.Equal(
                2 * rules.Contracts.Suit.Plain.PointsOf(rank),
                rules.Contracts.NoTrump.Table.PointsOf(rank));
        }

        Assert.Equal(20, rules.LastTrickBonus(ContractKind.NoTrump));
    }

    [Fact]
    public void R10_All_trump_uses_trump_values_in_every_suit()
    {
        var rules = RuleSets.Bulgarian;
        foreach (var suit in Enum.GetValues<Suit>())
        {
            Assert.Equal(20, rules.CardPoints(ContractKind.AllTrump, new Card(suit, Rank.Jack)));
            Assert.Equal(14, rules.CardPoints(ContractKind.AllTrump, new Card(suit, Rank.Nine)));
        }
    }

    [Fact]
    public void R3_Bid_ranking_runs_from_clubs_up_to_all_trump()
    {
        var rules = RuleSets.Bulgarian;
        Assert.Equal(
            new[]
            {
                ContractKind.Clubs, ContractKind.Diamonds, ContractKind.Hearts,
                ContractKind.Spades, ContractKind.NoTrump, ContractKind.AllTrump,
            },
            rules.Bidding.Ranking.ToArray());
    }

    [Fact]
    public void Invalid_rule_sets_are_reported_with_paths()
    {
        var rules = RuleSets.Bulgarian;

        var badPattern = rules with { Deal = new DealRules(ImmutableArray.Create(3, 3, 3), 2) };
        Assert.Contains(RuleSetValidator.Validate(badPattern), i => i.Path == "deal.pattern");

        var badOrder = rules with
        {
            Contracts = rules.Contracts with
            {
                NoTrump = rules.Contracts.NoTrump with
                {
                    Table = rules.Contracts.NoTrump.Table with
                    {
                        Order = ImmutableArray.Create(Rank.Ace, Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Nine, Rank.Eight, Rank.Seven),
                    },
                },
            },
        };
        Assert.Contains(RuleSetValidator.Validate(badOrder), i => i.Path == "contracts.noTrump.order");

        var dupRanking = rules with
        {
            Bidding = rules.Bidding with
            {
                Ranking = ImmutableArray.Create(
                    ContractKind.Clubs, ContractKind.Clubs, ContractKind.Hearts,
                    ContractKind.Spades, ContractKind.NoTrump, ContractKind.AllTrump),
            },
        };
        Assert.Contains(RuleSetValidator.Validate(dupRanking), i => i.Path == "bidding.ranking");

        var noTarget = rules with { Match = rules.Match with { TargetScore = 0 } };
        Assert.Contains(RuleSetValidator.Validate(noTarget), i => i.Path == "match.targetScore");

        Assert.Throws<ArgumentException>(() => GameEngine.Start(noTarget, 1));
    }
}

public class CorePurityTests
{
    [Fact]
    public void Belote_Core_references_only_the_base_class_library()
    {
        var references = typeof(Card).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.All(
            references,
            name => Assert.True(
                name.StartsWith("System", StringComparison.Ordinal)
                || name is "mscorlib" or "netstandard",
                $"Unexpected dependency: {name}"));
    }

    [Fact]
    public void Core_sources_use_no_io_clock_or_ambient_randomness()
    {
        string root = FindRepositoryRoot();
        string core = Path.Combine(root, "src", "Belote.Core");
        var banned = new[]
        {
            @"DateTime\s*\.\s*(Now|UtcNow|Today)", @"DateTimeOffset\s*\.\s*(Now|UtcNow)", @"Stopwatch",
            @"System\.Random\b", @"\bnew\s+Random\s*\(", @"Random\s*\.\s*Shared", @"Guid\s*\.\s*NewGuid",
            @"using\s+System\.IO\b", @"\bFile\s*\.", @"\bConsole\s*\.", @"Environment\s*\.",
            @"RandomNumberGenerator", @"Task\s*\.\s*Delay", @"Thread\s*\.\s*Sleep",
        };

        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            string text = File.ReadAllText(file);
            foreach (var pattern in banned)
            {
                if (Regex.IsMatch(text, pattern))
                {
                    violations.Add($"{Path.GetFileName(file)}: {pattern}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Rule_values_are_defined_only_in_the_rule_set_file()
    {
        // Principle IV: no card point tables or bonuses may be hard-coded outside RuleSets.cs.
        string root = FindRepositoryRoot();
        string core = Path.Combine(root, "src", "Belote.Core");
        var suspicious = new[] { @"\b162\b", @"\b258\b", @"\b260\b", @"\b151\b", @"==\s*90\b" };
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && Path.GetFileName(f) != "RuleSets.cs"))
        {
            string text = Regex.Replace(File.ReadAllText(file), @"///.*|//.*", string.Empty);
            foreach (var pattern in suspicious)
            {
                if (Regex.IsMatch(text, pattern))
                {
                    violations.Add($"{Path.GetFileName(file)}: {pattern}");
                }
            }
        }

        Assert.Empty(violations);
    }

    internal static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Belote.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Belote.sln not found above the test directory.");
    }
}
