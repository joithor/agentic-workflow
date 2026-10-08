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
  "SND-CLI-002": { summary: "Invalid arguments for this command.", fix: "Run `sindri help` and check the command's flags." },
  "SND-ITEM-404": { summary: "No such item in the ledger.", fix: "Run `sindri observe` to list items." },
  "SND-PROFILE-001": { summary: "The profile is invalid.", fix: "Run `sindri profile validate` and fix each listed key." },
  "SND-PROFILE-002": { summary: "No profile was found.", fix: "Run `sindri profile init`, pass --profile <dir>, or set AW_PROFILE_DIR." },
  "SND-PROFILE-003": { summary: "No such profile key.", fix: "See docs/sindri/profile.md for the keys." },
  "SND-PROFILE-004": { summary: "No such repo in the profile.", fix: "Use a name listed under repos in profile.yaml." },
  "SND-PROFILE-005": { summary: "The profile was written for a newer sindri.", fix: "Upgrade sindri (`scripts/install-sindri.sh` from the latest main)." },
  "SND-PROFILE-006": { summary: "The hash doesn't match the current profile.", fix: "Run `sindri profile approve` to see the current hash and diff, then approve that hash." },
  "SND-PROFILE-007": { summary: "A profile already exists there.", fix: "Edit it, or pass --force to overwrite it." },
  "SND-PROFILE-008": { summary: "This repo has no plan files for --ring0.", fix: "Run --ring0 from a repo with docs/superpowers/plans, or run `sindri profile init` without it." },
  "SND-PROFILE-009": { summary: "Not inside a git repo.", fix: "cd into the repo first." },
  "SND-PROFILE-010": { summary: "Approving a profile needs an interactive terminal.", fix: "run `sindri profile approve <hash>` yourself, in a terminal" },
  "SND-PROFILE-011": { summary: "The approval was not confirmed.", fix: "rerun and type the first 6 characters of the hash" },
  "SND-LOCK-001": { summary: "Another sindri run holds the lock.", fix: "wait a moment and rerun; `sindri doctor` shows the holder" },
  "SND-SCRUB-001": { summary: "A profile scrub pattern does not compile.", fix: "Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`." },
  "SND-SCRUB-002": { summary: "Staged changes contain likely secrets.", fix: "remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why" },
  "SND-SCRUB-003": { summary: "A different pre-commit hook is already installed.", fix: "Add `sindri scrub --staged || exit 1` to that hook by hand." },
  "SND-SCRUB-004": { summary: "`scrub --staged` or `--install-pre-commit` ran outside a git repo.", fix: "cd into the repo first, or pass --repo PATH to --install-pre-commit." },
  "SND-SCRUB-005": { summary: "`git diff --cached` failed, so `scrub --staged` could not scan the staged changes.", fix: "Fix the git error shown in the details, then retry the commit." },
  "SND-LEDGER-001": { summary: "The ledger was written by a newer sindri.", fix: "Upgrade sindri (`scripts/install-sindri.sh` from the latest main), then rerun." },
  "SND-LOCK-003": { summary: "This run's fencing epoch is stale; another run took over.", fix: "Nothing to do; the newer run continues. Check `sindri doctor` if this repeats." },
  "SND-TRACKER-001": { summary: "The tracker's source is missing or unreadable (for plan-file: the repo path or plan dir).", fix: "Restore that directory, or fix `path` in repos/<name>.yaml and run `sindri profile approve`." },
  "SND-TRACKER-002": { summary: "The tracker's source holds no items to read (for plan-file: no plan file matches tracker.include).", fix: "Fix tracker.include in profile.yaml (or restore the plan files), then run `sindri profile approve`." },
  "SND-TRACKER-003": { summary: "A plan file is too big to read (over 2 MiB).", fix: "Split the plan file into smaller plans." },
  "SND-TRACKER-404": { summary: "The tracker has no such item.", fix: "Check the id with `sindri observe`." },
  "SND-TRACKER-405": { summary: "This tracker can't write.", fix: "The plan-file tracker is read-only; edit the plan file." },
  "SND-CLI-900": { summary: "Unexpected internal error (a bug).", fix: "rerun with SINDRI_DEBUG=1 and report the output" },
} as const satisfies Record<string, ErrorDef>;

export type ErrorCode = keyof typeof ERRORS;

export class SindriError extends Error {
  readonly fix?: string;
  readonly details: string[];
  // 1 when the human can fix it and rerun (spec §10.3 "attention"); default 2.
  readonly exitCode?: 1 | 2;
  constructor(
    readonly code: ErrorCode,
    message: string,
    more: { fix?: string; details?: string[]; exitCode?: 1 | 2 } = {},
  ) {
    super(message);
    this.name = "SindriError";
    this.fix = more.fix;
    this.details = more.details ?? [];
    this.exitCode = more.exitCode;
  }
}
