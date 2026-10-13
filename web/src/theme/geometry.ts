import type { Seat, Team } from "../protocol/types";

/** Unit vectors from the table centre towards each seat (x right, y down). */
export const SEAT_POSITIONS: Record<Seat, { x: number; y: number }> = {
  south: { x: 0, y: 1 },
  north: { x: 0, y: -1 },
  west: { x: -1, y: 0 },
  east: { x: 1, y: 0 },
};

/** Pixel offset of a seat from the table centre, used as the origin/destination of card travel. */
export function offsetFrom(seat: Seat, scale = 160): { x: number; y: number } {
  const p = SEAT_POSITIONS[seat];
  return { x: p.x * scale, y: p.y * scale };
}

export function teamOf(seat: Seat): Team {
  return seat === "north" || seat === "south" ? "northSouth" : "eastWest";
}

/** The person always sits south, so their team is northSouth. */
export function isMyTeam(seat: Seat): boolean {
  return teamOf(seat) === "northSouth";
}

export const SEATS_CLOCKWISE: Seat[] = ["south", "west", "north", "east"];

/** Seat order as the game moves: after north comes east, then south, then west. */
export const SEAT_ORDER: Seat[] = ["north", "east", "south", "west"];

/** Position (0..3) in which a seat receives each round of a deal: the seat after the dealer first. */
export function dealOrder(seat: Seat, dealer: Seat): number {
  const d = (SEAT_ORDER.indexOf(seat) - SEAT_ORDER.indexOf(dealer) + 4) % 4;
  return (d + 3) % 4;
}

/** Pixel vector from `to` to `from`, i.e. where a card must start relative to where it ends. */
export function travelFrom(from: Seat, to: Seat, scale = 120): { x: number; y: number } {
  const a = SEAT_POSITIONS[from];
  const b = SEAT_POSITIONS[to];
  return { x: (a.x - b.x) * scale, y: (a.y - b.y) * scale };
}
