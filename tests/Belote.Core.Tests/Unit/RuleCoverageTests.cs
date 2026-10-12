using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Belote.Core.Tests;

public class RuleCoverageTests
{
    [Fact]
    public void SC_005_Every_rule_row_R1_to_R25_in_research_md_has_at_least_one_named_test()
    {
        var covered = typeof(RuleCoverageTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => Regex.Match(m.Name, @"R(\d+)(?:_R(\d+))*_"))
            .Where(m => m.Success)
            .SelectMany(m => Regex.Matches(m.Value, @"R(\d+)").Select(x => int.Parse(x.Groups[1].Value)))
            .ToHashSet();

        var missing = Enumerable.Range(1, 25).Where(r => !covered.Contains(r)).Select(r => $"R{r}").ToList();
        Assert.True(missing.Count == 0, "Rules without a test named Rxx_*: " + string.Join(", ", missing));
    }
}
