# Sindri Plan 1: Measure + Standalone Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Sindri rollout step 0 (measurement baselines) and the two standalone step-1 fixes that later plans depend on: done-gate false positives, and a judge `--providers` allowlist.

**Architecture:** Measurement is a new `scorer audit` subcommand. It reuses scorer's transcript discovery and `classifyUserText` to extract human turns, count recurring-direction patterns, and compute per-item token usage. It writes JSONL, JSON and a Markdown baseline. The done-gate change replaces a bare-word regex with a small claim detector that ignores questions, negations, tables and code. The judge change adds a chain filter, so callers (later, Sindri) can restrict which providers a question may use.

**Tech Stack:** TypeScript 5.7 strict, ESM (Node16 resolution), Node >= 20, Vitest, Zod 3; bash + jq for hooks.

**Spec:** `docs/superpowers/specs/2026-10-07-sindri-design.md`. This plan implements §13 step 0, plus these step-1 items: "done-gate fix" (§5.3 coexistence, §7.6/§7.7 first self-proposal) and "judge `--providers` flag" (§6.1 Providers and egress, M1/M2).

Later plans cover the rest of step 1:
- Plan 2: Sindri core package (profile, ledger, lock and fencing, scrubber, CLI skeleton)
- Plan 3: host code index
- Plan 4: scoping harness with `--backtest`
- Plan 5: ported pstack skills, artifact registry and offline self-evolution

## Global Constraints

- Node >= 20, TypeScript 5.7 strict mode, ESM with Node16 module resolution (AGENTS.md Tech Stack).
- No `any` types outside Fastify integration boundaries. No `/* v8 ignore */` annotations (AGENTS.md Merge Gate 5–6).
- Tests: Vitest for TS packages; bash test scripts for hooks, run with `bash <file>` (AGENTS.md Commands).
- One heavy job at a time: run `npm test` / typecheck once per commit, not per edit (global CLAUDE.md).
- Core stays generic. No workplace-specific names, labels, ticket prefixes or hosts in code or defaults. Use generic defaults (item pattern `[A-Z][A-Z0-9]{1,9}-\d+`) that a profile overrides (spec §4.3, memory "generic core").
- No human hand-labeling. Measurements use deterministic patterns and outcomes only (spec invariant 9).
- Commit format: `type: short description`, atomic commits (AGENTS.md Commit Conventions).

## Review Focus

1. **Transcripts with malformed or partial lines.** A truncated last line, non-JSON lines, or unknown record types must be skipped, not crash the audit. Pinned in Task 3.
2. **Done claims inside quotes, tables or code fences.** "`ready for review`" inside a markdown table or a fenced block is not a claim. Pinned in Task 1.
3. **Negated or hedged claims.** "nothing is finished", "not done yet", "isn't complete" must not count as claims. Pinned in Task 1.
4. **`--providers` naming an unknown provider or listing none.** It must exit with a usage error, not silently fall back to the full chain. Pinned in Task 2.
5. **Huge transcript corpora.** The audit streams files line by line and never loads a whole file into memory. Pinned in Task 3 (large-file test).

---

## File Structure

| File | Responsibility |
|---|---|
| `config/hooks/done-gate.sh` (modify) | Replace the bare-word claim regex with an `is_done_claim` function |
| `config/lib/tests/done-gate.test.sh` (modify) | New claim-detection tests |
| `judge/src/chain.ts` (modify) | Add `restrictChain(spec, allowed)` |
| `judge/src/providers-flag.ts` (create) | Parse `--providers` / `AW_JUDGE_PROVIDERS` into a validated allowlist |
| `judge/src/cli.ts` (modify) | Apply the allowlist to the runtime chain; strip the flag from argv |
| `judge/tests/chain.test.ts`, `judge/tests/providers-flag.test.ts` | Tests |
| `scorer/src/audit/human-turns.ts` (create) | Stream one transcript file → human-turn records |
| `scorer/src/audit/patterns.ts` (create) | Named regex pattern sets + counting |
| `scorer/src/audit/usage.ts` (create) | Per-session token totals, grouped by work-item id |
| `scorer/src/audit/items.ts` (create) | Optional items file → XS/clear/trusted share |
| `scorer/src/audit/run-audit.ts` (create) | Orchestrate: discover → extract → write outputs |
| `scorer/src/args.ts`, `scorer/src/cli.ts` (modify) | `audit` command and its flags |
| `scorer/tests/audit-*.test.ts` | Tests |
| `scripts/transcript-audit/README.md` (create) | How to run the audit; output schema |
| `AGENTS.md` (modify) | Command list line for `scorer audit` |

---

### Task 1: done-gate claim detection (fixes false positives)

**Files:**
- Modify: `config/hooks/done-gate.sh:57` (the `if ! printf '%s' "$CLAIM_TEXT" | grep -qiE '\b(done|complete|finished|ready for review|merged|shipped)\b'` line)
- Test: `config/lib/tests/done-gate.test.sh`

**Interfaces:**
- Produces: bash function `is_done_claim <text>`. It returns 0 when the text claims completion and 1 otherwise. The hook calls it where the old regex was.

- [ ] **Step 1: Write the failing tests**

Append these functions to `config/lib/tests/done-gate.test.sh`, before the list of test invocations at the bottom of the file, and add their names to that list:

```bash
claim_rc() {
  # Runs the hook on one assistant message with a fake judge (no brief), prints the exit code.
  local text="$1" bin transcript rc
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "$text"
  set +e
  PATH="$bin:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" > /dev/null 2>&1
  rc=$?
  set -e
  echo "$rc"
}

test_question_with_claim_word_is_not_a_claim() {
  [ "$(claim_rc 'Should I mark it ready for review now?')" -eq 0 ] || { echo "FAIL: question treated as claim"; exit 1; }
  echo "PASS: test_question_with_claim_word_is_not_a_claim"
}

test_negated_claim_is_not_a_claim() {
  [ "$(claim_rc "I'm not claiming anything is finished; four reviews are still running.")" -eq 0 ] || { echo "FAIL: negation treated as claim"; exit 1; }
  [ "$(claim_rc 'Nothing is done yet.')" -eq 0 ] || { echo "FAIL: 'Nothing is done yet' treated as claim"; exit 1; }
  [ "$(claim_rc "It isn't complete.")" -eq 0 ] || { echo "FAIL: isn't complete treated as claim"; exit 1; }
  echo "PASS: test_negated_claim_is_not_a_claim"
}

test_claim_word_in_table_or_code_is_not_a_claim() {
  local table code
  table=$'| Step | Status |\n|---|---|\n| undraft | ready for review |'
  code=$'Example:\n```\necho done\n```\nWhich option do you want?'
  [ "$(claim_rc "$table")" -eq 0 ] || { echo "FAIL: table cell treated as claim"; exit 1; }
  [ "$(claim_rc "$code")" -eq 0 ] || { echo "FAIL: code fence treated as claim"; exit 1; }
  echo "PASS: test_claim_word_in_table_or_code_is_not_a_claim"
}

test_real_claims_without_evidence_still_block() {
  [ "$(claim_rc 'Done. The refactor is complete.')" -eq 2 ] || { echo "FAIL: real claim not blocked"; exit 1; }
  [ "$(claim_rc "I've finished the migration.")" -eq 2 ] || { echo "FAIL: I've finished not blocked"; exit 1; }
  [ "$(claim_rc 'The PR is ready for review.')" -eq 2 ] || { echo "FAIL: 'is ready for review' not blocked"; exit 1; }
  echo "PASS: test_real_claims_without_evidence_still_block"
}

test_real_claim_with_evidence_passes() {
  [ "$(claim_rc 'Done — ran npm test and all 42 tests passed.')" -eq 0 ] || { echo "FAIL: evidenced claim blocked"; exit 1; }
  echo "PASS: test_real_claim_with_evidence_passes"
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash config/lib/tests/done-gate.test.sh`

Expected: FAIL at `test_question_with_claim_word_is_not_a_claim` (the current regex matches "ready for review" inside the question, so the hook exits 2).

- [ ] **Step 3: Implement `is_done_claim`**

In `config/hooks/done-gate.sh`, define the function before the `SESSION_ID=` line:

```bash
# A done claim is an assertion of completion, not any occurrence of a claim word.
# Ignored: fenced code, table rows, lines ending in '?', and sentences whose claim
# word is negated (not/n't/nothing/no/yet). Deterministic: grep/sed only.
is_done_claim() {
  local text="$1" body
  body="$(printf '%s\n' "$text" \
    | awk 'BEGIN{f=0} /^[[:space:]]*```/{f=!f; next} !f' \
    | grep -v -E '^[[:space:]]*\|' \
    | grep -v -E '\?[[:space:]]*$' || true)"
  [ -n "$body" ] || return 1
  # Split into sentences, one per line (awk, not sed: BSD sed has no \n in replacements).
  body="$(printf '%s\n' "$body" | awk '{gsub(/[.!;:][[:space:]]+/, "&\n"); print}')"
  # Drop sentences with a negation anywhere before the claim word.
  body="$(printf '%s\n' "$body" | grep -v -iE "(\bnot\b|n't\b|\bnothing\b|\bno\b|\byet\b)" || true)"
  [ -n "$body" ] || return 1
  printf '%s\n' "$body" | grep -qiE \
    -e '^[[:space:]]*(done|finished|shipped|merged)\b' \
    -e "\b(is|are|it's|it is|now|all|everything('s| is)?)[[:space:]]+(now[[:space:]]+)?(done|complete|completed|finished|merged|shipped|ready for review)\b" \
    -e "\b(i|we)('ve| have)?[[:space:]]+(finished|completed|shipped|merged)\b"
}
```

Replace the old line:

```bash
if ! printf '%s' "$CLAIM_TEXT" | grep -qiE '\b(done|complete|finished|ready for review|merged|shipped)\b'; then
```

with:

```bash
if ! is_done_claim "$CLAIM_TEXT"; then
```

- [ ] **Step 4: Run all done-gate tests**

Run: `bash config/lib/tests/done-gate.test.sh && bash config/lib/tests/done-gate-annotate.test.sh`
Expected: every line `PASS: …`, exit 0. The existing tests (RF-2, RF-3, RF-4, the UI requirement tests) must still pass unchanged.

- [ ] **Step 5: Commit**

```bash
git add config/hooks/done-gate.sh config/lib/tests/done-gate.test.sh
git commit -m "fix: done-gate detects completion claims, not bare claim words (questions, negations, tables, code ignored)"
```

---

### Task 2: judge `--providers` allowlist

**Files:**
- Modify: `judge/src/chain.ts` (add `restrictChain`)
- Create: `judge/src/providers-flag.ts`
- Modify: `judge/src/config.ts:15` (`function isProviderName` → `export function isProviderName`)
- Modify: `judge/src/cli.ts:86` (`const chain = buildChain(...)`) and argv handling at `:95`
- Test: `judge/tests/chain.test.ts`, `judge/tests/providers-flag.test.ts`

**Interfaces:**
- Consumes: `ChainSpec`, `buildChain` from `judge/src/chain.ts`; `ProviderName` from `judge/src/types.ts`; `isProviderName` from `judge/src/config.ts:15`. It is module-private today; this task adds `export`.
- Produces:
  - `restrictChain(spec: ChainSpec, allowed: readonly ProviderName[]): ChainSpec`
  - `parseProvidersAllowlist(argv: readonly string[], env: NodeJS.ProcessEnv): { ok: true; allowed: ProviderName[] | null; argv: string[] } | { ok: false; error: string }`
- Later plans: Sindri calls `judge --providers jev,claude-cli <cmd> ...`.

- [ ] **Step 1: Write the failing tests**

Append to `judge/tests/chain.test.ts`:

```ts
describe("restrictChain", () => {
  it("keeps only allowed providers per class, always keeping rules as the last resort", async () => {
    const { buildChain, restrictChain } = await import("../src/chain.js");
    const chain = buildChain({ agentClis: ["claude-cli", "codex-cli"], jev: true });
    const restricted = restrictChain(chain, ["jev", "claude-cli"]);
    expect(restricted.classes.code).toEqual(["jev", "claude-cli", "rules"]);
    expect(restricted.classes.image).toEqual(["claude-cli", "rules"]);
  });

  it("leaves rules alone when the allowlist excludes every model provider", async () => {
    const { buildChain, restrictChain } = await import("../src/chain.js");
    const restricted = restrictChain(buildChain({ agentClis: ["codex-cli"], jev: true }), ["claude-cli"]);
    expect(restricted.classes.code).toEqual(["rules"]);
  });
});
```

Create `judge/tests/providers-flag.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { parseProvidersAllowlist } from "../src/providers-flag.js";

describe("parseProvidersAllowlist", () => {
  it("returns null allowlist and untouched argv when neither flag nor env is set", () => {
    expect(parseProvidersAllowlist(["node", "judge", "why", "d1"], {})).toEqual({ ok: true, allowed: null, argv: ["node", "judge", "why", "d1"] });
  });

  it("parses --providers and strips it from argv wherever it appears", () => {
    expect(parseProvidersAllowlist(["node", "judge", "--providers", "jev,claude-cli", "ask-check"], {})).toEqual({
      ok: true,
      allowed: ["jev", "claude-cli"],
      argv: ["node", "judge", "ask-check"],
    });
  });

  it("falls back to AW_JUDGE_PROVIDERS when the flag is absent", () => {
    expect(parseProvidersAllowlist(["node", "judge", "health"], { AW_JUDGE_PROVIDERS: "jev" })).toEqual({ ok: true, allowed: ["jev"], argv: ["node", "judge", "health"] });
  });

  it("rejects unknown provider names instead of falling back to the full chain", () => {
    const r = parseProvidersAllowlist(["node", "judge", "--providers", "jev,gpt5"], {});
    expect(r.ok).toBe(false);
  });

  it("rejects an empty list and a missing value", () => {
    expect(parseProvidersAllowlist(["node", "judge", "--providers", ""], {}).ok).toBe(false);
    expect(parseProvidersAllowlist(["node", "judge", "--providers"], {}).ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd judge && npx vitest run tests/chain.test.ts tests/providers-flag.test.ts`
Expected: FAIL. `restrictChain` is not exported, and `../src/providers-flag.js` can't be resolved.

- [ ] **Step 3: Implement**

Append to `judge/src/chain.ts`:

```ts
/**
 * Limit a chain to an allowlist of providers (spec §6.1: providers.allowed).
 * "rules" is local and deterministic, so it is always kept as the last resort.
 */
export function restrictChain(spec: ChainSpec, allowed: readonly ProviderName[]): ChainSpec {
  const keep = new Set<ProviderName>([...allowed, "rules"]);
  const classes: Partial<Record<ContentClass, readonly ProviderName[]>> = {};
  for (const [cls, names] of Object.entries(spec.classes) as [ContentClass, readonly ProviderName[]][]) {
    classes[cls] = names.filter((n) => keep.has(n));
  }
  return { classes };
}
```

Create `judge/src/providers-flag.ts`:

```ts
import { isProviderName } from "./config.js";
import type { ProviderName } from "./types.js";

export type AllowlistResult = { ok: true; allowed: ProviderName[] | null; argv: string[] } | { ok: false; error: string };

// Global flag, accepted anywhere in argv so it composes with every subcommand:
//   judge --providers jev,claude-cli <cmd> ...
// AW_JUDGE_PROVIDERS is the env equivalent (hooks can't always add flags).
export function parseProvidersAllowlist(argv: readonly string[], env: NodeJS.ProcessEnv): AllowlistResult {
  const i = argv.indexOf("--providers");
  let raw: string | undefined;
  let rest = [...argv];
  if (i !== -1) {
    raw = argv[i + 1];
    if (raw === undefined) return { ok: false, error: "--providers needs a comma-separated list" };
    rest = [...argv.slice(0, i), ...argv.slice(i + 2)];
  } else if (env.AW_JUDGE_PROVIDERS !== undefined) {
    raw = env.AW_JUDGE_PROVIDERS;
  } else {
    return { ok: true, allowed: null, argv: rest };
  }
  const names = raw.split(",").map((s) => s.trim()).filter((s) => s.length > 0);
  if (names.length === 0) return { ok: false, error: "--providers list is empty" };
  const bad = names.filter((n) => !isProviderName(n));
  if (bad.length > 0) return { ok: false, error: `unknown provider(s): ${bad.join(", ")}` };
  return { ok: true, allowed: [...new Set(names as ProviderName[])], argv: rest };
}
```

In `judge/src/cli.ts`, add `restrictChain` to the import from `./chain.js` and import `parseProvidersAllowlist` from `./providers-flag.js`. Replace:

```ts
const chain = buildChain({ agentClis, jev: config.providers?.jev ?? true });
```

with:

```ts
const allowlist = parseProvidersAllowlist(process.argv, process.env);
if (!allowlist.ok) {
  console.error(`judge: ${allowlist.error}`);
  process.exit(64);
}
const fullChain = buildChain({ agentClis, jev: config.providers?.jev ?? true });
const chain = allowlist.allowed === null ? fullChain : restrictChain(fullChain, allowlist.allowed);
```

Then replace `const [, , cmd, ...rest] = process.argv;` with:

```ts
const [, , cmd, ...rest] = allowlist.argv;
```

Leave the `isHookSort` line (`cli.ts:39`) unchanged. It runs before the chain is built, and `prompt-sort` is never combined with `--providers`.

In `judge/src/config.ts:15`, change `function isProviderName` to `export function isProviderName`.

- [ ] **Step 4: Run the judge suite and typecheck**

Run: `cd judge && npm run typecheck && npm test`
Expected: typecheck clean; all tests pass, including the 7 new ones.

- [ ] **Step 5: Commit**

```bash
git add judge/src/chain.ts judge/src/config.ts judge/src/providers-flag.ts judge/src/cli.ts judge/tests/chain.test.ts judge/tests/providers-flag.test.ts
git commit -m "feat: judge --providers allowlist (rules always kept as last resort)"
```

---

### Task 3: `scorer audit`: human-turn extraction and pattern counts

**Files:**
- Create: `scorer/src/audit/human-turns.ts`, `scorer/src/audit/patterns.ts`
- Test: `scorer/tests/audit-human-turns.test.ts`, `scorer/tests/audit-patterns.test.ts`

**Interfaces:**
- Consumes: `classifyUserText(raw: string): UserTextKind` from `scorer/src/transcript/classify.ts`.
- Produces:
  - `interface HumanTurn { project: string; session: string; ts: string; index: number; kind: "turn" | "command" | "interrupt"; text: string; skills: string[]; guardFiredBefore: boolean; compactedBefore: boolean; contextTokens: number; prevAssistantTail: string }`
  - `extractHumanTurns(file: { path: string; project: string; sessionId: string }): AsyncGenerator<HumanTurn>`
  - `const PATTERNS: Readonly<Record<PatternName, RegExp>>`
  - `type PatternName = "ship_recipe" | "ci_conflicts" | "push_only" | "evidence_env" | "found_defect" | "handoff" | "restate" | "rigor" | "scope_surface" | "dispatch"`
  - `countPatterns(turns: Iterable<HumanTurn>): Record<PatternName, { turns: number; sessions: number }>`

- [ ] **Step 1: Write the failing tests**

Create `scorer/tests/audit-human-turns.test.ts`:

```ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { extractHumanTurns } from "../src/audit/human-turns.js";

function writeJsonl(lines: unknown[], extra = ""): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "audit-"));
  const file = path.join(dir, "s1.jsonl");
  fs.writeFileSync(file, lines.map((l) => JSON.stringify(l)).join("\n") + "\n" + extra);
  return file;
}

async function collect(file: string) {
  const out = [];
  for await (const t of extractHumanTurns({ path: file, project: "p", sessionId: "s1" })) out.push(t);
  return out;
}

const user = (text: string, extra: Record<string, unknown> = {}) => ({ type: "user", timestamp: "2026-10-01T00:00:00Z", message: { content: text }, ...extra });
const assistant = (text: string, input = 1000, skill?: string) => ({
  type: "assistant",
  message: {
    usage: { input_tokens: input, cache_read_input_tokens: 0, cache_creation_input_tokens: 0 },
    content: [{ type: "text", text }, ...(skill ? [{ type: "tool_use", name: "Skill", input: { skill } }] : [])],
  },
});

describe("extractHumanTurns", () => {
  it("keeps typed human turns and drops machine text, meta, sidechain and tool results", async () => {
    const file = writeJsonl([
      user("<system-reminder>x</system-reminder>"),
      user("meta", { isMeta: true }),
      user("side", { isSidechain: true }),
      { type: "user", timestamp: "t", message: { content: [{ type: "tool_result", content: "ok" }] } },
      assistant("I can open the PR. Want me to?", 5000, "bugFixOrchestrator"),
      user("raise the PR as draft, run /review + /addressReview"),
    ]);
    const turns = await collect(file);
    expect(turns).toHaveLength(1);
    expect(turns[0]).toMatchObject({ kind: "turn", index: 1, skills: ["bugFixOrchestrator"], contextTokens: 5000, prevAssistantTail: "I can open the PR. Want me to?" });
  });

  it("records slash commands with their args and interrupts", async () => {
    const file = writeJsonl([
      user("<command-name>/bugFixOrchestrator</command-name><command-args>ABC-12</command-args>"),
      user("[Request interrupted by user]"),
    ]);
    const turns = await collect(file);
    expect(turns.map((t) => [t.kind, t.text])).toEqual([["command", "/bugFixOrchestrator ABC-12"], ["interrupt", "[Request interrupted by user]"]]);
    expect(turns[1].skills).toContain("bugFixOrchestrator");
  });

  it("marks guard-fired and compacted state for later turns", async () => {
    const file = writeJsonl([
      { type: "attachment", text: "context is now ~210000 tokens (past the 200000-token guard)" },
      user("This session is being continued from a previous conversation"),
      user("continue the plan"),
    ]);
    const [turn] = await collect(file);
    expect(turn).toMatchObject({ guardFiredBefore: true, compactedBefore: true, text: "continue the plan" });
  });

  it("skips malformed and truncated lines without throwing", async () => {
    const file = writeJsonl([user("first")], "{not json\n{\"type\":\"user\",\"message\":{\"content\":\"trunc");
    const turns = await collect(file);
    expect(turns.map((t) => t.text)).toEqual(["first"]);
  });

  it("streams a large file without loading it whole", async () => {
    const lines = Array.from({ length: 50_000 }, (_, i) => user(`turn ${i}`));
    const turns = await collect(writeJsonl(lines));
    expect(turns).toHaveLength(50_000);
  });
});
```

Create `scorer/tests/audit-patterns.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import type { HumanTurn } from "../src/audit/human-turns.js";
import { countPatterns, PATTERNS } from "../src/audit/patterns.js";

const turn = (text: string, session = "s1"): HumanTurn => ({
  project: "p", session, ts: "t", index: 1, kind: "turn", text, skills: [], guardFiredBefore: false, compactedBefore: false, contextTokens: 0, prevAssistantTail: "",
});

describe("PATTERNS", () => {
  it.each([
    ["ship_recipe", "raise the PR as draft, run /review + /addressReview and monitor bugbot"],
    ["push_only", "push"],
    ["evidence_env", "attach the screenshots to the linear issue"],
    ["found_defect", "[Image #3] this still wraps onto a second row"],
    ["handoff", "give me a handoff prompt for a new orchestrator"],
    ["restate", "The ticket description suggests the fix. why are we not doing it"],
    ["rigor", "refute or confirm WITH EVIDENCE"],
    ["dispatch", "fix this https://linear.app/acme/issue/ABC-12/thing"],
  ] as const)("%s matches a representative turn", (name, text) => {
    expect(PATTERNS[name].test(text)).toBe(true);
  });

  it("push_only does not match longer instructions", () => {
    expect(PATTERNS.push_only.test("push the fix and then rebase the stack")).toBe(false);
  });
});

describe("countPatterns", () => {
  it("counts turns and distinct sessions per pattern", () => {
    const counts = countPatterns([turn("push", "a"), turn("push", "a"), turn("push", "b"), turn("hello", "c")]);
    expect(counts.push_only).toEqual({ turns: 3, sessions: 2 });
    expect(counts.handoff).toEqual({ turns: 0, sessions: 0 });
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd scorer && npx vitest run tests/audit-human-turns.test.ts tests/audit-patterns.test.ts`
Expected: FAIL, with modules `../src/audit/human-turns.js` and `../src/audit/patterns.js` not found.

- [ ] **Step 3: Implement**

Create `scorer/src/audit/human-turns.ts`:

```ts
import fs from "node:fs";
import readline from "node:readline";

import { classifyUserText } from "../transcript/classify.js";

export interface HumanTurn {
  project: string;
  session: string;
  ts: string;
  index: number;
  kind: "turn" | "command" | "interrupt";
  text: string;
  skills: string[];
  guardFiredBefore: boolean;
  compactedBefore: boolean;
  contextTokens: number;
  prevAssistantTail: string;
}

const GUARD_MARK = "token guard)";
const COMPACTED = "This session is being continued";
const CMD = /<command-name>\/?([^<]+)<\/command-name>/;
const CMD_ARGS = /<command-args>([\s\S]*?)<\/command-args>/;
const MAX_TEXT = 4000;
const TAIL = 700;

type Block = { type?: unknown; text?: unknown; name?: unknown; input?: unknown };

function textOf(content: unknown): { text: string; toolResult: boolean } {
  if (typeof content === "string") return { text: content, toolResult: false };
  if (!Array.isArray(content)) return { text: "", toolResult: false };
  const parts: string[] = [];
  let toolResult = false;
  for (const b of content as Block[]) {
    if (b?.type === "text" && typeof b.text === "string") parts.push(b.text);
    else if (b?.type === "tool_result") toolResult = true;
  }
  return { text: parts.join("\n"), toolResult };
}

function skillsUsed(content: unknown): string[] {
  if (!Array.isArray(content)) return [];
  const out: string[] = [];
  for (const b of content as Block[]) {
    if (b?.type === "tool_use" && b.name === "Skill" && typeof b.input === "object" && b.input !== null) {
      const skill = (b.input as { skill?: unknown }).skill;
      if (typeof skill === "string") out.push(skill);
    }
  }
  return out;
}

export async function* extractHumanTurns(file: { path: string; project: string; sessionId: string }): AsyncGenerator<HumanTurn> {
  const rl = readline.createInterface({ input: fs.createReadStream(file.path, { encoding: "utf8" }), crlfDelay: Infinity });
  const skills: string[] = [];
  let guard = false;
  let compacted = false;
  let tokens = 0;
  let prev = "";
  let index = 0;
  for await (const line of rl) {
    if (line.includes(GUARD_MARK)) guard = true;
    let rec: { type?: unknown; isSidechain?: unknown; isMeta?: unknown; timestamp?: unknown; message?: { content?: unknown; usage?: Record<string, unknown> } };
    try {
      rec = JSON.parse(line) as typeof rec;
    } catch {
      continue;
    }
    if (rec.isSidechain === true) continue;
    if (rec.type === "assistant") {
      const u = rec.message?.usage ?? {};
      const n = ["input_tokens", "cache_read_input_tokens", "cache_creation_input_tokens"].reduce((s, k) => s + (typeof u[k] === "number" ? (u[k] as number) : 0), 0);
      if (n > 0) tokens = n;
      const { text } = textOf(rec.message?.content);
      if (text.trim()) prev = text.trim();
      for (const s of skillsUsed(rec.message?.content)) if (!skills.includes(s)) skills.push(s);
      continue;
    }
    if (rec.type !== "user" || rec.isMeta === true) continue;
    const { text, toolResult } = textOf(rec.message?.content);
    const s = text.trim();
    if (toolResult && !s) continue;
    if (s.startsWith(COMPACTED)) { compacted = true; continue; }
    let kind: HumanTurn["kind"] = "turn";
    let body = s;
    const m = CMD.exec(s);
    if (m) {
      const name = m[1].trim();
      if (!skills.includes(name)) skills.push(name);
      body = `/${name} ${(CMD_ARGS.exec(s)?.[1] ?? "").trim()}`.trim();
      kind = "command";
    } else {
      const c = classifyUserText(s);
      if (c.kind === "interrupt") kind = "interrupt";
      else if (c.kind !== "user") continue;
    }
    index += 1;
    yield {
      project: file.project, session: file.sessionId, ts: typeof rec.timestamp === "string" ? rec.timestamp : "",
      index, kind, text: body.slice(0, MAX_TEXT), skills: [...skills], guardFiredBefore: guard,
      compactedBefore: compacted, contextTokens: tokens, prevAssistantTail: prev.slice(-TAIL),
    };
  }
}
```

Create `scorer/src/audit/patterns.ts`:

```ts
import type { HumanTurn } from "./human-turns.js";

export type PatternName = "ship_recipe" | "ci_conflicts" | "push_only" | "evidence_env" | "found_defect" | "handoff" | "restate" | "rigor" | "scope_surface" | "dispatch";

// Deterministic floor counts (spec appendix). Workplace-specific labels never appear here;
// profiles add their own patterns in a later plan.
export const PATTERNS: Readonly<Record<PatternName, RegExp>> = {
  ship_recipe: /\/review\s*\+\s*\/addressReview|review loop|bugbot|raise (?:the|it|a) pr as (?:a )?draft|ready (?:to|for) review/i,
  ci_conflicts: /failing (?:ci|checks|tests|db tests|smoke)|fail(?:ed|ing)? ci|merge conflic|\bconflicts\b|typecheck|\blint/i,
  push_only: /^(?:push(?: it)?|push \w+|raise (?:a|the) pr|raise prs?|commit and push)[.! ]*$/i,
  evidence_env: /living preview|\/ui-evidence|pixel diff|visual parity|screenshots?\b.*\b(?:attach|issue|ticket)|attach (?:it|this|that|them|the \w+) to (?:the )?(?:issue|ticket|pr)/i,
  found_defect: /\[Image #\d+\]/,
  handoff: /handoff|hand off|\bdigest\b|pick up (?:from|where)|\bcrashed\b|take ?over/i,
  restate: /as i (?:mentioned|said)|i (?:already|just) (?:said|told)|you were supposed to|why are we not doing it|(?:described|suggests?) (?:in )?(?:the )?ticket|ticket (?:body|description) (?:suggests|says|describes)/i,
  rigor: /100% sure|with evidence|actually (?:works|loads|replicate)|refute or confirm|are you sure|double check|tests? to (?:repro|replicate)/i,
  scope_surface: /everywhere|all (?:the )?(?:places|surfaces|screens|editors)|other (?:places|surfaces|code|screens)|\bwe forgot\b|did we (?:lose|update|miss)/i,
  dispatch: /https?:\/\/\S+\/issue\/[A-Z][A-Z0-9]{1,9}-\d+|\/bugFixOrchestrator\b/,
};

export function countPatterns(turns: Iterable<HumanTurn>): Record<PatternName, { turns: number; sessions: number }> {
  const names = Object.keys(PATTERNS) as PatternName[];
  const acc = Object.fromEntries(names.map((n) => [n, { turns: 0, sessions: new Set<string>() }])) as Record<PatternName, { turns: number; sessions: Set<string> }>;
  for (const t of turns) {
    if (t.kind !== "turn" && t.kind !== "command") continue;
    for (const n of names) {
      if (PATTERNS[n].test(t.text)) {
        acc[n].turns += 1;
        acc[n].sessions.add(t.session);
      }
    }
  }
  return Object.fromEntries(names.map((n) => [n, { turns: acc[n].turns, sessions: acc[n].sessions.size }])) as Record<PatternName, { turns: number; sessions: number }>;
}
```

- [ ] **Step 4: Run the tests**

Run: `cd scorer && npx vitest run tests/audit-human-turns.test.ts tests/audit-patterns.test.ts`
Expected: PASS (all cases).

- [ ] **Step 5: Commit**

```bash
git add scorer/src/audit/human-turns.ts scorer/src/audit/patterns.ts scorer/tests/audit-human-turns.test.ts scorer/tests/audit-patterns.test.ts
git commit -m "feat: scorer audit human-turn extraction and direction-pattern counts"
```

---

### Task 4: per-item token usage (quota baseline) and the items-share calculation

**Files:**
- Create: `scorer/src/audit/usage.ts`, `scorer/src/audit/items.ts`
- Test: `scorer/tests/audit-usage.test.ts`, `scorer/tests/audit-items.test.ts`

**Interfaces:**
- Consumes: `HumanTurn` from Task 3.
- Produces:
  - `sessionTokenTotals(path: string): Promise<{ input: number; cacheRead: number; cacheCreation: number; output: number }>`. It deduplicates assistant records by `message.id`, because Claude Code writes one line per content block with the same usage.
  - `itemIdsForSession(turns: readonly HumanTurn[], pattern: RegExp): string[]`: ids mentioned in the session's human turns, in first-seen order.
  - `summarizeItemUsage(perSession: { session: string; items: string[]; total: number }[]): { items: number; medianTokens: number; p75Tokens: number; byItem: Record<string, number> }`. A session's tokens are split evenly across the items it mentions.
  - `interface ItemRecord { id: string; size?: "XS" | "S" | "M" | "L" | "XL"; ambiguous?: boolean; authorsTrusted?: boolean }`
  - `autoStartShare(items: readonly ItemRecord[], maxSize: ItemRecord["size"]): { eligible: number; total: number; share: number }`

- [ ] **Step 1: Write the failing tests**

Create `scorer/tests/audit-usage.test.ts`:

```ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import type { HumanTurn } from "../src/audit/human-turns.js";
import { itemIdsForSession, sessionTokenTotals, summarizeItemUsage } from "../src/audit/usage.js";

const ID = /[A-Z][A-Z0-9]{1,9}-\d+/g;
const t = (text: string): HumanTurn => ({ project: "p", session: "s", ts: "", index: 1, kind: "turn", text, skills: [], guardFiredBefore: false, compactedBefore: false, contextTokens: 0, prevAssistantTail: "" });

describe("sessionTokenTotals", () => {
  it("sums usage once per message id", async () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "usage-"));
    const file = path.join(dir, "s.jsonl");
    const usage = { input_tokens: 10, cache_read_input_tokens: 100, cache_creation_input_tokens: 5, output_tokens: 7 };
    fs.writeFileSync(file, [
      { type: "assistant", message: { id: "m1", usage } },
      { type: "assistant", message: { id: "m1", usage } },
      { type: "assistant", message: { id: "m2", usage } },
    ].map((l) => JSON.stringify(l)).join("\n"));
    expect(await sessionTokenTotals(file)).toEqual({ input: 20, cacheRead: 200, cacheCreation: 10, output: 14 });
  });
});

describe("itemIdsForSession", () => {
  it("returns distinct ids in first-seen order", () => {
    expect(itemIdsForSession([t("fix ABC-12 and ABC-9"), t("also ABC-12")], ID)).toEqual(["ABC-12", "ABC-9"]);
  });
});

describe("summarizeItemUsage", () => {
  it("splits a session's tokens across its items and reports median and p75", () => {
    const s = summarizeItemUsage([
      { session: "a", items: ["X-1"], total: 100 },
      { session: "b", items: ["X-2", "X-3"], total: 200 },
      { session: "c", items: [], total: 999 },
    ]);
    expect(s.byItem).toEqual({ "X-1": 100, "X-2": 100, "X-3": 100 });
    expect(s).toMatchObject({ items: 3, medianTokens: 100, p75Tokens: 100 });
  });
});
```

Create `scorer/tests/audit-items.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { autoStartShare } from "../src/audit/items.js";

describe("autoStartShare", () => {
  it("counts items at or below maxSize that are unambiguous and fully trusted", () => {
    const r = autoStartShare([
      { id: "a", size: "XS", ambiguous: false, authorsTrusted: true },
      { id: "b", size: "S", ambiguous: false, authorsTrusted: true },
      { id: "c", size: "XS", ambiguous: true, authorsTrusted: true },
      { id: "d", size: "XS", ambiguous: false, authorsTrusted: false },
      { id: "e" },
    ], "XS");
    expect(r).toEqual({ eligible: 1, total: 5, share: 0.2 });
  });

  it("returns share 0 for an empty list", () => {
    expect(autoStartShare([], "XS")).toEqual({ eligible: 0, total: 0, share: 0 });
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd scorer && npx vitest run tests/audit-usage.test.ts tests/audit-items.test.ts`
Expected: FAIL (modules not found).

- [ ] **Step 3: Implement**

Create `scorer/src/audit/usage.ts`:

```ts
import fs from "node:fs";
import readline from "node:readline";

import type { HumanTurn } from "./human-turns.js";

export async function sessionTokenTotals(path: string): Promise<{ input: number; cacheRead: number; cacheCreation: number; output: number }> {
  const seen = new Set<string>();
  const sum = { input: 0, cacheRead: 0, cacheCreation: 0, output: 0 };
  const rl = readline.createInterface({ input: fs.createReadStream(path, { encoding: "utf8" }), crlfDelay: Infinity });
  for await (const line of rl) {
    let rec: { type?: unknown; isSidechain?: unknown; message?: { id?: unknown; usage?: Record<string, unknown> } };
    try {
      rec = JSON.parse(line) as typeof rec;
    } catch {
      continue;
    }
    if (rec.type !== "assistant" || typeof rec.message?.id !== "string" || seen.has(rec.message.id)) continue;
    seen.add(rec.message.id);
    const u = rec.message.usage ?? {};
    const n = (k: string): number => (typeof u[k] === "number" ? (u[k] as number) : 0);
    sum.input += n("input_tokens");
    sum.cacheRead += n("cache_read_input_tokens");
    sum.cacheCreation += n("cache_creation_input_tokens");
    sum.output += n("output_tokens");
  }
  return sum;
}

export function itemIdsForSession(turns: readonly HumanTurn[], pattern: RegExp): string[] {
  const g = new RegExp(pattern.source, pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`);
  const out: string[] = [];
  for (const t of turns) for (const m of t.text.matchAll(g)) if (!out.includes(m[0])) out.push(m[0]);
  return out;
}

function quantile(sorted: readonly number[], q: number): number {
  if (sorted.length === 0) return 0;
  const i = Math.min(sorted.length - 1, Math.floor(q * (sorted.length - 1) + 0.5));
  return sorted[i];
}

export function summarizeItemUsage(perSession: { session: string; items: string[]; total: number }[]): { items: number; medianTokens: number; p75Tokens: number; byItem: Record<string, number> } {
  const byItem: Record<string, number> = {};
  for (const s of perSession) {
    if (s.items.length === 0) continue;
    const share = s.total / s.items.length;
    for (const id of s.items) byItem[id] = (byItem[id] ?? 0) + share;
  }
  const values = Object.values(byItem).sort((a, b) => a - b);
  return { items: values.length, medianTokens: quantile(values, 0.5), p75Tokens: quantile(values, 0.75), byItem };
}
```

Create `scorer/src/audit/items.ts`:

```ts
import { z } from "zod";

export const SIZES = ["XS", "S", "M", "L", "XL"] as const;
export type Size = (typeof SIZES)[number];

export const ItemRecordSchema = z.object({
  id: z.string().min(1),
  size: z.enum(SIZES).optional(),
  ambiguous: z.boolean().optional(),
  authorsTrusted: z.boolean().optional(),
});
export type ItemRecord = z.infer<typeof ItemRecordSchema>;

// Share of items the router could auto-start (spec §7.1, §13 step 0).
// Missing fields count as "not eligible": unknown is never treated as safe.
export function autoStartShare(items: readonly ItemRecord[], maxSize: Size | undefined): { eligible: number; total: number; share: number } {
  const limit = maxSize === undefined ? -1 : SIZES.indexOf(maxSize);
  const eligible = items.filter((i) => i.size !== undefined && SIZES.indexOf(i.size) <= limit && i.ambiguous === false && i.authorsTrusted === true).length;
  return { eligible, total: items.length, share: items.length === 0 ? 0 : eligible / items.length };
}
```

- [ ] **Step 4: Run the tests**

Run: `cd scorer && npx vitest run tests/audit-usage.test.ts tests/audit-items.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scorer/src/audit/usage.ts scorer/src/audit/items.ts scorer/tests/audit-usage.test.ts scorer/tests/audit-items.test.ts
git commit -m "feat: scorer audit per-item token usage and auto-start share"
```

---

### Task 5: `scorer audit` command, outputs and docs

**Files:**
- Create: `scorer/src/audit/run-audit.ts`, `scripts/transcript-audit/README.md`
- Modify: `scorer/src/args.ts` (command union, flags), `scorer/src/cli.ts` (dispatch), `AGENTS.md` (Commands block)
- Test: `scorer/tests/audit-run.test.ts`, `scorer/tests/args.test.ts` (new cases)

**Interfaces:**
- Consumes: `discoverFiles(projectsDir)` from `scorer/src/transcript/discover.ts`; Tasks 3–4 exports.
- Produces:
  - `runAudit(opts: { projectsDir: string; since: Date; outDir: string; itemPattern: RegExp; itemsFile: string | null; maxSize: Size }): Promise<AuditSummary>`
  - `interface AuditSummary { sessions: number; turns: number; commands: number; interrupts: number; patterns: Record<PatternName, { turns: number; sessions: number }>; usage: { items: number; medianTokens: number; p75Tokens: number }; autoStart: { eligible: number; total: number; share: number } | null }`
  - Files written to `outDir`: `human-turns.jsonl`, `summary.json`, `baseline.md`.
  - `CliOptions` gains `command: "audit"`, plus `auditOut: string`, `itemPattern: string`, `itemsFile: string | null`, `maxSize: Size`.

- [ ] **Step 1: Write the failing tests**

In `scorer/tests/args.test.ts`, the existing test `"defaults to a one-day report"` compares the full options object with `toEqual`. Add the four new defaults to its expected object:

```ts
      contextTokensPath: null, liveSession: null, liveCwd: null, liveWindow: 200_000, json: false,
      auditOut: "/home/j/.agentic-workflow/audit", itemPattern: "[A-Z][A-Z0-9]{1,9}-\\d+", itemsFile: null, maxSize: "XS",
```

Then add these tests inside the top-level `describe("parseArgs", …)`, using the file's `NOW` and `HOME` constants:

```ts
it("parses the audit command with defaults", () => {
  const r = parseArgs(["audit"], NOW, HOME);
  expect(r.ok && r.options.command).toBe("audit");
  expect(r.ok && r.options.auditOut).toBe("/home/j/.agentic-workflow/audit");
  expect(r.ok && r.options.itemPattern).toBe("[A-Z][A-Z0-9]{1,9}-\\d+");
  expect(r.ok && r.options.maxSize).toBe("XS");
});

it("rejects an invalid --max-size and an invalid --item-pattern", () => {
  expect(parseArgs(["audit", "--max-size", "XXL"], NOW, HOME).ok).toBe(false);
  expect(parseArgs(["audit", "--item-pattern", "("], NOW, HOME).ok).toBe(false);
});
```

Create `scorer/tests/audit-run.test.ts`:

```ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { runAudit } from "../src/audit/run-audit.js";

describe("runAudit", () => {
  it("writes human-turns.jsonl, summary.json and baseline.md for sessions after --since", async () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "out-"));
    const dir = path.join(projects, "repo");
    fs.mkdirSync(dir);
    const lines = [
      { type: "assistant", message: { id: "m1", usage: { input_tokens: 50, output_tokens: 5 }, content: [{ type: "text", text: "Want me to push?" }] } },
      { type: "user", timestamp: "2026-10-05T00:00:00Z", message: { content: "push" } },
      { type: "user", timestamp: "2026-10-05T00:01:00Z", message: { content: "now fix ABC-7" } },
    ];
    fs.writeFileSync(path.join(dir, "s1.jsonl"), lines.map((l) => JSON.stringify(l)).join("\n"));
    const items = path.join(out, "items.json");
    fs.writeFileSync(items, JSON.stringify([{ id: "ABC-7", size: "XS", ambiguous: false, authorsTrusted: true }, { id: "ABC-8", size: "M" }]));

    const summary = await runAudit({ projectsDir: projects, since: new Date("2026-10-01T00:00:00Z"), outDir: out, itemPattern: /[A-Z][A-Z0-9]{1,9}-\d+/, itemsFile: items, maxSize: "XS" });

    expect(summary).toMatchObject({ sessions: 1, turns: 2, patterns: { push_only: { turns: 1, sessions: 1 } }, usage: { items: 1, medianTokens: 55 }, autoStart: { eligible: 1, total: 2, share: 0.5 } });
    expect(fs.readFileSync(path.join(out, "human-turns.jsonl"), "utf8").trim().split("\n")).toHaveLength(2);
    expect(JSON.parse(fs.readFileSync(path.join(out, "summary.json"), "utf8")).turns).toBe(2);
    expect(fs.readFileSync(path.join(out, "baseline.md"), "utf8")).toContain("| push_only | 1 | 1 |");
  });

  it("rejects a malformed items file with a clear error", async () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "out-"));
    const items = path.join(out, "items.json");
    fs.writeFileSync(items, JSON.stringify([{ size: "XS" }]));
    await expect(runAudit({ projectsDir: projects, since: new Date(0), outDir: out, itemPattern: /X-\d+/, itemsFile: items, maxSize: "XS" })).rejects.toThrow(/items file/);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd scorer && npx vitest run tests/audit-run.test.ts tests/args.test.ts`
Expected: FAIL. `run-audit.js` is missing, and the `audit` command is unknown.

- [ ] **Step 3: Implement**

Create `scorer/src/audit/run-audit.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import { z } from "zod";

import { discoverFiles } from "../transcript/discover.js";
import type { HumanTurn } from "./human-turns.js";
import { extractHumanTurns } from "./human-turns.js";
import type { ItemRecord, Size } from "./items.js";
import { autoStartShare, ItemRecordSchema } from "./items.js";
import type { PatternName } from "./patterns.js";
import { countPatterns } from "./patterns.js";
import { itemIdsForSession, sessionTokenTotals, summarizeItemUsage } from "./usage.js";

export interface AuditSummary {
  sessions: number;
  turns: number;
  commands: number;
  interrupts: number;
  patterns: Record<PatternName, { turns: number; sessions: number }>;
  usage: { items: number; medianTokens: number; p75Tokens: number };
  autoStart: { eligible: number; total: number; share: number } | null;
}

function readItems(file: string): ItemRecord[] {
  const parsed = z.array(ItemRecordSchema).safeParse(JSON.parse(fs.readFileSync(file, "utf8")));
  if (!parsed.success) throw new Error(`items file ${file} is invalid: ${parsed.error.issues[0]?.message ?? "schema mismatch"}`);
  return parsed.data;
}

export async function runAudit(opts: { projectsDir: string; since: Date; outDir: string; itemPattern: RegExp; itemsFile: string | null; maxSize: Size }): Promise<AuditSummary> {
  const items = opts.itemsFile === null ? null : readItems(opts.itemsFile);
  fs.mkdirSync(opts.outDir, { recursive: true });
  const turnsOut = fs.createWriteStream(path.join(opts.outDir, "human-turns.jsonl"));
  const all: HumanTurn[] = [];
  const perSession: { session: string; items: string[]; total: number }[] = [];
  let sessions = 0;
  for (const file of discoverFiles(opts.projectsDir)) {
    if (!file.isMain) continue;
    if (fs.statSync(file.path).mtime < opts.since) continue;
    const turns: HumanTurn[] = [];
    for await (const t of extractHumanTurns({ path: file.path, project: file.project, sessionId: file.sessionId })) {
      if (t.ts !== "" && new Date(t.ts) < opts.since) continue;
      turns.push(t);
      turnsOut.write(`${JSON.stringify(t)}\n`);
    }
    if (turns.length === 0) continue;
    sessions += 1;
    all.push(...turns);
    const tok = await sessionTokenTotals(file.path);
    perSession.push({ session: file.sessionId, items: itemIdsForSession(turns, opts.itemPattern), total: tok.input + tok.cacheRead + tok.cacheCreation + tok.output });
  }
  await new Promise<void>((resolve) => turnsOut.end(resolve));
  const usage = summarizeItemUsage(perSession);
  const summary: AuditSummary = {
    sessions,
    turns: all.filter((t) => t.kind === "turn").length,
    commands: all.filter((t) => t.kind === "command").length,
    interrupts: all.filter((t) => t.kind === "interrupt").length,
    patterns: countPatterns(all),
    usage: { items: usage.items, medianTokens: usage.medianTokens, p75Tokens: usage.p75Tokens },
    autoStart: items === null ? null : autoStartShare(items, opts.maxSize),
  };
  fs.writeFileSync(path.join(opts.outDir, "summary.json"), `${JSON.stringify(summary, null, 2)}\n`);
  fs.writeFileSync(path.join(opts.outDir, "baseline.md"), renderBaseline(summary, opts));
  return summary;
}

function renderBaseline(s: AuditSummary, opts: { since: Date; maxSize: Size }): string {
  const rows = (Object.entries(s.patterns) as [PatternName, { turns: number; sessions: number }][])
    .map(([n, v]) => `| ${n} | ${v.turns} | ${v.sessions} |`)
    .join("\n");
  const pct = (n: number): string => (s.turns === 0 ? "0.0" : ((100 * n) / s.turns).toFixed(1));
  const auto = s.autoStart === null ? "_no items file given_" : `${s.autoStart.eligible}/${s.autoStart.total} (${(100 * s.autoStart.share).toFixed(1)}%) at maxSize ${opts.maxSize}`;
  return [
    `# Transcript audit baseline`,
    ``,
    `Since ${opts.since.toISOString()}: ${s.sessions} sessions, ${s.turns} typed turns, ${s.commands} slash commands, ${s.interrupts} interrupts.`,
    ``,
    `| pattern | turns | sessions |`,
    `|---|---|---|`,
    rows,
    ``,
    `Shares of typed turns: ${(Object.keys(s.patterns) as PatternName[]).map((n) => `${n} ${pct(s.patterns[n].turns)}%`).join(", ")}.`,
    ``,
    `Tokens per item: ${s.usage.items} items, median ${Math.round(s.usage.medianTokens)}, p75 ${Math.round(s.usage.p75Tokens)}.`,
    ``,
    `Auto-start share: ${auto}.`,
    ``,
  ].join("\n");
}
```

In `scorer/src/args.ts`:
- Import `SIZES` and `Size` from `./audit/items.js`.
- Widen `command` to `"report" | "probe" | "context-tokens" | "live" | "audit"`.
- Add fields `auditOut: string; itemPattern: string; itemsFile: string | null; maxSize: Size;`.
- Default them in `options` to `auditOut: path.join(home, ".agentic-workflow", "audit")`, `itemPattern: "[A-Z][A-Z0-9]{1,9}-\\d+"`, `itemsFile: null`, `maxSize: "XS"`.
- Add `if (arg === "audit") { options.command = "audit"; continue; }` next to the `live` branch.
- Add `"--out"`, `"--item-pattern"`, `"--items"` and `"--max-size"` to `VALUE_FLAGS`, and handle them:

```ts
if (arg === "--out") options.auditOut = value;
if (arg === "--items") options.itemsFile = value;
if (arg === "--item-pattern") {
  try {
    new RegExp(value);
  } catch {
    return { ok: false, error: `--item-pattern is not a valid regular expression: ${value}` };
  }
  options.itemPattern = value;
}
if (arg === "--max-size") {
  if (!(SIZES as readonly string[]).includes(value)) return { ok: false, error: `--max-size must be ${SIZES.join("|")}: ${value}` };
  options.maxSize = value as Size;
}
```

In `scorer/src/cli.ts`, add `import { runAudit } from "./audit/run-audit.js";` and a branch before the final `else`:

```ts
} else if (parsed.options.command === "audit") {
  const o = parsed.options;
  try {
    const s = await runAudit({ projectsDir: o.projectsDir, since: o.since, outDir: o.auditOut, itemPattern: new RegExp(o.itemPattern), itemsFile: o.itemsFile, maxSize: o.maxSize });
    console.log(`scorer audit: ${s.sessions} sessions, ${s.turns} turns → ${o.auditOut}/baseline.md`);
  } catch (e) {
    console.error(`scorer audit: ${(e as Error).message}`);
    process.exit(1);
  }
```

Also extend the usage string in `cli.ts` with `| scorer audit [--since 60d] [--out DIR] [--item-pattern RE] [--items FILE] [--max-size XS|S|M|L|XL]`.

Create `scripts/transcript-audit/README.md`:

````markdown
# Transcript audit (Sindri rollout step 0)

Measures where human turns go across Claude Code transcripts and sets the baselines for Sindri's
success metrics (spec §2, §13 step 0).

```bash
(cd scorer && npm run build)
node scorer/dist/cli.js audit --since 60d                 # → ~/.agentic-workflow/audit/
node scorer/dist/cli.js audit --since 60d --items items.json --max-size XS
```

Outputs in `--out` (default `~/.agentic-workflow/audit/`):

| File | Content |
|---|---|
| `human-turns.jsonl` | One record per human turn (`HumanTurn`): text, active skills, guard/compaction state, context tokens, the preceding assistant message tail |
| `summary.json` | Session/turn counts, per-pattern floor counts, tokens per item, auto-start share |
| `baseline.md` | Human-readable baseline table |

`items.json` (optional) is an array of `{ id, size?, ambiguous?, authorsTrusted? }`, from any tracker export or
triage output. Missing fields count as not eligible. Patterns are deterministic floors, not labels.
````

In `AGENTS.md`, under the `# TypeScript packages` commands block, after the `scorer probe` line, add:

```bash
scorer audit [--since 60d] [--items FILE] [--max-size XS]   # Human-turn baseline → ~/.agentic-workflow/audit/
```

- [ ] **Step 4: Run the scorer suite and typecheck**

Run: `cd scorer && npm run typecheck && npm test`
Expected: typecheck clean; all tests pass.

- [ ] **Step 5: Run it against the real transcripts (evidence for the baseline)**

Run: `(cd scorer && npm run build) && node scorer/dist/cli.js audit --since 60d && sed -n 1,25p ~/.agentic-workflow/audit/baseline.md`
Expected: a non-empty table. The `push_only` and `ship_recipe` counts should be of the same order as the spec's appendix (about 85 and 189 over one month). Record the printed numbers in the PR description; they are step 0's baselines.

- [ ] **Step 6: Commit**

```bash
git add scorer/src/audit/run-audit.ts scorer/src/args.ts scorer/src/cli.ts scorer/tests/audit-run.test.ts scorer/tests/args.test.ts scripts/transcript-audit/README.md AGENTS.md
git commit -m "feat: scorer audit command (Sindri step 0 baselines)"
```

---

## Done criteria for this plan
- Merge gate (AGENTS.md) green for `judge` and `scorer`: `npm run typecheck` + `npm test` in each. The done-gate bash tests pass.
- `scorer audit --since 60d` produces `baseline.md` on the real corpus, and the numbers are recorded in the PR.
- Every new test from Review Focus 1–5 is present and passing.
