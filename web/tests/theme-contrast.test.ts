import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const root = join(__dirname, "..");
const css = readFileSync(join(root, "src/theme/tokens.css"), "utf8");

function token(name: string): string {
  const match = new RegExp(`--color-${name}:\\s*(#[0-9a-fA-F]{6});`).exec(css);
  if (!match) throw new Error(`Missing colour token --color-${name}`);
  return match[1]!;
}

function luminance(hex: string): number {
  const channel = (i: number) => {
    const c = parseInt(hex.slice(1 + i * 2, 3 + i * 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(0) + 0.7152 * channel(1) + 0.0722 * channel(2);
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(token(a)), luminance(token(b))].sort((x, y) => y - x) as [number, number];
  return (hi + 0.05) / (lo + 0.05);
}

describe("theme contrast (WCAG AA)", () => {
  const text: [string, string][] = [
    ["ink", "page"],
    ["ink", "surface"],
    ["ink", "raised"],
    ["muted", "page"],
    ["muted", "surface"],
    ["muted", "raised"],
    ["accent", "page"],
    ["accent", "surface"],
    ["on-accent", "accent"],
    ["face-ink", "face"],
    ["face-red", "face"],
    ["danger", "surface"],
    ["warn", "surface"],
  ];
  it.each(text)("text %s on %s is at least 4.5:1", (fg, bg) => {
    expect(contrast(fg, bg)).toBeGreaterThanOrEqual(4.5);
  });

  const ui: [string, string][] = [
    ["focus", "page"],
    ["focus", "felt"],
    ["accent", "felt"],
    ["accent", "surface"],
  ];
  it.each(ui)("interface colour %s against %s is at least 3:1", (fg, bg) => {
    expect(contrast(fg, bg)).toBeGreaterThanOrEqual(3);
  });
});

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? sourceFiles(path) : /\.tsx$/.test(name) ? [path] : [];
  });
}

describe("dark theme", () => {
  it("leaves no light-theme utility classes behind", () => {
    const banned = /\b(bg-white|bg-slate-\d+|text-slate-\d+|border-slate-\d+|bg-yellow-\d+|bg-blue-\d+|bg-amber-\d+|bg-red-\d+|text-red-\d+|border-red-\d+)\b/;
    const offenders = sourceFiles(join(root, "src")).filter((file) => banned.test(readFileSync(file, "utf8")));
    expect(offenders).toEqual([]);
  });
});
