using System;
using System.Linq;

namespace Belote.Core;

/// <summary>
/// Converts raw hand points to game points. The pool is the rounded total / divisor (half rounds up);
/// each team gets the floor of its own amount / divisor and the leftover game points go to the
/// largest remainders, ties to the favoured team. This keeps the two teams' game points summing to the
/// pool exactly, which is why e.g. 154 + 104 in all-trump becomes 15 + 11 (the team with fewer points
/// is favoured), and 106 + 106 becomes 11 (declarer, held over) + 10.
/// </summary>
public static class GamePointConverter
{
    /// <summary>Game points in play for a raw total (half rounds up).</summary>
    public static int Pool(int total, int divisor) =>
        total / divisor + (total % divisor * 2 >= divisor ? 1 : 0);

    /// <summary>Splits the pool of <c>Pool(sum(amounts))</c> between the two teams.</summary>
    public static int[] Split(int[] amounts, int divisor, Team favored)
    {
        if (amounts.Length != 2)
        {
            throw new ArgumentException("Exactly two team amounts are required.", nameof(amounts));
        }

        int pool = Pool(amounts[0] + amounts[1], divisor);
        var result = new[] { amounts[0] / divisor, amounts[1] / divisor };
        var remainder = new[] { amounts[0] % divisor, amounts[1] % divisor };
        int extra = Math.Max(0, pool - result[0] - result[1]);
        var order = new[] { 0, 1 }
            .OrderByDescending(i => remainder[i])
            .ThenBy(i => i == (int)favored ? 0 : 1)
            .ToArray();
        for (int k = 0; k < extra && k < 2; k++)
        {
            result[order[k]]++;
        }

        return result;
    }
}
