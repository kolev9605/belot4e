import { createContext, useContext, type ReactNode } from "react";
import { pacingFromEnv, type PacingConfig } from "../theme/motion";

const PacingContext = createContext<PacingConfig | null>(null);

export function PacingProvider({ value, children }: { value?: PacingConfig; children: ReactNode }) {
  return <PacingContext.Provider value={value ?? pacingFromEnv()}>{children}</PacingContext.Provider>;
}

export function usePacing(): PacingConfig {
  return useContext(PacingContext) ?? pacingFromEnv();
}
