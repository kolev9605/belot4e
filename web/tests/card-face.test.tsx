import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CardFace, HandCard, TableCard } from "../src/components/Card";
import { LanguageProvider } from "../src/i18n/LanguageProvider";

function renderInLanguage(node: React.ReactNode) {
  window.localStorage.setItem("belote.language", "en");
  return render(<LanguageProvider>{node}</LanguageProvider>);
}

describe("card face (US4)", () => {
  it("shows rank and a suit glyph, with the accessible name carried by the card element", () => {
    renderInLanguage(<TableCard card="10H" />);
    expect(screen.getByRole("img", { name: "Ten of Hearts" })).toBeInTheDocument();
    const face = screen.getByTestId("card-face");
    expect(face).toHaveTextContent("10");
    expect(face).toHaveTextContent("♥");
  });

  it("tells suits apart by glyph shape as well as colour", () => {
    const { container } = renderInLanguage(
      <>
        <CardFace card="AH" />
        <CardFace card="AD" />
        <CardFace card="AS" />
        <CardFace card="AC" />
      </>,
    );
    const glyphs = Array.from(container.querySelectorAll("[data-testid=card-face]")).map((el) =>
      /[♠♥♦♣]/.exec(el.textContent ?? "")?.[0],
    );
    expect(new Set(glyphs).size).toBe(4);
  });

  it("keeps the playable and not-playable wording on hand cards", () => {
    renderInLanguage(
      <>
        <HandCard card="QH" playable enabled onPlay={() => undefined} />
        <HandCard card="7C" playable={false} enabled onPlay={() => undefined} />
      </>,
    );
    expect(screen.getByRole("button", { name: /Queen of Hearts/ })).toBeEnabled();
    const blocked = screen.getByRole("button", { name: /Seven of Clubs/ });
    expect(blocked).toBeDisabled();
    expect(blocked).toHaveTextContent("not playable");
  });
});
