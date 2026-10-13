import { rankName, translate, type Language } from "../i18n";
import type { AnnouncementSpec, BidEntry, ContractKind, IntentDto, PublicAnnouncement, Seat, Team } from "../protocol/types";

export function seatName(language: Language, seat: Seat): string {
  return translate(language, `seat.${seat}` as never);
}

export function teamName(language: Language, team: Team): string {
  return translate(language, `team.${team}` as never);
}

export function contractName(language: Language, contract: ContractKind | string): string {
  return translate(language, `contract.${contract}` as never);
}

function suitName(language: Language, suit: string | null): string {
  return suit ? translate(language, `suit.${suit}` as never) : "";
}

export function announcementSpecLabel(language: Language, spec: AnnouncementSpec): string {
  if (spec.kind === "carre") {
    return translate(language, "action.carre", { rank: rankName(language, spec.rank) });
  }
  return translate(language, "action.sequence", {
    length: spec.length,
    suit: suitName(language, spec.suit),
    rank: rankName(language, spec.rank),
  });
}

/** Button text for one of the server's legal bid/announce actions. */
export function actionLabel(language: Language, action: IntentDto): string {
  switch (action.kind) {
    case "pass":
      return translate(language, "action.pass");
    case "bid":
      return translate(language, "action.bid", { contract: contractName(language, action.contract ?? "") });
    case "contra":
      return translate(language, "action.contra");
    case "recontra":
      return translate(language, "action.recontra");
    case "announce":
      return action.announcement ? announcementSpecLabel(language, action.announcement) : "";
    default:
      return "";
  }
}

export function historyLabel(language: Language, entry: BidEntry): string {
  const seat = seatName(language, entry.seat);
  switch (entry.action) {
    case "pass":
      return translate(language, "history.pass", { seat });
    case "bid":
      return translate(language, "history.bid", { seat, contract: contractName(language, entry.contract ?? "") });
    case "contra":
      return translate(language, "history.contra", { seat });
    case "recontra":
      return translate(language, "history.recontra", { seat });
    default:
      return `${seat}: ${entry.action}`;
  }
}

/** One call as shown inside its seat box (the seat is implied by where it appears). */
export function callLabel(language: Language, entry: BidEntry): string {
  switch (entry.action) {
    case "pass":
      return translate(language, "call.pass");
    case "bid":
      return translate(language, "call.bid", { contract: contractName(language, entry.contract ?? "") });
    case "contra":
      return translate(language, "call.contra");
    case "recontra":
      return translate(language, "call.recontra");
    default:
      return entry.action;
  }
}

export function publicAnnouncementLabel(language: Language, a: PublicAnnouncement): string {
  const seat = seatName(language, a.seat);
  if (a.revealed) {
    return a.revealed.kind === "carre"
      ? translate(language, "announced.carreRevealed", { seat, rank: rankName(language, a.revealed.rank) })
      : translate(language, "announced.sequenceRevealed", {
          seat,
          length: a.revealed.length,
          suit: suitName(language, a.revealed.suit),
          rank: rankName(language, a.revealed.rank),
        });
  }
  return a.kind === "carre"
    ? translate(language, "announced.carre", { seat })
    : translate(language, "announced.sequence", { seat, length: a.length });
}
