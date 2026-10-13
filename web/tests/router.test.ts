import { describe, expect, it } from "vitest";
import { parseRoomLink, roomPath, routeFromPath } from "../src/routing/router";

const ID = "3f2b8c1e-9a4d-4e7b-8c55-0d1f2a3b4c5d";

describe("parseRoomLink", () => {
  it("accepts a bare guid in any common spelling", () => {
    expect(parseRoomLink(ID)).toBe(ID);
    expect(parseRoomLink(ID.toUpperCase())).toBe(ID);
    expect(parseRoomLink(ID.replace(/-/g, ""))).toBe(ID);
    expect(parseRoomLink(`{${ID}}`)).toBe(ID);
    expect(parseRoomLink(`  ${ID}  `)).toBe(ID);
  });

  it("extracts the guid from a pasted link", () => {
    expect(parseRoomLink(`https://belote.example/room/${ID}`)).toBe(ID);
    expect(parseRoomLink(`belote.example/room/${ID}?utm=1#x`)).toBe(ID);
    expect(parseRoomLink(`http://localhost:5173/room/${ID}/`)).toBe(ID);
  });

  it("rejects anything that is not a room", () => {
    expect(parseRoomLink("")).toBeNull();
    expect(parseRoomLink("   ")).toBeNull();
    expect(parseRoomLink("hello")).toBeNull();
    expect(parseRoomLink(ID.slice(0, 30))).toBeNull();
    expect(parseRoomLink(`${ID}-extra`)).toBeNull();
    expect(parseRoomLink("https://example.com/other")).toBeNull();
    expect(parseRoomLink(null)).toBeNull();
  });
});

describe("routeFromPath", () => {
  it("maps the menu and room paths", () => {
    expect(routeFromPath("/")).toEqual({ kind: "menu" });
    expect(routeFromPath(roomPath(ID))).toEqual({ kind: "room", roomId: ID });
    expect(routeFromPath(`/room/${ID}/`)).toEqual({ kind: "room", roomId: ID });
    expect(routeFromPath("/room/nope")).toEqual({ kind: "menu" });
    expect(routeFromPath("/elsewhere")).toEqual({ kind: "menu" });
  });
});
