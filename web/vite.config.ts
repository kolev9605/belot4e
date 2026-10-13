/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const server = process.env.BELOTE_SERVER ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      "/table": { target: server, ws: true, changeOrigin: true },
    },
  },
  test: {
    environment: "jsdom",
    // Unit tests assert on immediately-settled state; pacing tests pass an explicit config.
    env: { VITE_PACING: "instant" },
    globals: true,
    setupFiles: ["./tests/setup.ts"],
    include: ["tests/**/*.test.{ts,tsx}"],
  },
});
