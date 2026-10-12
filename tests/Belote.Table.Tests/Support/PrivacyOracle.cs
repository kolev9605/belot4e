using System.Text.Json;
using System.Text.RegularExpressions;
using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

/// <summary>
/// Independent check that a message holds no card outside the seat's own hand or the public table.
/// It is built from the omniscient event list, never from the view being checked.
/// </summary>
internal static class PrivacyOracle
{
    private static readonly Regex CardToken = new("^(7|8|9|10|J|Q|K|A)[CDHS]$", RegexOptions.Compiled);
    private static readonly string[] ForbiddenKeys = { "seed", "deck", "hands", "events", "log", "undealt" };

    public static IReadOnlyList<string> Violations(
        TableUpdateDto update,
        IReadOnlyList<GameEvent> eventsUpToUpdate,
        Seat seat = Seat.South)
    {
        var problems = new List<string>();
        int deal = update.View.DealNumber;

        var dealt = eventsUpToUpdate.OfType<HandDealt>().Where(e => e.Deal == deal).LastOrDefault();
        var allowed = new HashSet<string>();
        if (dealt is not null)
        {
            var mine = dealt.Hands[(int)seat];
            int visible = update.View.Phase == "bidding" ? dealt.DealtBeforeBidding : mine.Length;
            foreach (var card in mine.Take(visible))
            {
                allowed.Add(card.ToString());
            }
        }

        int? previousDeal = update.View.LastCompletedTrick?.DealNumber;
        foreach (var played in eventsUpToUpdate.OfType<CardPlayed>()
                     .Where(e => e.Deal == deal || e.Deal == previousDeal))
        {
            allowed.Add(played.Card.ToString());
        }

        var json = JsonSerializer.Serialize(update, WireJson.Options);
        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement, allowed, problems, update.Version);
        return problems;
    }

    public static IReadOnlyList<string> CardsIn(TableUpdateDto update)
    {
        var json = JsonSerializer.Serialize(update, WireJson.Options);
        using var doc = JsonDocument.Parse(json);
        var cards = new List<string>();
        Collect(doc.RootElement, cards);
        return cards;
    }

    private static void Collect(JsonElement element, List<string> cards)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    Collect(prop.Value, cards);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, cards);
                }

                break;
            case JsonValueKind.String:
                var text = element.GetString()!;
                if (CardToken.IsMatch(text))
                {
                    cards.Add(text);
                }

                break;
        }
    }

    private static void Walk(JsonElement element, HashSet<string> allowed, List<string> problems, int version)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (ForbiddenKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        problems.Add($"v{version}: forbidden field [{prop.Name}]");
                    }

                    Walk(prop.Value, allowed, problems, version);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, allowed, problems, version);
                }

                break;
            case JsonValueKind.String:
                var text = element.GetString()!;
                if (CardToken.IsMatch(text) && !allowed.Contains(text))
                {
                    problems.Add($"v{version}: card {text} is neither own nor public");
                }

                break;
        }
    }
}
