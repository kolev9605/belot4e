import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HandResultDialog } from "../src/components/HandResultDialog";
import { LanguageProvider } from "../src/i18n/LanguageProvider";
import { makeSummary } from "./fixtures";

function renderDialog(
  props: Partial<Parameters<typeof HandResultDialog>[0]> = {},
  language: "en" | "bg" = "en",
) {
  window.localStorage.setItem("belote.language", language);
  const onDismiss = vi.fn();
  const onPlayAgain = vi.fn();
  render(
    <LanguageProvider>
      <HandResultDialog summary={makeSummary()} onDismiss={onDismiss} {...props} onPlayAgain={props.onPlayAgain ?? undefined} />
    </LanguageProvider>,
  );
  return { onDismiss, onPlayAgain };
}

beforeEach(() => {
  window.localStorage.clear();
});

describe("hand result pop-up (spec 006 US5)", () => {
  it("shows two separated team sections, each a single row of that team's cards", () => {
    renderDialog();
    const ns = screen.getByTestId("hand-result-northSouth");
    const ew = screen.getByTestId("hand-result-eastWest");
    expect(within(ns).getByTestId("won-cards-northSouth").children).toHaveLength(8);
    expect(within(ew).getByTestId("won-cards-eastWest").children).toHaveLength(24);
    expect(within(ns).getByText("Cards won: 8")).toBeInTheDocument();
    // Nothing is grouped per trick.
    expect(screen.queryByText(/trick \d/i)).toBeNull();
  });

  it("marks the person's team and labels the other as opponents", () => {
    renderDialog();
    expect(within(screen.getByTestId("hand-result-northSouth")).getByText("Your team")).toBeInTheDocument();
    expect(within(screen.getByTestId("hand-result-eastWest")).getByText("Opponents")).toBeInTheDocument();
  });

  it("shows each team's breakdown, hand score and game total, labelled distinctly", () => {
    renderDialog();
    const ew = within(screen.getByTestId("hand-result-eastWest"));
    expect(ew.getByText("Card points").nextSibling).toHaveTextContent("100");
    expect(ew.getByText("Last trick bonus").nextSibling).toHaveTextContent("10");
    expect(ew.getByText("Announcements").nextSibling).toHaveTextContent("20");
    expect(ew.getByText("Belote").nextSibling).toHaveTextContent("20");
    expect(ew.getByText("Points in the hand").nextSibling).toHaveTextContent("150");
    expect(ew.getByText("Game points this hand").nextSibling).toHaveTextContent("15");
    expect(ew.getByText("Game total").nextSibling).toHaveTextContent("66");
    const ns = within(screen.getByTestId("hand-result-northSouth"));
    expect(ns.getByText("Game total").nextSibling).toHaveTextContent("39");
    // Zero-valued parts are left out rather than shown as noise.
    expect(ns.queryByText("Belote")).toBeNull();
  });

  it("states whether the contract was made or failed", () => {
    renderDialog();
    expect(screen.getByTestId("hand-result-outcome")).toHaveTextContent("Contract made");
  });

  it("continues the game when dismissed with the button or Escape", async () => {
    const { onDismiss } = renderDialog();
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(onDismiss).toHaveBeenCalledTimes(1);
    const dialog = screen.getByTestId("hand-result");
    await act(async () => {
      dialog.dispatchEvent(new Event("cancel", { cancelable: true }));
    });
    expect(onDismiss).toHaveBeenCalledTimes(2);
  });

  it("names the winner and offers a new game when the game is over", async () => {
    const onPlayAgain = vi.fn();
    const { onDismiss } = renderDialog({ summary: makeSummary({ winner: "eastWest" }), onPlayAgain });
    expect(screen.getByTestId("hand-result-winner")).toHaveTextContent("East–West win");
    expect(screen.getByTestId("hand-result-winner")).toHaveTextContent("Your opponents won.");
    expect(screen.queryByRole("button", { name: "Continue" })).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Play again" }));
    expect(onPlayAgain).toHaveBeenCalledTimes(1);
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it("copes with an empty team row", () => {
    const base = makeSummary();
    renderDialog({
      summary: makeSummary({
        teams: { northSouth: { ...base.teams.northSouth, wonCards: [] }, eastWest: base.teams.eastWest },
      }),
    });
    expect(screen.getByTestId("hand-result-northSouth")).toHaveTextContent("No cards won");
    expect(screen.queryByTestId("won-cards-northSouth")).toBeNull();
  });

  it("localizes the text", () => {
    renderDialog({}, "bg");
    expect(screen.getByRole("button", { name: "Продължи" })).toBeInTheDocument();
    expect(within(screen.getByTestId("hand-result-northSouth")).getByText("Вашият отбор")).toBeInTheDocument();
  });

  it("is a labelled dialog and gives initial focus to the primary action", () => {
    renderDialog();
    expect(screen.getByRole("dialog", { name: /Result of hand 1/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Continue" })).toHaveFocus();
  });
});
