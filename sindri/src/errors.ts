// Stable error codes: SND-<AREA>-<NNN>. Every code used in src/ must be
// registered here, and every registered code must be used (tests/errors.test.ts).
// docs/sindri/errors.md is generated from this table (npm run gen).
export interface ErrorDef {
  readonly summary: string;
  readonly fix: string;
}

// `fix` is a command whenever one exists (spec §10.3); a SindriError may carry a
// more specific fix for one occurrence.
export const ERRORS = {
  "SND-CLI-001": { summary: "Unknown command.", fix: "sindri help" },
  "SND-SCRUB-001": { summary: "A profile scrub pattern does not compile.", fix: "Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`." },
  "SND-LEDGER-001": { summary: "The ledger was written by a newer sindri.", fix: "Upgrade sindri (`scripts/install-sindri.sh` from the latest main), then rerun." },
  "SND-LOCK-003": { summary: "This run's fencing epoch is stale; another run took over.", fix: "Nothing to do; the newer run continues. Check `sindri doctor` if this repeats." },
  "SND-CLI-900": { summary: "Unexpected internal error (a bug).", fix: "rerun with SINDRI_DEBUG=1 and report the output" },
} as const satisfies Record<string, ErrorDef>;

export type ErrorCode = keyof typeof ERRORS;

export class SindriError extends Error {
  readonly fix?: string;
  readonly details: string[];
  constructor(
    readonly code: ErrorCode,
    message: string,
    more: { fix?: string; details?: string[] } = {},
  ) {
    super(message);
    this.name = "SindriError";
    this.fix = more.fix;
    this.details = more.details ?? [];
  }
}
