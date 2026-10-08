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
