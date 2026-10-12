using System;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Table;

/// <summary>Turns a wire <see cref="IntentDto"/> into a seat and action; never throws on bad input.</summary>
public static class IntentParser
{
    public static bool TryParse(IntentDto? dto, out Seat seat, out PlayerAction action, out Rejection? rejection)
    {
        seat = default;
        action = null!;
        rejection = null;

        if (dto is null || string.IsNullOrEmpty(dto.Kind))
        {
            rejection = Rejection.Malformed("The intent has no kind.");
            return false;
        }

        if (!TryParseSeat(dto.Seat, out seat))
        {
            rejection = Rejection.Malformed($"Unknown or missing seat [{dto.Seat}].");
            return false;
        }

        switch (dto.Kind)
        {
            case "pass":
                action = new PassAction();
                return true;
            case "contra":
                action = new ContraAction();
                return true;
            case "recontra":
                action = new RecontraAction();
                return true;
            case "bid":
                if (!TryParseContract(dto.Contract, out var kind))
                {
                    rejection = Rejection.Malformed($"Unknown or missing contract [{dto.Contract}].");
                    return false;
                }

                action = new BidAction(kind);
                return true;
            case "play":
                if (dto.Card is null || !Card.TryParse(dto.Card, out var card) || card.ToString() != dto.Card)
                {
                    rejection = Rejection.Malformed($"Unknown or missing card [{dto.Card}].");
                    return false;
                }

                action = new PlayCardAction(card);
                return true;
            case "announce":
                if (!TryParseSpec(dto.Announcement, out var spec))
                {
                    rejection = Rejection.Malformed("Unknown or malformed announcement.");
                    return false;
                }

                action = new AnnounceAction(spec);
                return true;
            default:
                rejection = Rejection.Malformed($"Unknown intent kind [{dto.Kind}].");
                return false;
        }
    }

    public static bool TryParseSeat(string? text, out Seat seat)
    {
        seat = default;
        if (text is null)
        {
            return false;
        }

        try
        {
            seat = JsonNames.ParseSeat(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseContract(string? text, out ContractKind kind)
    {
        kind = default;
        if (text is null)
        {
            return false;
        }

        try
        {
            kind = JsonNames.ParseKind(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseSpec(AnnouncementSpecDto? dto, out AnnouncementSpec spec)
    {
        spec = default;
        if (dto?.Rank is null || dto.Length < 1 || dto.Length > 8
            || !Card.TryParse(dto.Rank + "C", out var rankCard) || Card.RankText(rankCard.Rank) != dto.Rank)
        {
            return false;
        }

        if (dto.Kind == "carre")
        {
            spec = AnnouncementSpec.Carre(rankCard.Rank);
            return dto.Length == 4;
        }

        if (dto.Kind == "sequence" && dto.Suit is not null)
        {
            try
            {
                var suit = JsonNames.ParseSuit(dto.Suit);
                spec = AnnouncementSpec.Sequence(suit, rankCard.Rank, dto.Length);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return false;
    }
}
