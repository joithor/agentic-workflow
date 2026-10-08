# Sindri Plan 3: Host Code Index Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the per-repo code index from spec §6.2 on the host. It has five layers: structure, clones, dependencies, local embeddings and a graphify graph. It also computes the shape signals ("reinvented?", "generalize at the second case?", "simpler?") on every commit in **record-only** mode. Then **switch it on** for this repo, so shape thresholds are calibrated on Sindri's own commits from Plan 4 onward (spec §13.3, row 7).

**Architecture:** A new `sindri/src/index/` module inside the Plan 2 package. Each layer is a builder that reads the repo's tracked files and writes tables in one SQLite file per repo, `$AW_STATE_DIR/sindri/index/<repo>.db`. Builds run under the box-wide heavy-job lock, write to a temp copy and swap it in atomically. Each layer has a version stamp, and a stamp mismatch rebuilds that layer. Shape signals compare a per-commit **overlay** (the staged changes, parsed in memory) against the base index. The git pre-commit hook records them to a spool file that is never a blocker. `observe` and `shape report` ingest the spool into the ledger, so hooks never write a database (spec §5.2).

**Tech Stack:** TypeScript 5.7 strict, ESM (Node16 resolution), Node >= 20, Vitest 2 with v8 coverage, Zod 3, better-sqlite3 13. The `typescript` compiler API parses code. Embeddings come from Ollama's `/api/embed` on loopback (`nomic-embed-text`). The graph comes from graphify (`graphifyy` on PyPI, run as `graphify extract --code-only`), which runs under `sandbox-exec` on macOS or `bwrap --unshare-net` on Linux.

**Spec:** `docs/superpowers/specs/2026-10-07-sindri-design.md`. This plan implements §6.2 (code index, signals, offline guarantee, freshness, per-worktree overlay), the §11.3 index dependencies (`sindri index setup`), the `repo add`/`index` CLI rows of §10.3, the §11.5 `doctor` index checks, and §13.3 row 7 (record-only shape signals on ring 0). Enforcement (§6.2's enforced-outcome table, the Shape direction check) is rollout step 3b and is **not** in this plan.

**Depends on:** Plan 2 merged and switched on. This plan uses Plan 2's `Deps`, `COMMANDS`/`CommandDef`, `failure`/`SindriError`, `parseFlags`, the ledger (`migrateWith`, `withEpoch`), the tick lock, `approvedProfile`, the scrubber, and the pre-commit hook (`preCommitHook`, `hookBinary`).

## Spec amendments in this plan

Each is also edited into the spec in Task 11.

1. **The structure layer parses TypeScript and JavaScript with the TypeScript compiler API, not tree-sitter.** Both ring-0 (this repo) and the first ring-1 repo are TypeScript. The compiler API is exact for them, ships with a dependency the repo already has, and needs no native or WASM grammars. Other languages get the graph layer through graphify, which does use tree-sitter. A tree-sitter `Parser` adapter for further languages is a later plan, behind the same `Parser` interface.
2. **Embeddings call Ollama directly.** The Prism route (§6.2 "or Prism with `cloud_fallback:false`") is not built, which keeps one loopback endpoint to verify.
3. **The graphify layer runs on a snapshot of the tracked files** (`git archive HEAD`, minus `index.denyPaths`), never on the working tree, because graphify writes `graphify-out/` into the directory it reads.
4. **The heavy-job lock is a `mkdir` lock at `$AW_STATE_DIR/locks/heavy`**, compatible with `config/lib/locks.sh` (same primitive, `rmdir` to release). The holder record sits beside it, so the lock dir stays empty for `rmdir`.
5. **Shape signals in this plan are record-only and use deterministic layers inside the 2 s commit budget.** The embedding check runs only if the model answers within the remaining budget, otherwise it's recorded as `deferred`. The graph layer's reinvention check uses the call sets the TypeScript parser extracts (and graphify's edges for other languages). Nothing blocks a commit until rollout step 3b.

## Global Constraints

- Node >= 20, TypeScript 5.7 strict mode, ESM with Node16 module resolution (AGENTS.md Tech Stack).
- No `any` types. No `/* v8 ignore */` annotations. 100% line, function, branch and statement coverage in `sindri` (`npm run test:coverage`). New coverage exclusions are only thin process wrappers named in a task, each with a smoke test.
- One heavy job at a time: `npm test`, typecheck and index builds run serially, once per commit (global CLAUDE.md). Index builds take the heavy-job lock themselves.
- **Offline guarantee (spec §6.2, T5):** index code never sends code off the machine. The embedding URL must resolve to loopback (`127.0.0.1`, `::1` or `localhost`), or the build refuses it. graphify runs with network denied and fails closed when that sandbox isn't available.
- Index inputs: tracked files only (`git ls-files`), minus `index.denyPaths`. Symlinks are never followed. Per-file and total size caps apply (spec §6.2 Inputs).
- Hooks never write the ledger or the index. The pre-commit hook writes a spool file; `observe` and `shape report` ingest it (spec §5.2).
- Record-only: `sindri shape --record` always exits 0 and never blocks a commit in this plan.
- Core stays generic: no workplace names, labels, hosts or ticket prefixes in code, defaults or examples.
- Never write a full secret-shaped literal in any file; build test secrets by concatenation (Plan 2's pre-commit guard refuses them).
- Tick each step's checkbox (`- [x]`) in this plan file in the same commit that completes it.
- Commit format: `type: short description`, atomic commits, the session's attribution lines.

## Review Focus

1. **A file that changes between `git ls-files` and the read, or a tracked path that is now a symlink.** The inventory must skip it (lstat, regular files only), not follow it or crash. Pinned in Task 2.
2. **A build interrupted halfway (killed, out of disk).** The live index must stay the previous complete one, with no half-written tables. Builds write a temp copy and rename it. Pinned in Task 5.
3. **Ollama down, the model missing, or a non-loopback URL in the profile.** The embeddings layer must report `unavailable` with a reason and leave the other layers built and usable; a non-loopback URL is refused before any request. Pinned in Task 6.
4. **graphify missing, sandbox missing, or graphify trying the network.** The graph layer fails closed (`unavailable`) and the other layers still build. Pinned in Task 7.
5. **A commit touching only non-code files, a deleted file, or a huge generated file.** `shape --record` must finish under the budget, record nothing spurious, and exit 0. Pinned in Task 9.

---

## File Structure

| File | Responsibility |
|---|---|
| `sindri/src/profile/schema.ts` (modify) | `index` and `shape` profile keys; repo `index.denyPaths` |
| `sindri/src/ledger/db.ts` (modify) | Migration v2: `shape_runs`, `shape_signals` |
| `sindri/src/index/heavy-lock.ts` | `withHeavyLock()`, `heavyLockState()` (spec §8.2 heavy-job lock, host side) |
| `sindri/src/index/globs.ts` | `globToRegExp()` for `**`/`*` path globs |
| `sindri/src/index/files.ts` | Tracked-file inventory with deny paths, symlink refusal and size caps |
| `sindri/src/index/parse-ts.ts` | TypeScript compiler API → symbols with normalized AST hash, tokens, complexity, callees |
| `sindri/src/index/minhash.ts` | MinHash signatures, LSH bands, Jaccard |
| `sindri/src/index/db.ts` | Index SQLite schema, stamps, reader |
| `sindri/src/index/deps-layer.ts` | Package manifests → dependencies with purpose tags |
| `sindri/src/index/embed.ts` | Ollama loopback embedder, vector encode/decode, cosine |
| `sindri/src/index/graph.ts` | graphify adapter: snapshot, sandboxed run, `graph.json` parsing |
| `sindri/src/index/sandbox-real.ts` | Real sandboxed process runner (coverage-excluded, smoke-tested) |
| `sindri/src/index/build.ts` | Incremental build of all layers, atomic swap, version stamps |
| `sindri/src/index/overlay.ts` | Staged changes → in-memory overlay over the base index |
| `sindri/src/index/signals.ts` | Shape signals with evidence |
| `sindri/src/index/spool.ts` | Spool writer (hook side) and ingester (sindri side) |
| `sindri/src/index/commands.ts` | `sindri index build|status|query|setup`, `sindri shape`, `sindri repo add` |
| `sindri/src/scrub/commands.ts` (modify) | Pre-commit hook v2: scrub, then record shape signals |
| `sindri/src/doctor/doctor.ts` (modify) | Index, embeddings, graphify and heavy-lock checks |
| `sindri/tests/fixtures/index-repo/` | Small TypeScript fixture repo for layer tests |
| `docs/sindri/index.md` | How the index works, setup, signals, thresholds |

---
### Task 1: Profile keys, ledger migration v2 and the heavy-job lock

**Files:**
- Create: `sindri/src/index/loopback.ts`, `sindri/src/index/heavy-lock.ts`
- Modify: `sindri/src/profile/schema.ts` (add `index`, `shape`; repo `index`), `sindri/src/ledger/db.ts` (append migration v2), `sindri/src/deps.ts` (add `sleep`), `sindri/src/cli.ts`, `sindri/tests/helpers.ts`, `sindri/src/errors.ts`
- Test: `sindri/tests/index-profile.test.ts`, `sindri/tests/heavy-lock.test.ts`

**Interfaces:**
- Consumes: `ProfileSchema`, `RepoSchema`, `SizeSchema` (Plan 2 Task 5); `migrateWith`/`MIGRATIONS` (Plan 2 Task 3); `Deps`, `awStateDir` (Plan 2 Task 1).
- Produces:
  - `isLoopbackUrl(url: string): boolean` — true only for `http(s)://127.0.0.1|[::1]|localhost[:port]`.
  - Profile keys (all with defaults): `index.{denyPaths, utilityGlobs, maxFileKB, maxTotalMB, maxAgeHours, embeddings.{enabled, url, model}, graph}` and `shape.{record, budgetMs, defaultSize, thresholds.{nameSimilarity, embedding, embeddingAst, nearCloneTokens, nearCloneJaccard, callOverlap, complexityDelta}, sizeBudget.{XS..XL}, exportAllowance.{XS..XL}}`; repo key `index.denyPaths` (added to the profile's).
  - Ledger v2 tables `shape_runs` and `shape_signals` (schema in Step 3).
  - `Deps.sleep(ms: number): Promise<void>`.
  - `withHeavyLock<T>(deps, kind: string, timeoutMs: number, fn: () => Promise<T>): Promise<T>` — throws `SND-INDEX-001` when busy after `timeoutMs`; `heavyLockState(stateRoot: string, now: () => Date): { held: boolean; holder: HeavyHolder | null; ageMs: number | null }`; `heavyLockDir(stateRoot): string`.

- [ ] **Step 1: Write the failing tests**

Add `sleep: async () => undefined,` to the object `makeDeps` returns in `sindri/tests/helpers.ts`.

`sindri/tests/index-profile.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { LEDGER_SCHEMA_VERSION, openLedger, openMemoryLedger } from "../src/ledger/db.js";
import { tempDir } from "./helpers.js";
import { isLoopbackUrl } from "../src/index/loopback.js";
import { ProfileSchema, RepoSchema } from "../src/profile/schema.js";

const base = { schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"] };

describe("index and shape profile keys", () => {
  it("defaults every index and shape key", () => {
    const p = ProfileSchema.parse(base);
    expect(p.index.embeddings).toEqual({ enabled: true, url: "http://127.0.0.1:11434", model: "nomic-embed-text" });
    expect(p.index.graph).toBe("graphify");
    expect(p.index.denyPaths).toContain("**/*.pem");
    expect(p.shape.thresholds).toEqual({
      nameSimilarity: 0.85, embedding: 0.9, embeddingAst: 0.6, nearCloneTokens: 60, nearCloneJaccard: 0.8, callOverlap: 0.5, complexityDelta: 10,
    });
    expect(p.shape.sizeBudget).toEqual({ XS: 80, S: 250, M: 600, L: 1200, XL: 2400 });
    expect(p.shape).toMatchObject({ record: true, budgetMs: 2000, defaultSize: "S" });
    expect(RepoSchema.parse({ schemaVersion: 1, name: "r", path: "/r" }).index).toEqual({ denyPaths: [] });
  });

  it("refuses an embedding URL that is not loopback (offline guarantee)", () => {
    const r = ProfileSchema.safeParse({ ...base, index: { embeddings: { url: "https://embeddings.example.com" } } });
    expect(r.success).toBe(false);
    if (!r.success) expect(r.error.issues[0].message).toContain("loopback");
  });

  it("recognizes loopback URLs only", () => {
    for (const u of ["http://127.0.0.1:11434", "http://localhost:11434/", "http://[::1]:11434"]) expect(isLoopbackUrl(u)).toBe(true);
    for (const u of ["http://10.0.0.5:11434", "https://localhost.example.com", "http://127.0.0.1.nip.io", "not a url", "file:///tmp"]) expect(isLoopbackUrl(u)).toBe(false);
  });
});

describe("ledger migration v2", () => {
  it("adds shape_runs and shape_signals", () => {
    const db = openMemoryLedger();
    expect(LEDGER_SCHEMA_VERSION).toBe(2);
    const tables = (db.prepare("SELECT name FROM sqlite_master WHERE type = 'table'").all() as { name: string }[]).map((t) => t.name);
    expect(tables).toEqual(expect.arrayContaining(["shape_runs", "shape_signals"]));
  });

  it("migrates a v1 ledger file in place and keeps a backup", () => {
    const file = path.join(tempDir(), "ledger.db");
    const v1 = openLedger(file);
    v1.pragma("user_version = 1");
    v1.exec("DROP TABLE shape_signals; DROP TABLE shape_runs;");
    v1.close();
    const db = openLedger(file);
    expect(db.pragma("user_version", { simple: true })).toBe(2);
    expect(fs.existsSync(`${file}.bak-v1`)).toBe(true);
    db.close();
  });
});
```

`sindri/tests/heavy-lock.test.ts`:

```ts
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { awStateDir } from "../src/deps.js";
import { SindriError } from "../src/errors.js";
import { heavyLockDir, heavyLockState, withHeavyLock } from "../src/index/heavy-lock.js";
import { makeDeps } from "./helpers.js";

const LOCKS_SH = path.resolve(import.meta.dirname, "../../config/lib/locks.sh");

describe("heavy-job lock", () => {
  it("holds the lock for the duration of the job and records the holder", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    const seen = await withHeavyLock(d, "index-build", 0, async () => heavyLockState(root, d.now));
    expect(seen).toMatchObject({ held: true, holder: { kind: "index-build", pid: 4242 } });
    expect(heavyLockState(root, d.now)).toEqual({ held: false, holder: null, ageMs: null });
  });

  it("is the same lock as config/lib/locks.sh", async () => {
    const d = makeDeps();
    const dir = heavyLockDir(awStateDir(d));
    await withHeavyLock(d, "index-build", 0, async () => {
      const r = execFileSync("bash", ["-c", `source "${LOCKS_SH}"; acquire_lock "${dir}" 0 && echo got || echo busy`], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
      expect(r.trim()).toBe("busy");
    });
    execFileSync("bash", ["-c", `source "${LOCKS_SH}"; acquire_lock "${dir}" 0`]);
    await expect(withHeavyLock(d, "index-build", 0, async () => 1)).rejects.toThrow(SindriError);
    execFileSync("bash", ["-c", `source "${LOCKS_SH}"; release_lock "${dir}"`]);
    expect(await withHeavyLock(d, "index-build", 0, async () => 2)).toBe(2);
  });

  it("waits, then reports the holder when still busy", async () => {
    let slept = 0;
    const d = makeDeps({ sleep: async () => { slept++; } });
    const root = awStateDir(d);
    await withHeavyLock(d, "test-suite", 0, async () => {
      const err = await withHeavyLock(d, "index-build", 2000, async () => 0).catch((e: unknown) => e);
      expect(err).toBeInstanceOf(SindriError);
      expect((err as SindriError).code).toBe("SND-INDEX-001");
      expect((err as SindriError).message).toContain("test-suite, pid 4242");
    });
    expect(slept).toBe(2);
    fs.mkdirSync(heavyLockDir(root));
    const err = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err as SindriError).message).toBe("the heavy-job lock is busy");
    expect(heavyLockState(root, () => new Date(Date.now() + 60_000)).ageMs).toBeGreaterThan(0);
  });

  it("releases the lock when the job throws, and rethrows unexpected mkdir errors", async () => {
    const d = makeDeps();
    await expect(withHeavyLock(d, "x", 0, async () => { throw new Error("job failed"); })).rejects.toThrow("job failed");
    expect(heavyLockState(awStateDir(d), d.now).held).toBe(false);
    fs.mkdirSync(path.join(awStateDir(d), "locks"), { recursive: true });
    fs.chmodSync(path.join(awStateDir(d), "locks"), 0o500);
    try {
      await expect(withHeavyLock(d, "x", 0, async () => 0)).rejects.toThrow(/EACCES/);
    } finally {
      fs.chmodSync(path.join(awStateDir(d), "locks"), 0o700);
    }
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/index-profile.test.ts tests/heavy-lock.test.ts`
Expected: FAIL with `Failed to load url ../src/index/loopback.js` (and `../src/index/heavy-lock.js`).

- [ ] **Step 3: Implement**

`sindri/src/index/loopback.ts`:

```ts
// Spec §6.2 offline guarantee: the embedding endpoint must be this machine.
export function isLoopbackUrl(url: string): boolean {
  let u: URL;
  try {
    u = new URL(url);
  } catch {
    return false;
  }
  if (u.protocol !== "http:" && u.protocol !== "https:") return false;
  return u.hostname === "127.0.0.1" || u.hostname === "[::1]" || u.hostname === "localhost";
}
```

In `sindri/src/profile/schema.ts`, add before `export const ProfileSchema`:

```ts
const unit = z.number().min(0).max(1);
const count = z.number().int().positive();
const bySize = (d: Record<Size, number>) =>
  z.object({ XS: count.default(d.XS), S: count.default(d.S), M: count.default(d.M), L: count.default(d.L), XL: count.default(d.XL) }).strict().default({});

const IndexSchema = z
  .object({
    denyPaths: z.array(z.string().min(1)).default([".env*", "**/.env*", "**/*.pem", "**/*.key", "**/secrets/**"])
      .describe("Path globs the index never reads (secrets, PHI fixtures, generated code)"),
    utilityGlobs: z.array(z.string().min(1)).default([]).describe("Globs of internal utility modules; their exports are reinvention candidates"),
    maxFileKB: count.default(512),
    maxTotalMB: count.default(200),
    maxAgeHours: count.default(24).describe("An index older than this is stale (doctor warns; shape records index-stale)"),
    embeddings: z
      .object({
        enabled: z.boolean().default(true),
        url: z.string().refine(isLoopbackUrl, "must be a loopback URL (127.0.0.1, [::1] or localhost): the index never sends code off the machine").default("http://127.0.0.1:11434"),
        model: z.string().min(1).default("nomic-embed-text"),
      })
      .strict()
      .default({}),
    graph: z.enum(["graphify", "none"]).default("graphify"),
  })
  .strict()
  .default({})
  .describe("Code index (spec §6.2)");

const ShapeSchema = z
  .object({
    record: z.boolean().default(true).describe("Record shape signals at commit (record-only until rollout step 3b)"),
    budgetMs: count.default(2000),
    defaultSize: SizeSchema.default("S").describe("Size class used for diff budgets when a commit has no item"),
    thresholds: z
      .object({
        nameSimilarity: unit.default(0.85),
        embedding: unit.default(0.9),
        embeddingAst: unit.default(0.6),
        nearCloneTokens: count.default(60),
        nearCloneJaccard: unit.default(0.8),
        callOverlap: unit.default(0.5),
        complexityDelta: count.default(10),
      })
      .strict()
      .default({}),
    sizeBudget: bySize({ XS: 80, S: 250, M: 600, L: 1200, XL: 2400 }).describe("Changed-line budget per size class"),
    exportAllowance: bySize({ XS: 1, S: 3, M: 6, L: 10, XL: 20 }).describe("New exports allowed per size class"),
  })
  .strict()
  .default({})
  .describe("Shape signals (spec §6.2)");
```

Add `import { isLoopbackUrl } from "../index/loopback.js";` at the top, and the keys `index: IndexSchema,` and `shape: ShapeSchema,` to the `ProfileSchema` object (after `scrub`). In `RepoSchema`, add after `overrides`:

```ts
    index: z.object({ denyPaths: z.array(z.string().min(1)).default([]) }).strict().default({}).describe("index.denyPaths for this repo, added to the profile's"),
```

In `sindri/src/ledger/db.ts`, append to `MIGRATIONS` (never edit entry 0):

```ts
  `
  CREATE TABLE shape_runs (
    run_id TEXT PRIMARY KEY,
    repo TEXT NOT NULL,
    ts TEXT NOT NULL,
    head TEXT,
    elapsed_ms INTEGER NOT NULL,
    deferred TEXT NOT NULL,
    signal_count INTEGER NOT NULL,
    epoch INTEGER NOT NULL
  );
  CREATE TABLE shape_signals (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL REFERENCES shape_runs(run_id),
    type TEXT NOT NULL,
    layer TEXT NOT NULL,
    value REAL NOT NULL,
    threshold REAL NOT NULL,
    at TEXT NOT NULL,
    existing TEXT,
    detail TEXT NOT NULL,
    epoch INTEGER NOT NULL
  );
  CREATE INDEX shape_signals_type ON shape_signals(type);
  `,
```

`sindri/src/index/heavy-lock.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { awStateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";

// The box-wide heavy-job lock (global CLAUDE.md: one heavy job at a time). Same
// primitive as config/lib/locks.sh: mkdir to acquire, rmdir to release. The
// holder record sits beside the dir so the dir stays empty for rmdir.
export interface HeavyHolder {
  kind: string;
  pid: number;
  startedAt: string;
}

const HolderSchema = z.object({ kind: z.string(), pid: z.number().int(), startedAt: z.string() });

export const heavyLockDir = (stateRoot: string): string => path.join(stateRoot, "locks", "heavy");
const holderFile = (stateRoot: string): string => path.join(stateRoot, "locks", "heavy.holder.json");

function readHolder(stateRoot: string): HeavyHolder | null {
  try {
    const r = HolderSchema.safeParse(JSON.parse(fs.readFileSync(holderFile(stateRoot), "utf8")));
    return r.success ? r.data : null;
  } catch {
    return null;
  }
}

export async function withHeavyLock<T>(deps: Deps, kind: string, timeoutMs: number, fn: () => Promise<T>): Promise<T> {
  const root = awStateDir(deps);
  const dir = heavyLockDir(root);
  fs.mkdirSync(path.dirname(dir), { recursive: true });
  // Bounded by attempts, not wall time, so a fake clock can't loop forever.
  const attempts = Math.ceil(timeoutMs / 1000) + 1;
  for (let i = 1; ; i++) {
    try {
      fs.mkdirSync(dir);
      break;
    } catch (e) {
      if ((e as NodeJS.ErrnoException).code !== "EEXIST") throw e;
    }
    if (i >= attempts) {
      const h = readHolder(root);
      const who = h === null ? "" : ` (${h.kind}, pid ${h.pid}, since ${h.startedAt})`;
      throw new SindriError("SND-INDEX-001", `the heavy-job lock is busy${who}`);
    }
    await deps.sleep(1000);
  }
  fs.writeFileSync(holderFile(root), JSON.stringify({ kind, pid: deps.system.pid, startedAt: deps.now().toISOString() }));
  try {
    return await fn();
  } finally {
    fs.rmSync(holderFile(root), { force: true });
    fs.rmdirSync(dir);
  }
}

export function heavyLockState(stateRoot: string, now: () => Date): { held: boolean; holder: HeavyHolder | null; ageMs: number | null } {
  const st = fs.statSync(heavyLockDir(stateRoot), { throwIfNoEntry: false });
  if (st === undefined) return { held: false, holder: null, ageMs: null };
  return { held: true, holder: readHolder(stateRoot), ageMs: now().getTime() - st.mtimeMs };
}
```

In `sindri/src/deps.ts`, add the field `sleep: (ms: number) => Promise<void>;`. In `sindri/src/cli.ts`, pass `sleep: (ms) => new Promise((r) => setTimeout(r, ms)),`.

Add to `ERRORS`:

```ts
  "SND-INDEX-001": { summary: "The heavy-job lock is busy.", fix: "wait for the holder to finish; `sindri doctor` shows it" },
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npm run gen && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS (Plan 2's `profile-doc` test passes after `npm run gen` regenerates `profile.md` and the schemas); 100% coverage.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests sindri/schema docs/sindri
git commit -m "feat: sindri index and shape profile keys, ledger v2, heavy-job lock"
```

---

### Task 2: Path globs and the tracked-file inventory

**Files:**
- Create: `sindri/src/index/globs.ts`, `sindri/src/index/files.ts`
- Modify: `sindri/src/errors.ts` (add `SND-INDEX-002`, `SND-INDEX-003`)
- Test: `sindri/tests/globs.test.ts`, `sindri/tests/files.test.ts`

**Interfaces:**
- Consumes: `GitRunner` (Plan 2 Task 6); `SindriError` (Plan 2 Task 1).
- Produces:
  - `globToRegExp(glob: string): RegExp` — `**/` matches zero or more directories, a trailing `**` anything, `*` anything within one segment, `?` one character; `matchesAny(p: string, globs: readonly string[]): boolean`.
  - `interface IndexedFile { path: string; hash: string; size: number; text: string }` (`hash` = sha256 hex of the bytes).
  - `type SkipReason = "denied" | "symlink" | "not-a-file" | "too-large" | "unreadable"`.
  - `inventory(git: GitRunner, repoPath: string, o: { denyPaths: readonly string[]; maxFileKB: number; maxTotalMB: number; select: (p: string) => boolean }): Promise<{ files: IndexedFile[]; skipped: { path: string; reason: SkipReason }[] }>` — tracked files only, sorted by path; throws `SND-INDEX-002` outside a git repo and `SND-INDEX-003` past `maxTotalMB`.
  - `isSourcePath(p: string): boolean` — `.ts .tsx .mts .cts .js .jsx .mjs .cjs`, not `.d.ts`.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/globs.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { globToRegExp, matchesAny } from "../src/index/globs.js";

describe("globToRegExp", () => {
  it.each([
    ["**/*.pem", "a/b/key.pem", true],
    ["**/*.pem", "key.pem", true],
    ["**/*.pem", "key.pem.txt", false],
    [".env*", ".env.local", true],
    [".env*", "app/.env", false],
    ["**/.env*", "app/.env", true],
    ["src/*.ts", "src/a.ts", true],
    ["src/*.ts", "src/x/a.ts", false],
    ["src/**", "src/x/y/z.ts", true],
    ["**/secrets/**", "a/secrets/b/c.ts", true],
    ["file?.ts", "file1.ts", true],
    ["a+b.(c).ts", "a+b.(c).ts", true],
  ])("%s vs %s → %s", (glob, p, want) => {
    expect(globToRegExp(glob).test(p)).toBe(want);
  });

  it("matchesAny checks every glob", () => {
    expect(matchesAny("x/key.pem", ["src/**", "**/*.pem"])).toBe(true);
    expect(matchesAny("x/key.ts", [])).toBe(false);
  });
});
```

`sindri/tests/files.test.ts`:

```ts
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { SindriError } from "../src/errors.js";
import { inventory, isSourcePath } from "../src/index/files.js";
import { realGitRunner } from "../src/git-real.js";
import { tempDir } from "./helpers.js";

afterEach(() => vi.restoreAllMocks());

function repo(files: Record<string, string>, links: Record<string, string> = {}): string {
  const root = tempDir("sindri-inv-");
  for (const [rel, text] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  for (const [rel, target] of Object.entries(links)) fs.symlinkSync(target, path.join(root, rel));
  execFileSync("git", ["init", "-q"], { cwd: root });
  execFileSync("git", ["add", "-A"], { cwd: root });
  return root;
}

const opts = { denyPaths: [".env*", "**/secrets/**"], maxFileKB: 1, maxTotalMB: 1, select: isSourcePath };

describe("inventory (Review Focus 1)", () => {
  it("lists tracked source files with hashes, skipping denied paths, symlinks and big files", async () => {
    const root = repo(
      { "src/a.ts": "export const a = 1;\n", "src/b.d.ts": "declare const b: number;\n", "src/secrets/k.ts": "x", ".env.ts": "x", "big.ts": "x".repeat(2000), "README.md": "# hi\n" },
      { "src/link.ts": "a.ts" },
    );
    fs.writeFileSync(path.join(root, "src/untracked.ts"), "export {};\n");
    const inv = await inventory(realGitRunner(), root, opts);
    expect(inv.files.map((f) => f.path)).toEqual(["src/a.ts"]);
    expect(inv.files[0]).toMatchObject({ size: 20, text: "export const a = 1;\n" });
    expect(inv.files[0].hash).toMatch(/^[0-9a-f]{64}$/);
    expect(inv.skipped).toEqual([
      { path: ".env.ts", reason: "denied" },
      { path: "big.ts", reason: "too-large" },
      { path: "src/link.ts", reason: "symlink" },
      { path: "src/secrets/k.ts", reason: "denied" },
    ]);
  });

  it("skips a tracked file that is gone, became a directory, or turned into a symlink before the read", async () => {
    const root = repo({ "a.ts": "1", "b.ts": "2", "c.ts": "3" });
    fs.rmSync(path.join(root, "a.ts"));
    fs.rmSync(path.join(root, "b.ts"));
    fs.mkdirSync(path.join(root, "b.ts"));
    const real = fs.openSync;
    vi.spyOn(fs, "openSync").mockImplementation((p, flags, mode) => {
      if (String(p).endsWith("c.ts")) throw Object.assign(new Error("loop"), { code: "ELOOP" });
      return real(p, flags, mode);
    });
    const inv = await inventory(realGitRunner(), root, opts);
    expect(inv.files).toEqual([]);
    expect(inv.skipped).toEqual([
      { path: "a.ts", reason: "unreadable" },
      { path: "b.ts", reason: "not-a-file" },
      { path: "c.ts", reason: "unreadable" },
    ]);
  });

  it("stops past maxTotalMB and outside a git repo", async () => {
    const root = repo({ "a.ts": "x".repeat(900), "b.ts": "y".repeat(900) });
    const err = await inventory(realGitRunner(), root, { ...opts, maxTotalMB: 0.001 }).catch((e: unknown) => e);
    expect((err as SindriError).code).toBe("SND-INDEX-003");
    const err2 = await inventory(realGitRunner(), tempDir(), opts).catch((e: unknown) => e);
    expect((err2 as SindriError).code).toBe("SND-INDEX-002");
  });

  it("isSourcePath accepts TS and JS, not declarations or other files", () => {
    for (const p of ["a.ts", "a.tsx", "a.mts", "a.cts", "a.js", "a.jsx", "a.mjs", "a.cjs"]) expect(isSourcePath(p)).toBe(true);
    for (const p of ["a.d.ts", "a.md", "package.json", "a.ts.bak"]) expect(isSourcePath(p)).toBe(false);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/globs.test.ts tests/files.test.ts`
Expected: FAIL with `Failed to load url ../src/index/globs.js` (and `files.js`).

- [ ] **Step 3: Implement**

`sindri/src/index/globs.ts`:

```ts
// Path globs for index.denyPaths and index.utilityGlobs. Paths are repo-relative
// with "/" separators. "**/" = zero or more directories; trailing "**" = anything;
// "*" = anything inside one segment; "?" = one character.
export function globToRegExp(glob: string): RegExp {
  let re = "";
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i];
    if (c === "*" && glob[i + 1] === "*") {
      if (glob[i + 2] === "/") {
        re += "(?:.*/)?";
        i += 2;
      } else {
        re += ".*";
        i += 1;
      }
    } else if (c === "*") {
      re += "[^/]*";
    } else if (c === "?") {
      re += "[^/]";
    } else {
      re += c.replace(/[.+^${}()|[\]\\]/g, "\\$&");
    }
  }
  return new RegExp(`^${re}$`);
}

export function matchesAny(p: string, globs: readonly string[]): boolean {
  return globs.some((g) => globToRegExp(g).test(p));
}
```

`sindri/src/index/files.ts`:

```ts
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import { matchesAny } from "./globs.js";

export interface IndexedFile {
  path: string;
  hash: string;
  size: number;
  text: string;
}

export type SkipReason = "denied" | "symlink" | "not-a-file" | "too-large" | "unreadable";

const SOURCE = /\.(?:ts|tsx|mts|cts|js|jsx|mjs|cjs)$/;

export function isSourcePath(p: string): boolean {
  return SOURCE.test(p) && !p.endsWith(".d.ts");
}

// Spec §6.2 inputs: tracked files only, minus denyPaths; symlinks are never
// followed (lstat, then O_NOFOLLOW so a file swapped for a link mid-read fails).
export async function inventory(
  git: GitRunner,
  repoPath: string,
  o: { denyPaths: readonly string[]; maxFileKB: number; maxTotalMB: number; select: (p: string) => boolean },
): Promise<{ files: IndexedFile[]; skipped: { path: string; reason: SkipReason }[] }> {
  const ls = await git.run(["ls-files", "-z", "--cached"], repoPath);
  if (!ls.ok) throw new SindriError("SND-INDEX-002", `${repoPath} is not a git repo (git ls-files failed)`);
  const files: IndexedFile[] = [];
  const skipped: { path: string; reason: SkipReason }[] = [];
  let total = 0;
  for (const rel of [...new Set(ls.stdout.split("\0").filter((p) => p !== ""))].sort()) {
    if (!o.select(rel)) continue;
    if (matchesAny(rel, o.denyPaths)) {
      skipped.push({ path: rel, reason: "denied" });
      continue;
    }
    const full = path.join(repoPath, rel);
    const st = fs.lstatSync(full, { throwIfNoEntry: false });
    const reason: SkipReason | null =
      st === undefined ? "unreadable" : st.isSymbolicLink() ? "symlink" : !st.isFile() ? "not-a-file" : st.size > o.maxFileKB * 1024 ? "too-large" : null;
    if (reason !== null) {
      skipped.push({ path: rel, reason });
      continue;
    }
    let buf: Buffer;
    try {
      const fd = fs.openSync(full, fs.constants.O_RDONLY | fs.constants.O_NOFOLLOW);
      try {
        buf = fs.readFileSync(fd);
      } finally {
        fs.closeSync(fd);
      }
    } catch {
      skipped.push({ path: rel, reason: "unreadable" });
      continue;
    }
    total += buf.length;
    if (total > o.maxTotalMB * 1024 * 1024) {
      throw new SindriError("SND-INDEX-003", `index input is over index.maxTotalMB (${o.maxTotalMB} MB)`);
    }
    files.push({ path: rel, hash: createHash("sha256").update(buf).digest("hex"), size: buf.length, text: buf.toString("utf8") });
  }
  return { files, skipped };
}
```

Add to `ERRORS`:

```ts
  "SND-INDEX-002": { summary: "The repo path is not a git repo.", fix: "check repos/<name>.yaml path, then sindri profile approve" },
  "SND-INDEX-003": { summary: "The index input is larger than index.maxTotalMB.", fix: "add generated or vendored paths to index.denyPaths, or raise index.maxTotalMB" },
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npm run gen && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests docs/sindri/errors.md
git commit -m "feat: sindri index path globs and tracked-file inventory"
```

---

### Task 3: TypeScript structure parser (symbols, normalized AST hash, complexity, callees)

**Files:**
- Create: `sindri/src/index/parse-ts.ts`
- Modify: `sindri/package.json` (move `typescript` from `devDependencies` to `dependencies`)
- Test: `sindri/tests/parse-ts.test.ts`

**Interfaces:**
- Produces:
  ```ts
  interface ParsedSymbol {
    name: string;            // "foo", "Store.save", or "default"
    kind: "function" | "method" | "arrow" | "class";
    file: string;
    startLine: number;       // 1-based
    endLine: number;
    exported: boolean;
    signature: string;       // "(a: string, b = 1): Promise<void>", whitespace collapsed; "" for classes
    astHash: string;         // sha256 of the normalized token sequence of the whole node
    tokens: string[];        // node kinds; identifiers → "$id", literals → "$lit"
    complexity: number;      // 1 + decision points
    callees: string[];       // sorted unique names this symbol calls
    text: string;            // source text, capped at 4000 characters
  }
  interface Parser { supports(p: string): boolean; parse(file: string, text: string): ParsedSymbol[] }
  const typescriptParser: Parser;
  ```
  Renaming identifiers or changing literals doesn't change `astHash`; changing structure does. That is what "clone" means in §6.2.

- [ ] **Step 1: Write the failing tests**

In `sindri/package.json`, move `"typescript": "^5.7.0"` from `devDependencies` into `dependencies`, then run `cd sindri && npm install` (expected: `up to date` or `changed 0 packages`).

`sindri/tests/parse-ts.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { typescriptParser } from "../src/index/parse-ts.js";

const SRC = `
import { helper } from "./helper";

export function add(a: number, b: number): number {
  return a + b;
}

export function sum(x: number, y: number): number {
  return x + y;
}

export const mul = (a: number, b = 2) => helper(a * b);

const fn = function (s: string) { return s.trim(); };

export default function () { return 1; }

export class Store {
  save(id: string): void {
    if (id && this.ok) { log.info(id); } else { log.warn(id); }
    for (const x of [1, 2]) { while (x > 3) { break; } }
  }
  private hidden(): boolean { return true ? false : true; }
}

class Internal {
  run() { const inner = () => 0; return inner(); }
}
`;

describe("typescriptParser", () => {
  const syms = typescriptParser.parse("src/math.ts", SRC);
  const by = (name: string) => {
    const s = syms.find((x) => x.name === name);
    if (s === undefined) throw new Error(`no symbol ${name}`);
    return s;
  };

  it("finds functions, arrows, function expressions, default exports, classes and methods", () => {
    expect(syms.map((s) => [s.name, s.kind, s.exported])).toEqual([
      ["add", "function", true],
      ["sum", "function", true],
      ["mul", "arrow", true],
      ["fn", "arrow", false],
      ["default", "function", true],
      ["Store", "class", true],
      ["Store.save", "method", true],
      ["Store.hidden", "method", false],
      ["Internal", "class", false],
      ["Internal.run", "method", false],
      ["inner", "arrow", false],
    ]);
  });

  it("records lines, signatures and capped text", () => {
    expect(by("add")).toMatchObject({ file: "src/math.ts", startLine: 4, endLine: 6, signature: "(a: number, b: number): number" });
    expect(by("mul").signature).toBe("(a: number, b = 2)");
    expect(by("Store").signature).toBe("");
    expect(by("add").text.startsWith("export function add")).toBe(true);
  });

  it("gives renamed clones the same hash and different structure a different one", () => {
    expect(by("add").astHash).toBe(by("sum").astHash);
    expect(by("add").astHash).not.toBe(by("mul").astHash);
    expect(by("add").tokens).toContain("$id");
    expect(by("mul").tokens).toContain("$lit");
  });

  it("counts decision points and callees", () => {
    expect(by("add").complexity).toBe(1);
    expect(by("Store.save").complexity).toBe(5); // if, &&, for-of, while
    expect(by("Store.hidden").complexity).toBe(2); // ?:
    expect(by("Store.save").callees).toEqual(["info", "warn"]);
    expect(by("mul").callees).toEqual(["helper"]);
    expect(by("Internal.run").callees).toEqual(["inner"]);
  });

  it("parses JS, JSX and TSX, tolerates syntax errors, and caps text", () => {
    expect(typescriptParser.parse("a.js", "function f(){ return g(); }").map((s) => s.name)).toEqual(["f"]);
    expect(typescriptParser.parse("a.jsx", "const C = () => <div/>;").map((s) => s.name)).toEqual(["C"]);
    expect(typescriptParser.parse("a.tsx", "export const C = (): JSX.Element => <div/>;").map((s) => s.kind)).toEqual(["arrow"]);
    expect(() => typescriptParser.parse("bad.ts", "function ( {")).not.toThrow();
    const long = typescriptParser.parse("l.ts", `function big() { return "${"x".repeat(5000)}"; }`);
    expect(long[0].text).toHaveLength(4000);
  });

  it("supports TS and JS paths only", () => {
    expect(typescriptParser.supports("a.mts")).toBe(true);
    expect(typescriptParser.supports("a.d.ts")).toBe(false);
    expect(typescriptParser.supports("a.py")).toBe(false);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/parse-ts.test.ts`
Expected: FAIL with `Failed to load url ../src/index/parse-ts.js`.

- [ ] **Step 3: Implement**

`sindri/src/index/parse-ts.ts`:

```ts
import { createHash } from "node:crypto";
import ts from "typescript";

import { isSourcePath } from "./files.js";

export interface ParsedSymbol {
  name: string;
  kind: "function" | "method" | "arrow" | "class";
  file: string;
  startLine: number;
  endLine: number;
  exported: boolean;
  signature: string;
  astHash: string;
  tokens: string[];
  complexity: number;
  callees: string[];
  text: string;
}

export interface Parser {
  supports(p: string): boolean;
  parse(file: string, text: string): ParsedSymbol[];
}

const TEXT_CAP = 4000;

function scriptKind(file: string): ts.ScriptKind {
  if (file.endsWith(".tsx")) return ts.ScriptKind.TSX;
  if (file.endsWith(".jsx")) return ts.ScriptKind.JSX;
  if (/\.(?:js|mjs|cjs)$/.test(file)) return ts.ScriptKind.JS;
  return ts.ScriptKind.TS;
}

// Identifiers and literals are abstracted, so a renamed copy hashes the same (spec §6.2 clones).
function tokensOf(node: ts.Node): string[] {
  const out: string[] = [];
  const walk = (n: ts.Node): void => {
    if (ts.isIdentifier(n) || ts.isPrivateIdentifier(n)) out.push("$id");
    else if (ts.isLiteralExpression(n) || n.kind === ts.SyntaxKind.TrueKeyword || n.kind === ts.SyntaxKind.FalseKeyword) out.push("$lit");
    else {
      out.push(ts.SyntaxKind[n.kind]);
      ts.forEachChild(n, walk);
    }
  };
  walk(node);
  return out;
}

const DECISIONS = new Set([
  ts.SyntaxKind.IfStatement, ts.SyntaxKind.ForStatement, ts.SyntaxKind.ForInStatement, ts.SyntaxKind.ForOfStatement,
  ts.SyntaxKind.WhileStatement, ts.SyntaxKind.DoStatement, ts.SyntaxKind.CaseClause, ts.SyntaxKind.CatchClause, ts.SyntaxKind.ConditionalExpression,
]);
const LOGICAL = new Set([ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken]);

function analyze(node: ts.Node): { complexity: number; callees: string[] } {
  let complexity = 1;
  const callees = new Set<string>();
  const walk = (n: ts.Node): void => {
    if (DECISIONS.has(n.kind) || (ts.isBinaryExpression(n) && LOGICAL.has(n.operatorToken.kind))) complexity++;
    if (ts.isCallExpression(n)) {
      if (ts.isIdentifier(n.expression)) callees.add(n.expression.text);
      else if (ts.isPropertyAccessExpression(n.expression)) callees.add(n.expression.name.text);
    }
    ts.forEachChild(n, walk);
  };
  ts.forEachChild(node, walk);
  return { complexity, callees: [...callees].sort() };
}

const isExported = (n: ts.Declaration): boolean => (ts.getCombinedModifierFlags(n) & ts.ModifierFlags.Export) !== 0;

export const typescriptParser: Parser = {
  supports: isSourcePath,
  parse(file, text) {
    const sf = ts.createSourceFile(file, text, ts.ScriptTarget.ES2022, true, scriptKind(file));
    const out: ParsedSymbol[] = [];
    const add = (name: string, kind: ParsedSymbol["kind"], node: ts.Node, signature: string, exported: boolean): void => {
      const tokens = tokensOf(node);
      out.push({
        name,
        kind,
        file,
        startLine: sf.getLineAndCharacterOfPosition(node.getStart(sf)).line + 1,
        endLine: sf.getLineAndCharacterOfPosition(node.getEnd()).line + 1,
        exported,
        signature: signature.replace(/\s+/g, " "),
        astHash: createHash("sha256").update(tokens.join(" ")).digest("hex"),
        tokens,
        ...analyze(node),
        text: node.getText(sf).slice(0, TEXT_CAP),
      });
    };
    const sig = (f: ts.SignatureDeclaration): string =>
      `(${f.parameters.map((p) => p.getText(sf)).join(", ")})${f.type === undefined ? "" : `: ${f.type.getText(sf)}`}`;

    const visit = (node: ts.Node, cls: { name: string; exported: boolean } | null): void => {
      let inner = cls;
      if (ts.isFunctionDeclaration(node) && node.body !== undefined) {
        add(node.name?.text ?? "default", "function", node, sig(node), isExported(node));
      } else if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer !== undefined &&
        (ts.isArrowFunction(node.initializer) || ts.isFunctionExpression(node.initializer))) {
        add(node.name.text, "arrow", node, sig(node.initializer), isExported(node));
      } else if (ts.isClassDeclaration(node)) {
        const name = node.name?.text ?? "default";
        add(name, "class", node, "", isExported(node));
        inner = { name, exported: isExported(node) };
      } else if (ts.isMethodDeclaration(node) && node.body !== undefined && cls !== null) {
        const priv = (ts.getCombinedModifierFlags(node) & ts.ModifierFlags.Private) !== 0;
        add(`${cls.name}.${node.name.getText(sf)}`, "method", node, sig(node), cls.exported && !priv);
      }
      ts.forEachChild(node, (child) => visit(child, inner));
    };
    visit(sf, null);
    return out;
  },
};
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage. If a test's exact `complexity` or `startLine` differs, check the fixture's line count first (the template literal starts with a newline, so `add` is on line 4); don't loosen the assertion.

- [ ] **Step 5: Commit**

```bash
git add sindri/package.json sindri/package-lock.json sindri/src/index/parse-ts.ts sindri/tests/parse-ts.test.ts
git commit -m "feat: sindri TypeScript structure parser for the code index"
```

---

### Task 4: MinHash signatures and LSH bands

**Files:**
- Create: `sindri/src/index/minhash.ts`
- Test: `sindri/tests/minhash.test.ts`

**Interfaces:**
- Produces:
  - `SHINGLE = 5`, `PERMS = 64`, `BANDS = 16` (`ROWS = 4` per band).
  - `shingles(tokens: string[], k?: number): Set<string>` — `k`-token windows; a sequence shorter than `k` gives one shingle of the whole sequence.
  - `signature(tokens: string[]): Uint32Array` — 64 minimums of seeded FNV-1a hashes over the shingles.
  - `bandKeys(sig: Uint32Array): string[]` — 16 keys `"<band>:<hex>"`; two signatures that share any key are LSH candidates (no pairwise comparison, spec §6.2).
  - `estimateJaccard(a: Uint32Array, b: Uint32Array): number`; `jaccard(a: Set<string>, b: Set<string>): number` (exact).
  - `encodeSig(sig): Buffer` / `decodeSig(buf): Uint32Array` for SQLite blobs.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/minhash.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { bandKeys, decodeSig, encodeSig, estimateJaccard, jaccard, PERMS, shingles, signature } from "../src/index/minhash.js";

const seq = (n: number, tag = "a") => Array.from({ length: n }, (_, i) => `${tag}${i % 17}-${i}`);

describe("minhash", () => {
  it("builds k-token shingles, and one shingle for short sequences", () => {
    expect([...shingles(["a", "b", "c", "d", "e", "f"], 5)]).toEqual(["a b c d e", "b c d e f"]);
    expect([...shingles(["a", "b"], 5)]).toEqual(["a b"]);
    expect(shingles([], 5).size).toBe(0);
  });

  it("is deterministic and estimates Jaccard closely", () => {
    const a = seq(200);
    const b = [...seq(180), ...seq(20, "z")];
    expect(signature(a)).toEqual(signature(a));
    expect(signature(a)).toHaveLength(PERMS);
    const exact = jaccard(shingles(a), shingles(b));
    expect(Math.abs(estimateJaccard(signature(a), signature(b)) - exact)).toBeLessThan(0.15);
    expect(estimateJaccard(signature(a), signature(a))).toBe(1);
    expect(estimateJaccard(signature(seq(100)), signature(seq(100, "q")))).toBeLessThan(0.1);
    expect(jaccard(new Set(), new Set())).toBe(0);
  });

  it("puts near-duplicates in a shared band and unrelated code in none", () => {
    const a = seq(300);
    const near = [...seq(295), "x1", "x2", "x3", "x4", "x5"];
    const share = (x: string[], y: string[]) => bandKeys(signature(x)).some((k) => bandKeys(signature(y)).includes(k));
    expect(share(a, near)).toBe(true);
    expect(share(a, seq(300, "other"))).toBe(false);
    expect(bandKeys(signature(a))).toHaveLength(16);
    expect(bandKeys(signature(a))[0]).toMatch(/^0:[0-9a-f]{32}$/);
  });

  it("round-trips signatures through blobs", () => {
    const sig = signature(seq(50));
    expect(decodeSig(encodeSig(sig))).toEqual(sig);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/minhash.test.ts`
Expected: FAIL with `Failed to load url ../src/index/minhash.js`.

- [ ] **Step 3: Implement**

`sindri/src/index/minhash.ts`:

```ts
// MinHash + LSH over token shingles (spec §6.2 clones): near-duplicate candidates
// come from shared bands, never from comparing every pair.
export const SHINGLE = 5;
export const PERMS = 64;
export const BANDS = 16;
const ROWS = PERMS / BANDS;

export function shingles(tokens: string[], k: number = SHINGLE): Set<string> {
  if (tokens.length === 0) return new Set();
  if (tokens.length < k) return new Set([tokens.join(" ")]);
  const out = new Set<string>();
  for (let i = 0; i + k <= tokens.length; i++) out.add(tokens.slice(i, i + k).join(" "));
  return out;
}

// 32-bit FNV-1a with a per-permutation seed mixed into the offset basis.
function fnv1a(text: string, seed: number): number {
  let h = (0x811c9dc5 ^ Math.imul(seed + 1, 0x9e3779b1)) >>> 0;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return h >>> 0;
}

export function signature(tokens: string[]): Uint32Array {
  const sig = new Uint32Array(PERMS).fill(0xffffffff);
  for (const s of shingles(tokens)) {
    for (let p = 0; p < PERMS; p++) {
      const h = fnv1a(s, p);
      if (h < sig[p]) sig[p] = h;
    }
  }
  return sig;
}

export function bandKeys(sig: Uint32Array): string[] {
  const keys: string[] = [];
  for (let b = 0; b < BANDS; b++) {
    const rows = Array.from(sig.subarray(b * ROWS, (b + 1) * ROWS), (v) => v.toString(16).padStart(8, "0"));
    keys.push(`${b}:${rows.join("")}`);
  }
  return keys;
}

export function estimateJaccard(a: Uint32Array, b: Uint32Array): number {
  let same = 0;
  for (let i = 0; i < PERMS; i++) if (a[i] === b[i]) same++;
  return same / PERMS;
}

export function jaccard(a: Set<string>, b: Set<string>): number {
  if (a.size === 0 && b.size === 0) return 0;
  let inter = 0;
  for (const x of a) if (b.has(x)) inter++;
  return inter / (a.size + b.size - inter);
}

export function encodeSig(sig: Uint32Array): Buffer {
  return Buffer.from(sig.buffer, sig.byteOffset, sig.byteLength);
}

export function decodeSig(buf: Buffer): Uint32Array {
  return new Uint32Array(buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength));
}
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/index/minhash.ts sindri/tests/minhash.test.ts
git commit -m "feat: sindri MinHash signatures and LSH bands"
```

---

### Task 5: Index database, dependency layer, incremental build and `sindri index build|status`

**Files:**
- Create: `sindri/src/index/db.ts`, `sindri/src/index/deps-layer.ts`, `sindri/src/index/build.ts`, `sindri/src/index/commands.ts`
- Modify: `sindri/src/profile/approve.ts` (add `requireApprovedProfile`), `sindri/src/main.ts` (register `index`), `sindri/src/errors.ts`, `sindri/tests/helpers.ts` (add `gitRepo`)
- Test: `sindri/tests/index-db.test.ts`, `sindri/tests/deps-layer.test.ts`, `sindri/tests/index-build.test.ts`

**Interfaces:**
- Consumes: `inventory`, `isSourcePath` (Task 2); `typescriptParser`, `ParsedSymbol` (Task 3); `signature`, `bandKeys`, `encodeSig` (Task 4); `withHeavyLock` (Task 1); `matchesAny` (Task 2); `approvedProfile`, `LoadedProfile` (Plan 2); `ulid` (Plan 2).
- Produces (`db.ts`):
  - `INDEX_SCHEMA_VERSION = 1`; `indexPath(deps, repo: string): string` → `$AW_STATE_DIR/sindri/index/<repo>.db`.
  - `openIndex(file): IndexDb` — creates the schema; a file with another `user_version` is emptied and recreated (the index is derived data, so it's rebuilt, never migrated). `openIndexReadOnly(file): IndexDb | null`.
  - `type Layer = "structure" | "clones" | "deps" | "embeddings" | "graph"`; `type LayerStatus = "ok" | "unavailable" | "disabled"`.
  - Reader functions: `layers(db)`, `meta(db)`, `symbolsByAstHash(db, hash)`, `symbolsByName(db)`, `bandCandidates(db, keys: string[])`, `symbolById(db, id)`, `depRows(db)`, `embeddingRows(db, model)`, `graphEdges(db)`. Row type `SymbolRow` = `ParsedSymbol` minus `tokens`/`text`, plus `id`, `utility: boolean`, `tokenCount`, `minhash: Uint32Array`, `callees: string[]`.
- Produces (`deps-layer.ts`): `PURPOSE_TAGS: Record<string, readonly string[]>`; `tagsFor(name: string): string[]`; `readManifestDeps(manifest: string, text: string): DepRow[]` with `DepRow { manifest; name; version; kind: "prod" | "dev" | "peer"; tags: string[] }`.
- Produces (`build.ts`):
  - `INDEXER_VERSION = "1"`.
  - `interface Providers { embedder: Embedder | null; graph: GraphProvider | null }` (the interfaces arrive in Tasks 6 and 7; this task declares them as `null`-only placeholders: `type Embedder = never; type GraphProvider = never` in `build.ts`, replaced there).
  - `buildIndex(deps, loaded: LoadedProfile, repo: string, o: { full: boolean }, providers: Providers): Promise<BuildReport>` with `BuildReport { repo; commit: string | null; files: { indexed: number; changed: number; removed: number; skipped: number }; symbols: number; layers: Record<Layer, { status: LayerStatus; detail: string }>; ms: number }`.
- Produces (`commands.ts`): `indexCommand: Command` (`build [--repo NAME] [--full] [--json]`, `status [--repo NAME] [--json]`; `query` and `setup` arrive in Tasks 8 and 10).
- Produces (`approve.ts`): `requireApprovedProfile(deps, db): LoadedProfile` — throws `SND-PROFILE-012` when nothing is approved.
- Produces (`tests/helpers.ts`): `gitRepo(files: Record<string, string>): string` — a temp repo with those files committed (author `Tester <tester@example.com>`).

- [ ] **Step 1: Write the failing tests**

Add to `sindri/tests/helpers.ts`:

```ts
import { execFileSync } from "node:child_process";

export function gitRepo(files: Record<string, string>): string {
  const root = tempDir("sindri-repo-");
  for (const [rel, text] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  const g = (...a: string[]) => execFileSync("git", ["-c", "user.name=Tester", "-c", "user.email=tester@example.com", ...a], { cwd: root, stdio: "ignore" });
  g("init", "-q");
  g("add", "-A");
  g("commit", "-qm", "init", "--allow-empty");
  return root;
}
```

`sindri/tests/deps-layer.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { readManifestDeps, tagsFor } from "../src/index/deps-layer.js";

describe("dependency layer", () => {
  it("reads prod, dev and peer deps with purpose tags", () => {
    const text = JSON.stringify({ dependencies: { dayjs: "^1", zod: "^3" }, devDependencies: { vitest: "^2" }, peerDependencies: { react: "^18" } });
    expect(readManifestDeps("package.json", text)).toEqual([
      { manifest: "package.json", name: "dayjs", version: "^1", kind: "prod", tags: ["date"] },
      { manifest: "package.json", name: "zod", version: "^3", kind: "prod", tags: ["validation"] },
      { manifest: "package.json", name: "vitest", version: "^2", kind: "dev", tags: ["test"] },
      { manifest: "package.json", name: "react", version: "^18", kind: "peer", tags: [] },
    ]);
  });

  it("returns nothing for invalid or empty manifests", () => {
    expect(readManifestDeps("p/package.json", "{not json")).toEqual([]);
    expect(readManifestDeps("p/package.json", "[]")).toEqual([]);
    expect(readManifestDeps("p/package.json", JSON.stringify({ dependencies: { a: 1 } }))).toEqual([]);
  });

  it("tags known packages, including scoped ones by bare name", () => {
    expect(tagsFor("moment")).toEqual(["date"]);
    expect(tagsFor("@types/node")).toEqual([]);
    expect(tagsFor("left-pad")).toEqual([]);
  });
});
```

`sindri/tests/index-db.test.ts`:

```ts
import Database from "better-sqlite3";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { INDEX_SCHEMA_VERSION, indexPath, openIndex, openIndexReadOnly } from "../src/index/db.js";
import { makeDeps, tempDir } from "./helpers.js";

describe("index db", () => {
  it("lives under the sindri state dir, one file per repo", () => {
    const d = makeDeps({ env: { AW_STATE_DIR: "/s" } });
    expect(indexPath(d, "toolkit")).toBe("/s/sindri/index/toolkit.db");
  });

  it("creates the schema, and recreates a file from another indexer version", () => {
    const file = path.join(tempDir(), "x.db");
    const db = openIndex(file);
    expect(db.pragma("user_version", { simple: true })).toBe(INDEX_SCHEMA_VERSION);
    db.prepare("INSERT INTO files (path, hash, size) VALUES ('a.ts', 'h', 1)").run();
    db.close();
    const raw = new Database(file);
    raw.pragma("user_version = 99");
    raw.close();
    const again = openIndex(file);
    expect(again.prepare("SELECT COUNT(*) AS n FROM files").get()).toEqual({ n: 0 });
    again.close();
    expect(openIndexReadOnly(path.join(tempDir(), "missing.db"))).toBeNull();
  });
});
```

`sindri/tests/index-build.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { Deps } from "../src/deps.js";
import { buildIndex } from "../src/index/build.js";
import { bandCandidates, depRows, indexPath, layers, meta, openIndexReadOnly, symbolsByAstHash } from "../src/index/db.js";
import { runCli } from "../src/main.js";
import { loadProfile, type LoadedProfile } from "../src/profile/load.js";
import { gitRepo, makeDeps, tempDir } from "./helpers.js";

afterEach(() => vi.restoreAllMocks());

const FILES = {
  "src/a.ts": "export function add(a: number, b: number): number {\n  return a + b;\n}\n",
  "src/b.ts": "export function sum(x: number, y: number): number {\n  return x + y;\n}\n",
  "src/util/strings.ts": "export const trim = (s: string) => s.trim();\n",
  "package.json": JSON.stringify({ dependencies: { dayjs: "^1" } }),
  ".env.local.ts": "export const k = 1;\n",
};

function profileFor(root: string, extra = ""): LoadedProfile {
  const dir = tempDir("sindri-prof-");
  fs.mkdirSync(path.join(dir, "repos"));
  fs.writeFileSync(path.join(dir, "profile.yaml"), `schemaVersion: 1\nuser: me\nhosts:\n  active: test-host\ntracker:\n  type: plan-file\n  repo: r\nrepos:\n  - r\nindex:\n  utilityGlobs:\n    - "src/util/**"\n${extra}`);
  fs.writeFileSync(path.join(dir, "repos/r.yaml"), `schemaVersion: 1\nname: r\npath: ${root}\n`);
  const r = loadProfile(dir);
  if (!r.ok) throw new Error(JSON.stringify(r.issues));
  return r.value;
}

const none = { embedder: null, graph: null };

describe("buildIndex", () => {
  it("indexes structure, clones and deps, and reports disabled layers", async () => {
    const root = gitRepo(FILES);
    const d: Deps = makeDeps();
    const report = await buildIndex(d, profileFor(root), "r", { full: false }, none);
    expect(report.files).toEqual({ indexed: 4, changed: 3, removed: 0, skipped: 1 });
    expect(report.symbols).toBe(3);
    expect(report.layers.structure.status).toBe("ok");
    expect(report.layers.embeddings).toEqual({ status: "disabled", detail: "no embedder configured" });
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const addHash = (db.prepare("SELECT ast_hash FROM symbols WHERE name = 'add'").get() as { ast_hash: string }).ast_hash;
    const add = symbolsByAstHash(db, addHash);
    expect(add.map((s) => s.name).sort()).toEqual(["add", "sum"]);
    expect(add.find((s) => s.name === "add")?.utility).toBe(false);
    const trim = db.prepare("SELECT utility FROM symbols WHERE name = 'trim'").get() as { utility: number };
    expect(trim.utility).toBe(1);
    expect(depRows(db)).toEqual([{ manifest: "package.json", name: "dayjs", version: "^1", kind: "prod", tags: ["date"] }]);
    expect(bandCandidates(db, [])).toEqual([]);
    expect(meta(db).commit).toMatch(/^[0-9a-f]{40}$/);
    expect(layers(db).map((l) => l.layer).sort()).toEqual(["clones", "deps", "embeddings", "graph", "structure"]);
    db.close();
  });

  it("rebuilds only changed files, drops removed ones, and --full rebuilds everything", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, none);
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function add(a: number): number {\n  return a;\n}\n");
    fs.rmSync(path.join(root, "src/b.ts"));
    const { execFileSync } = await import("node:child_process");
    execFileSync("git", ["add", "-A"], { cwd: root });
    const second = await buildIndex(d, p, "r", { full: false }, none);
    expect(second.files).toMatchObject({ changed: 1, removed: 1 });
    const third = await buildIndex(d, p, "r", { full: true }, none);
    expect(third.files).toMatchObject({ changed: 2, removed: 0 });
  });

  it("keeps the previous index when a build fails halfway (Review Focus 2)", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, none);
    const before = fs.readFileSync(indexPath(d, "r"));
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function changed() { return 1; }\n");
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to) === indexPath(d, "r")) throw new Error("disk full");
      real(from, to);
    });
    await expect(buildIndex(d, p, "r", { full: false }, none)).rejects.toThrow("disk full");
    expect(fs.readFileSync(indexPath(d, "r"))).toEqual(before);
    expect(fs.readdirSync(path.dirname(indexPath(d, "r"))).filter((n) => n.includes(".tmp-"))).toEqual([]);
  });

  it("refuses an unknown repo", async () => {
    const root = gitRepo(FILES);
    await expect(buildIndex(makeDeps(), profileFor(root), "nope", { full: false }, none)).rejects.toThrow(/SND-PROFILE-004|no repo named nope/);
  });
});

describe("sindri index build | status", () => {
  async function approved(root: string): Promise<Deps> {
    const d = makeDeps({ cwd: root });
    await runCli(["profile", "init", "--ring0"], d);
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    return d;
  }

  it("builds every repo in the approved profile and reports status", async () => {
    const root = gitRepo({ ...FILES, "docs/superpowers/plans/p.md": "# P\n" });
    const d = await approved(root);
    const build = await runCli(["index", "build"], d);
    expect(build.exitCode).toBe(0);
    expect(build.stdout).toMatch(/: 4 files \(3 changed, 0 removed, 1 skipped\), 3 symbols; structure ok, clones ok, deps ok, embeddings disabled, graph disabled/);
    const status = await runCli(["index", "status"], d);
    expect(status.stdout).toContain("structure ok");
    expect(status.stdout).toMatch(/built .* ago at [0-9a-f]{12}/);
    const json = JSON.parse((await runCli(["index", "status", "--json"], d)).stdout);
    expect(json[0].layers.structure.status).toBe("ok");
  });

  it("says when there is no index, no approved profile, or an unknown subcommand", async () => {
    const root = gitRepo({ ...FILES, "docs/superpowers/plans/p.md": "# P\n" });
    const d = await approved(root);
    expect((await runCli(["index", "status"], d)).stderr).toContain("SND-INDEX-404");
    expect((await runCli(["index", "build"], makeDeps())).stderr).toContain("SND-PROFILE-012");
    expect((await runCli(["index", "frob"], d)).stderr).toContain("SND-CLI-002");
    expect((await runCli(["index", "build", "--repo", "zzz"], d)).stderr).toContain("SND-PROFILE-004");
  });

  it("marks a stale index (older than index.maxAgeHours) as needing attention", async () => {
    const root = gitRepo({ ...FILES, "docs/superpowers/plans/p.md": "# P\n" });
    const d = await approved(root);
    await runCli(["index", "build"], d);
    const later = { ...d, now: () => new Date(d.now().getTime() + 48 * 3_600_000) };
    const r = await runCli(["index", "status"], later);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("stale");
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/deps-layer.test.ts tests/index-db.test.ts tests/index-build.test.ts`
Expected: FAIL with `Failed to load url ../src/index/deps-layer.js` (and `db.js`, `build.js`).

- [ ] **Step 3: Implement**

`sindri/src/index/deps-layer.ts`:

```ts
// Dependency layer (spec §6.2): a new dependency whose purpose overlaps an
// existing one is a reinvention signal. Tags cover common overlaps; the
// eval loop can extend the map through a reuse-hint proposal (spec §7.4).
export const PURPOSE_TAGS: Record<string, readonly string[]> = {
  date: ["moment", "dayjs", "date-fns", "luxon"],
  http: ["axios", "got", "node-fetch", "ky", "superagent", "undici"],
  id: ["uuid", "nanoid", "ulid", "cuid", "cuid2"],
  validation: ["zod", "yup", "joi", "ajv", "valibot", "io-ts", "superstruct"],
  utility: ["lodash", "underscore", "ramda", "remeda", "lodash-es"],
  test: ["jest", "vitest", "mocha", "ava", "jasmine"],
  yaml: ["yaml", "js-yaml"],
  "cli-args": ["commander", "yargs", "minimist", "meow", "cac"],
  sqlite: ["better-sqlite3", "sqlite3", "sql.js"],
  logging: ["winston", "pino", "bunyan", "loglevel"],
  "deep-equal": ["fast-deep-equal", "deep-equal", "dequal"],
  glob: ["glob", "fast-glob", "globby", "minimatch", "micromatch", "picomatch"],
};

export interface DepRow {
  manifest: string;
  name: string;
  version: string;
  kind: "prod" | "dev" | "peer";
  tags: string[];
}

export function tagsFor(name: string): string[] {
  return Object.entries(PURPOSE_TAGS)
    .filter(([, names]) => names.includes(name))
    .map(([tag]) => tag);
}

const SECTIONS: readonly [string, DepRow["kind"]][] = [["dependencies", "prod"], ["devDependencies", "dev"], ["peerDependencies", "peer"]];

export function readManifestDeps(manifest: string, text: string): DepRow[] {
  let json: unknown;
  try {
    json = JSON.parse(text);
  } catch {
    return [];
  }
  if (json === null || typeof json !== "object" || Array.isArray(json)) return [];
  const out: DepRow[] = [];
  for (const [section, kind] of SECTIONS) {
    const entries = (json as Record<string, unknown>)[section];
    if (entries === null || typeof entries !== "object") continue;
    for (const [name, version] of Object.entries(entries as Record<string, unknown>)) {
      if (typeof version === "string") out.push({ manifest, name, version, kind, tags: tagsFor(name) });
    }
  }
  return out;
}
```

`sindri/src/index/db.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";

import { stateDir, type Deps } from "../deps.js";
import type { DepRow } from "./deps-layer.js";
import { decodeSig } from "./minhash.js";

export type IndexDb = Database.Database;
export type Layer = "structure" | "clones" | "deps" | "embeddings" | "graph";
export type LayerStatus = "ok" | "unavailable" | "disabled";
export const LAYERS: readonly Layer[] = ["structure", "clones", "deps", "embeddings", "graph"];

// The index is derived data: a different schema version is rebuilt, not migrated.
export const INDEX_SCHEMA_VERSION = 1;
const SCHEMA = `
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE layers (layer TEXT PRIMARY KEY, stamp TEXT NOT NULL, status TEXT NOT NULL, detail TEXT NOT NULL, built_at TEXT NOT NULL);
CREATE TABLE files (path TEXT PRIMARY KEY, hash TEXT NOT NULL, size INTEGER NOT NULL);
CREATE TABLE symbols (
  id INTEGER PRIMARY KEY,
  file TEXT NOT NULL REFERENCES files(path) ON DELETE CASCADE,
  name TEXT NOT NULL, kind TEXT NOT NULL, start_line INTEGER NOT NULL, end_line INTEGER NOT NULL,
  exported INTEGER NOT NULL, utility INTEGER NOT NULL, signature TEXT NOT NULL, ast_hash TEXT NOT NULL,
  token_count INTEGER NOT NULL, complexity INTEGER NOT NULL, callees TEXT NOT NULL, minhash BLOB NOT NULL, body TEXT NOT NULL
);
CREATE INDEX symbols_ast ON symbols(ast_hash);
CREATE INDEX symbols_name ON symbols(name);
CREATE INDEX symbols_file ON symbols(file);
CREATE TABLE bands (key TEXT NOT NULL, symbol_id INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE);
CREATE INDEX bands_key ON bands(key);
CREATE TABLE deps (manifest TEXT NOT NULL, name TEXT NOT NULL, version TEXT NOT NULL, kind TEXT NOT NULL, tags TEXT NOT NULL, PRIMARY KEY (manifest, name));
CREATE TABLE embeddings (symbol_id INTEGER PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE, model TEXT NOT NULL, vector BLOB NOT NULL);
CREATE TABLE graph_nodes (id TEXT PRIMARY KEY, file TEXT, name TEXT, line INTEGER);
CREATE TABLE graph_edges (src TEXT NOT NULL, dst TEXT NOT NULL, relation TEXT NOT NULL, confidence TEXT NOT NULL);
`;

export function indexPath(deps: Deps, repo: string): string {
  return path.join(stateDir(deps), "index", `${repo}.db`);
}

export function openIndex(file: string): IndexDb {
  fs.mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  let db = new Database(file);
  if (db.pragma("user_version", { simple: true }) !== INDEX_SCHEMA_VERSION) {
    db.close();
    fs.rmSync(file, { force: true });
    db = new Database(file);
    db.exec(SCHEMA);
    db.pragma(`user_version = ${INDEX_SCHEMA_VERSION}`);
  }
  db.pragma("foreign_keys = ON");
  fs.chmodSync(file, 0o600);
  return db;
}

export function openIndexReadOnly(file: string): IndexDb | null {
  if (!fs.existsSync(file)) return null;
  return new Database(file, { readonly: true });
}

export interface SymbolRow {
  id: number;
  file: string;
  name: string;
  kind: string;
  startLine: number;
  endLine: number;
  exported: boolean;
  utility: boolean;
  signature: string;
  astHash: string;
  tokenCount: number;
  complexity: number;
  callees: string[];
  minhash: Uint32Array;
  body: string;
}

interface RawSymbol {
  id: number; file: string; name: string; kind: string; start_line: number; end_line: number; exported: number; utility: number;
  signature: string; ast_hash: string; token_count: number; complexity: number; callees: string; minhash: Buffer; body: string;
}

const toRow = (r: RawSymbol): SymbolRow => ({
  id: r.id, file: r.file, name: r.name, kind: r.kind, startLine: r.start_line, endLine: r.end_line, exported: r.exported === 1,
  utility: r.utility === 1, signature: r.signature, astHash: r.ast_hash, tokenCount: r.token_count, complexity: r.complexity,
  callees: JSON.parse(r.callees) as string[], minhash: decodeSig(r.minhash), body: r.body,
});

export function symbolsByAstHash(db: IndexDb, hash: string): SymbolRow[] {
  return (db.prepare("SELECT * FROM symbols WHERE ast_hash = ? ORDER BY file, start_line").all(hash) as RawSymbol[]).map(toRow);
}

export function allSymbols(db: IndexDb): SymbolRow[] {
  return (db.prepare("SELECT * FROM symbols ORDER BY file, start_line").all() as RawSymbol[]).map(toRow);
}

export function symbolById(db: IndexDb, id: number): SymbolRow | null {
  const r = db.prepare("SELECT * FROM symbols WHERE id = ?").get(id) as RawSymbol | undefined;
  return r === undefined ? null : toRow(r);
}

export function bandCandidates(db: IndexDb, keys: string[]): number[] {
  if (keys.length === 0) return [];
  const rows = db.prepare(`SELECT DISTINCT symbol_id FROM bands WHERE key IN (${keys.map(() => "?").join(",")})`).all(...keys) as { symbol_id: number }[];
  return rows.map((r) => r.symbol_id);
}

export function depRows(db: IndexDb): DepRow[] {
  return (db.prepare("SELECT * FROM deps ORDER BY manifest, name").all() as (Omit<DepRow, "tags"> & { tags: string })[]).map((r) => ({
    ...r,
    tags: JSON.parse(r.tags) as string[],
  }));
}

export function embeddingRows(db: IndexDb, model: string): { symbolId: number; vector: Buffer }[] {
  return (db.prepare("SELECT symbol_id, vector FROM embeddings WHERE model = ?").all(model) as { symbol_id: number; vector: Buffer }[]).map((r) => ({
    symbolId: r.symbol_id,
    vector: r.vector,
  }));
}

export function graphEdges(db: IndexDb): { src: string; dst: string; relation: string; confidence: string }[] {
  return db.prepare("SELECT * FROM graph_edges").all() as { src: string; dst: string; relation: string; confidence: string }[];
}

export function layers(db: IndexDb): { layer: Layer; stamp: string; status: LayerStatus; detail: string; builtAt: string }[] {
  return (db.prepare("SELECT * FROM layers ORDER BY layer").all() as { layer: Layer; stamp: string; status: LayerStatus; detail: string; built_at: string }[]).map(
    (r) => ({ layer: r.layer, stamp: r.stamp, status: r.status, detail: r.detail, builtAt: r.built_at }),
  );
}

export function meta(db: IndexDb): { commit: string | null; builtAt: string | null } {
  const get = (k: string) => (db.prepare("SELECT value FROM meta WHERE key = ?").get(k) as { value: string } | undefined)?.value ?? null;
  return { commit: get("commit"), builtAt: get("built_at") };
}
```

`sindri/src/index/build.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import type { LoadedProfile } from "../profile/load.js";
import { type IndexDb, indexPath, type Layer, type LayerStatus, openIndex } from "./db.js";
import { readManifestDeps } from "./deps-layer.js";
import { inventory, isSourcePath, type IndexedFile } from "./files.js";
import { matchesAny } from "./globs.js";
import { withHeavyLock } from "./heavy-lock.js";
import { bandKeys, encodeSig, signature } from "./minhash.js";
import { typescriptParser } from "./parse-ts.js";

// Bump when parsing or hashing changes: every structure/clone row is rebuilt.
export const INDEXER_VERSION = "1";
const STRUCTURE_STAMP = `parse-ts@${INDEXER_VERSION}`;

// Replaced by the real interfaces in Tasks 6 (Embedder) and 7 (GraphProvider).
export type Embedder = never;
export type GraphProvider = never;
export interface Providers {
  embedder: Embedder | null;
  graph: GraphProvider | null;
}

export interface BuildReport {
  repo: string;
  commit: string | null;
  files: { indexed: number; changed: number; removed: number; skipped: number };
  symbols: number;
  layers: Record<Layer, { status: LayerStatus; detail: string }>;
  ms: number;
}

function setLayer(db: IndexDb, layer: Layer, stamp: string, status: LayerStatus, detail: string, now: Date): void {
  db.prepare(
    "INSERT INTO layers (layer, stamp, status, detail, built_at) VALUES (?, ?, ?, ?, ?) ON CONFLICT(layer) DO UPDATE SET stamp = excluded.stamp, status = excluded.status, detail = excluded.detail, built_at = excluded.built_at",
  ).run(layer, stamp, status, detail, now.toISOString());
}

function stampOf(db: IndexDb, layer: Layer): string | null {
  return (db.prepare("SELECT stamp FROM layers WHERE layer = ?").get(layer) as { stamp: string } | undefined)?.stamp ?? null;
}

function writeStructure(db: IndexDb, files: IndexedFile[], utilityGlobs: readonly string[]): { changed: number; removed: number } {
  if (stampOf(db, "structure") !== STRUCTURE_STAMP) db.exec("DELETE FROM files");
  const known = new Map((db.prepare("SELECT path, hash FROM files").all() as { path: string; hash: string }[]).map((r) => [r.path, r.hash]));
  const sources = files.filter((f) => isSourcePath(f.path));
  const live = new Set(sources.map((f) => f.path));
  const removed = [...known.keys()].filter((p) => !live.has(p));
  const changed = sources.filter((f) => known.get(f.path) !== f.hash);
  const insertSymbol = db.prepare(
    `INSERT INTO symbols (file, name, kind, start_line, end_line, exported, utility, signature, ast_hash, token_count, complexity, callees, minhash, body)
     VALUES (@file, @name, @kind, @startLine, @endLine, @exported, @utility, @signature, @astHash, @tokenCount, @complexity, @callees, @minhash, @body)`,
  );
  const insertBand = db.prepare("INSERT INTO bands (key, symbol_id) VALUES (?, ?)");
  db.transaction(() => {
    for (const p of removed) db.prepare("DELETE FROM files WHERE path = ?").run(p);
    for (const f of changed) {
      db.prepare("DELETE FROM files WHERE path = ?").run(f.path);
      db.prepare("INSERT INTO files (path, hash, size) VALUES (?, ?, ?)").run(f.path, f.hash, f.size);
      for (const s of typescriptParser.parse(f.path, f.text)) {
        const sig = signature(s.tokens);
        const { lastInsertRowid } = insertSymbol.run({
          ...s, exported: s.exported ? 1 : 0, utility: matchesAny(f.path, utilityGlobs) ? 1 : 0, tokenCount: s.tokens.length,
          callees: JSON.stringify(s.callees), minhash: encodeSig(sig), body: s.text,
        });
        for (const key of bandKeys(sig)) insertBand.run(key, Number(lastInsertRowid));
      }
    }
  })();
  return { changed: changed.length, removed: removed.length };
}

function writeDeps(db: IndexDb, files: IndexedFile[]): void {
  const rows = files.filter((f) => f.path === "package.json" || f.path.endsWith("/package.json")).flatMap((f) => readManifestDeps(f.path, f.text));
  db.transaction(() => {
    db.exec("DELETE FROM deps");
    for (const r of rows) db.prepare("INSERT OR REPLACE INTO deps (manifest, name, version, kind, tags) VALUES (?, ?, ?, ?, ?)").run(r.manifest, r.name, r.version, r.kind, JSON.stringify(r.tags));
  })();
}

export async function buildIndex(deps: Deps, loaded: LoadedProfile, repo: string, o: { full: boolean }, providers: Providers): Promise<BuildReport> {
  const cfg = loaded.repos[repo];
  if (cfg === undefined) throw new SindriError("SND-PROFILE-004", `no repo named ${repo}`);
  const ix = loaded.profile.index;
  return withHeavyLock(deps, `index-build:${repo}`, 600_000, async () => {
    const started = Date.now();
    const inv = await inventory(deps.git, cfg.path, {
      denyPaths: [...ix.denyPaths, ...cfg.index.denyPaths],
      maxFileKB: ix.maxFileKB,
      maxTotalMB: ix.maxTotalMB,
      select: (p) => isSourcePath(p) || p === "package.json" || p.endsWith("/package.json"),
    });
    const live = indexPath(deps, repo);
    // Build into a temp copy and rename it over the live file: a crash or a full
    // disk leaves the previous complete index in place (Review Focus 2).
    const tmp = `${live}.tmp-${ulid(deps.now())}`;
    fs.mkdirSync(path.dirname(live), { recursive: true, mode: 0o700 });
    if (!o.full && fs.existsSync(live)) fs.copyFileSync(live, tmp);
    const db = openIndex(tmp);
    try {
      const now = deps.now();
      const { changed, removed } = writeStructure(db, inv.files, ix.utilityGlobs);
      setLayer(db, "structure", STRUCTURE_STAMP, "ok", "TypeScript compiler API", now);
      setLayer(db, "clones", STRUCTURE_STAMP, "ok", "AST hash + MinHash/LSH", now);
      writeDeps(db, inv.files);
      setLayer(db, "deps", `deps@${INDEXER_VERSION}`, "ok", "package.json manifests", now);
      // Tasks 6 and 7 replace these two lines with the provider-backed layers.
      setLayer(db, "embeddings", "none", "disabled", "no embedder configured", now);
      setLayer(db, "graph", "none", "disabled", "no graph provider configured", now);
      const head = await deps.git.run(["rev-parse", "HEAD"], cfg.path);
      const commit = head.ok ? head.stdout.trim() : null;
      db.prepare("INSERT OR REPLACE INTO meta (key, value) VALUES ('commit', ?), ('built_at', ?)").run(commit ?? "", now.toISOString());
      const symbols = (db.prepare("SELECT COUNT(*) AS n FROM symbols").get() as { n: number }).n;
      const report: BuildReport = {
        repo,
        commit,
        files: { indexed: inv.files.length, changed, removed, skipped: inv.skipped.length },
        symbols,
        layers: Object.fromEntries((db.prepare("SELECT layer, status, detail FROM layers").all() as { layer: Layer; status: LayerStatus; detail: string }[]).map((l) => [l.layer, { status: l.status, detail: l.detail }])) as BuildReport["layers"],
        ms: Date.now() - started,
      };
      db.close();
      fs.renameSync(tmp, live);
      return report;
    } catch (e) {
      if (db.open) db.close();
      fs.rmSync(tmp, { force: true });
      throw e;
    }
  });
}
```

`sindri/src/index/commands.ts`:

```ts
import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ledgerPath, openLedger } from "../ledger/db.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { requireApprovedProfile } from "../profile/approve.js";
import type { LoadedProfile } from "../profile/load.js";
import { buildIndex, type BuildReport } from "./build.js";
import { indexPath, LAYERS, layers, meta, openIndexReadOnly } from "./db.js";

function approvedOrThrow(deps: Deps): LoadedProfile {
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    return requireApprovedProfile(deps, db);
  } finally {
    db.close();
  }
}

function reposOf(loaded: LoadedProfile, only: string | undefined): string[] {
  if (only === undefined) return Object.keys(loaded.repos).sort();
  if (!(only in loaded.repos)) throw new SindriError("SND-PROFILE-004", `no repo named ${only}`);
  return [only];
}

const layerLine = (l: Record<string, { status: string }>): string => LAYERS.map((n) => `${n} ${l[n].status}`).join(", ");

async function build(args: string[], deps: Deps): Promise<CommandResult> {
  const { values } = parseFlags(args, { repo: { type: "string" }, full: { type: "boolean" }, json: { type: "boolean" } });
  const loaded = approvedOrThrow(deps);
  const reports: BuildReport[] = [];
  for (const repo of reposOf(loaded, values.repo)) {
    reports.push(await buildIndex(deps, loaded, repo, { full: values.full === true }, { embedder: null, graph: null }));
  }
  const text = reports
    .map((r) => `${r.repo}: ${r.files.indexed} files (${r.files.changed} changed, ${r.files.removed} removed, ${r.files.skipped} skipped), ${r.symbols} symbols; ${layerLine(r.layers)} (${(r.ms / 1000).toFixed(1)} s)`)
    .join("\n");
  return success(text, reports, values.json === true);
}

function age(ms: number): string {
  const h = Math.floor(ms / 3_600_000);
  return h >= 1 ? `${h} h` : `${Math.max(0, Math.floor(ms / 60_000))} min`;
}

function status(args: string[], deps: Deps): CommandResult {
  const { values } = parseFlags(args, { repo: { type: "string" }, json: { type: "boolean" } });
  const loaded = approvedOrThrow(deps);
  const out: { repo: string; commit: string | null; builtAt: string | null; stale: boolean; layers: Record<string, { status: string; detail: string }> }[] = [];
  for (const repo of reposOf(loaded, values.repo)) {
    const db = openIndexReadOnly(indexPath(deps, repo));
    if (db === null) throw new SindriError("SND-INDEX-404", `no index for ${repo}`);
    const m = meta(db);
    const ls = Object.fromEntries(layers(db).map((l) => [l.layer, { status: l.status, detail: l.detail }]));
    db.close();
    const ageMs = m.builtAt === null ? Number.POSITIVE_INFINITY : deps.now().getTime() - Date.parse(m.builtAt);
    out.push({ repo, commit: m.commit, builtAt: m.builtAt, stale: ageMs > loaded.profile.index.maxAgeHours * 3_600_000, layers: ls });
  }
  const text = out
    .map((r) => `${r.repo}: built ${age(deps.now().getTime() - Date.parse(r.builtAt ?? ""))} ago at ${(r.commit ?? "unknown").slice(0, 12)}${r.stale ? " (stale: sindri index build)" : ""}; ${layerLine(r.layers)}`)
    .join("\n");
  return success(text, out, values.json === true, out.some((r) => r.stale) ? 1 : 0);
}

export const indexCommand: Command = async (args, deps) => {
  const [sub, ...rest] = args;
  const json = rest.includes("--json");
  try {
    if (sub === "build") return await build(rest, deps);
    if (sub === "status") return status(rest, deps);
    return failure("SND-CLI-002", `unknown index subcommand: ${sub ?? "(none)"}; use build or status`, json, { fix: "sindri index --help" });
  } catch (e) {
    return fromError(e, json);
  }
};
```

In `sindri/src/profile/approve.ts`, add:

```ts
export function requireApprovedProfile(deps: Deps, db: Ledger): LoadedProfile {
  const p = approvedProfile(deps, db);
  if (p === null) throw new SindriError("SND-PROFILE-012", "no approved profile", { fix: "sindri profile approve" });
  return p;
}
```

Register in `sindri/src/main.ts`:

```ts
import { indexCommand } from "./index/commands.js";

  index: {
    summary: "Build and inspect the per-repo code index",
    usage: "Usage:\n  sindri index build [--repo NAME] [--full] [--json]\n  sindri index status [--repo NAME] [--json]",
    run: indexCommand,
  },
```

Add to `ERRORS`:

```ts
  "SND-PROFILE-012": { summary: "No profile has been approved yet.", fix: "sindri profile approve" },
  "SND-INDEX-404": { summary: "No index has been built for this repo.", fix: "sindri index build" },
```

`files.indexed` counts every inventoried file (sources and manifests); `changed` and `removed` count source files.

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npm run gen && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests docs/sindri/errors.md
git commit -m "feat: sindri code index database, dependency layer and incremental build"
```

---

### Task 6: Embeddings layer (Ollama on loopback only)

**Files:**
- Create: `sindri/src/index/embed.ts`
- Modify: `sindri/src/index/build.ts` (real `Embedder` type, `embedLayer`), `sindri/src/index/commands.ts` (construct the embedder from the profile), `sindri/src/errors.ts`
- Test: `sindri/tests/embed.test.ts`, `sindri/tests/index-build.test.ts` (embedding cases)

**Interfaces:**
- Consumes: `isLoopbackUrl` (Task 1); `IndexDb`, `setLayer`, `stampOf` (Task 5, module-private in `build.ts`).
- Produces (`embed.ts`):
  - `type FetchLike = (url: string, init: { method: "POST"; headers: Record<string, string>; body: string; signal: AbortSignal }) => Promise<{ ok: boolean; status: number; json(): Promise<unknown> }>`.
  - `interface Embedder { model: string; embed(texts: string[]): Promise<Float32Array[]> }`.
  - `makeOllamaEmbedder(o: { url: string; model: string; fetch: FetchLike; timeoutMs?: number }): Embedder` — throws `SND-INDEX-005` for a non-loopback URL **before** any request; request errors throw `SND-INDEX-006`.
  - `embeddingText(s: { name: string; signature: string; body: string }): string` (≤ 2000 characters); `encodeVec(v: Float32Array): Buffer`; `decodeVec(b: Buffer): Float32Array`; `cosine(a: Float32Array, b: Float32Array): number`.
- Produces (`build.ts`): `Embedder` is now the real interface; the embeddings layer is `ok` (stamp `<model>@<INDEXER_VERSION>`), `disabled` (no embedder) or `unavailable` (with the error text). Only symbols without a vector for the current model are embedded; a model change re-embeds everything. Classes are not embedded.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/embed.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { cosine, decodeVec, embeddingText, encodeVec, makeOllamaEmbedder, type FetchLike } from "../src/index/embed.js";

function fakeFetch(answer: (body: { model: string; input: string[] }) => { ok: boolean; status: number; json: unknown }): FetchLike & { calls: string[] } {
  const calls: string[] = [];
  const f = (async (url, init) => {
    calls.push(url);
    const a = answer(JSON.parse(init.body) as { model: string; input: string[] });
    return { ok: a.ok, status: a.status, json: async () => a.json };
  }) as FetchLike & { calls: string[] };
  f.calls = calls;
  return f;
}

describe("Ollama embedder (Review Focus 3)", () => {
  it("posts batches of 32 to /api/embed on loopback and returns vectors", async () => {
    const fetch = fakeFetch((b) => ({ ok: true, status: 200, json: { embeddings: b.input.map((_, i) => [i, 1, 0]) } }));
    const e = makeOllamaEmbedder({ url: "http://127.0.0.1:11434/", model: "nomic-embed-text", fetch });
    const out = await e.embed(Array.from({ length: 40 }, (_, i) => `t${i}`));
    expect(out).toHaveLength(40);
    expect(Array.from(out[33])).toEqual([1, 1, 0]);
    expect(fetch.calls).toEqual(["http://127.0.0.1:11434/api/embed", "http://127.0.0.1:11434/api/embed"]);
    expect(e.model).toBe("nomic-embed-text");
  });

  it("refuses a non-loopback URL before sending anything", () => {
    const fetch = fakeFetch(() => ({ ok: true, status: 200, json: {} }));
    expect(() => makeOllamaEmbedder({ url: "https://api.example.com", model: "m", fetch })).toThrow(SindriError);
    expect(fetch.calls).toEqual([]);
  });

  it("reports a missing model, a down server and a malformed answer as SND-INDEX-006", async () => {
    const missing = makeOllamaEmbedder({ url: "http://localhost:11434", model: "m", fetch: fakeFetch(() => ({ ok: false, status: 404, json: {} })) });
    await expect(missing.embed(["a"])).rejects.toThrow("embedding request failed (HTTP 404); is the model pulled? (sindri index setup)");
    const down = makeOllamaEmbedder({ url: "http://localhost:11434", model: "m", fetch: async () => { throw new Error("connect ECONNREFUSED"); } });
    await expect(down.embed(["a"])).rejects.toThrow("embedding server unreachable: connect ECONNREFUSED");
    const bad = makeOllamaEmbedder({ url: "http://localhost:11434", model: "m", fetch: fakeFetch(() => ({ ok: true, status: 200, json: { nope: 1 } })) });
    await expect(bad.embed(["a"])).rejects.toThrow("embedding server answered in an unexpected shape");
    expect(await bad.embed([])).toEqual([]);
  });

  it("encodes vectors, computes cosine and caps the embedded text", () => {
    const v = new Float32Array([1, 2, 3]);
    expect(Array.from(decodeVec(encodeVec(v)))).toEqual([1, 2, 3]);
    expect(cosine(new Float32Array([1, 0]), new Float32Array([1, 0]))).toBeCloseTo(1);
    expect(cosine(new Float32Array([1, 0]), new Float32Array([0, 1]))).toBeCloseTo(0);
    expect(cosine(new Float32Array([0, 0]), new Float32Array([1, 1]))).toBe(0);
    expect(embeddingText({ name: "f", signature: "(a)", body: "x".repeat(5000) })).toHaveLength(2000);
  });
});
```

Add to `sindri/tests/index-build.test.ts`:

```ts
import type { Embedder } from "../src/index/embed.js";

function fakeEmbedder(model = "m1"): Embedder & { seen: string[] } {
  const seen: string[] = [];
  return {
    model,
    seen,
    embed: async (texts) => {
      seen.push(...texts);
      return texts.map((t) => new Float32Array([t.length, 1]));
    },
  };
}

describe("embeddings layer", () => {
  it("embeds new symbols only, re-embeds on a model change, and degrades on failure", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    const e1 = fakeEmbedder();
    const first = await buildIndex(d, p, "r", { full: false }, { embedder: e1, graph: null });
    expect(first.layers.embeddings).toEqual({ status: "ok", detail: "m1 on loopback" });
    expect(e1.seen).toHaveLength(3);
    fs.writeFileSync(path.join(root, "src/c.ts"), "export function c() { return 3; }\n");
    (await import("node:child_process")).execFileSync("git", ["add", "-A"], { cwd: root });
    const e2 = fakeEmbedder();
    await buildIndex(d, p, "r", { full: false }, { embedder: e2, graph: null });
    expect(e2.seen).toEqual([expect.stringContaining("function c()")]);
    const e3 = fakeEmbedder("m2");
    await buildIndex(d, p, "r", { full: false }, { embedder: e3, graph: null });
    expect(e3.seen).toHaveLength(4);
    const broken: Embedder = { model: "m2", embed: async () => { throw new Error("embedding server unreachable: down"); } };
    fs.writeFileSync(path.join(root, "src/d.ts"), "export function d() { return 4; }\n");
    (await import("node:child_process")).execFileSync("git", ["add", "-A"], { cwd: root });
    const degraded = await buildIndex(d, p, "r", { full: false }, { embedder: broken, graph: null });
    expect(degraded.layers.embeddings).toEqual({ status: "unavailable", detail: "embedding server unreachable: down" });
    expect(degraded.layers.structure.status).toBe("ok");
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/embed.test.ts tests/index-build.test.ts`
Expected: FAIL with `Failed to load url ../src/index/embed.js`.

- [ ] **Step 3: Implement**

`sindri/src/index/embed.ts`:

```ts
import { z } from "zod";

import { SindriError } from "../errors.js";
import { isLoopbackUrl } from "./loopback.js";

export type FetchLike = (
  url: string,
  init: { method: "POST"; headers: Record<string, string>; body: string; signal: AbortSignal },
) => Promise<{ ok: boolean; status: number; json(): Promise<unknown> }>;

export interface Embedder {
  model: string;
  embed(texts: string[]): Promise<Float32Array[]>;
}

const BATCH = 32;
const TEXT_CAP = 2000;
const Answer = z.object({ embeddings: z.array(z.array(z.number())) });

// Spec §6.2 offline guarantee: code goes only to a model on this machine.
export function makeOllamaEmbedder(o: { url: string; model: string; fetch: FetchLike; timeoutMs?: number }): Embedder {
  if (!isLoopbackUrl(o.url)) throw new SindriError("SND-INDEX-005", `embedding URL ${o.url} is not loopback; the index never sends code off the machine`);
  const endpoint = `${o.url.replace(/\/+$/, "")}/api/embed`;
  const timeoutMs = o.timeoutMs ?? 30_000;
  return {
    model: o.model,
    async embed(texts) {
      const out: Float32Array[] = [];
      for (let i = 0; i < texts.length; i += BATCH) {
        const input = texts.slice(i, i + BATCH);
        let res: Awaited<ReturnType<FetchLike>>;
        try {
          res = await o.fetch(endpoint, {
            method: "POST",
            headers: { "content-type": "application/json" },
            body: JSON.stringify({ model: o.model, input }),
            signal: AbortSignal.timeout(timeoutMs),
          });
        } catch (e) {
          throw new SindriError("SND-INDEX-006", `embedding server unreachable: ${(e as Error).message}`);
        }
        if (!res.ok) throw new SindriError("SND-INDEX-006", `embedding request failed (HTTP ${res.status}); is the model pulled? (sindri index setup)`);
        const parsed = Answer.safeParse(await res.json());
        if (!parsed.success || parsed.data.embeddings.length !== input.length) {
          throw new SindriError("SND-INDEX-006", "embedding server answered in an unexpected shape");
        }
        out.push(...parsed.data.embeddings.map((v) => Float32Array.from(v)));
      }
      return out;
    },
  };
}

export function embeddingText(s: { name: string; signature: string; body: string }): string {
  return `${s.name}${s.signature}\n${s.body}`.slice(0, TEXT_CAP);
}

export function encodeVec(v: Float32Array): Buffer {
  return Buffer.from(v.buffer, v.byteOffset, v.byteLength);
}

export function decodeVec(b: Buffer): Float32Array {
  return new Float32Array(b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength));
}

export function cosine(a: Float32Array, b: Float32Array): number {
  let dot = 0;
  let na = 0;
  let nb = 0;
  for (let i = 0; i < a.length; i++) {
    dot += a[i] * b[i];
    na += a[i] * a[i];
    nb += b[i] * b[i];
  }
  return na === 0 || nb === 0 ? 0 : dot / Math.sqrt(na * nb);
}
```

In `sindri/src/index/build.ts`:
- Replace `export type Embedder = never;` with `export type { Embedder } from "./embed.js";` and add `import { embeddingText, encodeVec, type Embedder } from "./embed.js";`.
- Add this function after `writeDeps`:

```ts
async function embedLayer(db: IndexDb, embedder: Embedder | null, now: Date): Promise<void> {
  if (embedder === null) {
    setLayer(db, "embeddings", "none", "disabled", "no embedder configured", now);
    return;
  }
  const stamp = `${embedder.model}@${INDEXER_VERSION}`;
  const previous = stampOf(db, "embeddings");
  if (previous !== stamp) db.exec("DELETE FROM embeddings");
  const todo = db
    .prepare("SELECT s.id, s.name, s.signature, s.body FROM symbols s LEFT JOIN embeddings e ON e.symbol_id = s.id WHERE e.symbol_id IS NULL AND s.kind != 'class' ORDER BY s.id")
    .all() as { id: number; name: string; signature: string; body: string }[];
  try {
    const vectors = await embedder.embed(todo.map(embeddingText));
    db.transaction(() => {
      todo.forEach((s, i) => db.prepare("INSERT OR REPLACE INTO embeddings (symbol_id, model, vector) VALUES (?, ?, ?)").run(s.id, embedder.model, encodeVec(vectors[i])));
    })();
    setLayer(db, "embeddings", stamp, "ok", `${embedder.model} on loopback`, now);
  } catch (e) {
    // The other layers stay usable; the next build retries (Review Focus 3).
    setLayer(db, "embeddings", previous ?? "none", "unavailable", (e as Error).message, now);
  }
}
```

- Replace the line `setLayer(db, "embeddings", "none", "disabled", "no embedder configured", now);` in `buildIndex` with `await embedLayer(db, providers.embedder, now);`.

In `sindri/src/index/commands.ts`, build the embedder from the profile and pass it to `buildIndex`:

```ts
import { makeOllamaEmbedder, type Embedder } from "./embed.js";

export function embedderFor(loaded: LoadedProfile): Embedder | null {
  const e = loaded.profile.index.embeddings;
  return e.enabled ? makeOllamaEmbedder({ url: e.url, model: e.model, fetch: globalThis.fetch }) : null;
}
```

and change the `buildIndex(...)` call in `build()` to pass `{ embedder: embedderFor(loaded), graph: null }`. In the CLI test of Task 5 ("builds every repo…"), the ring-0 profile has embeddings enabled and no Ollama runs in tests. So append to that test's ring-0 `profile.yaml`, before approving, `index:\n  embeddings:\n    enabled: false\n` (`fs.appendFileSync` on `$AW_STATE_DIR/profile/profile.yaml`), keeping its `embeddings disabled` expectation.

Add to `ERRORS`:

```ts
  "SND-INDEX-005": { summary: "The embedding URL is not loopback.", fix: "set index.embeddings.url to http://127.0.0.1:11434 (or disable embeddings)" },
  "SND-INDEX-006": { summary: "The local embedding server failed.", fix: "sindri index setup (starts Ollama checks and pulls the model)" },
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npm run gen && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests docs/sindri/errors.md
git commit -m "feat: sindri embeddings layer on a loopback-only Ollama"
```

---

### Task 7: Graph layer (graphify in a network-less sandbox)

**Files:**
- Create: `sindri/src/index/graph.ts`, `sindri/src/index/sandbox-real.ts`, `sindri/src/index/pins.ts`, `sindri/tests/fixtures/graphify/graph.json` (recorded in Step 1)
- Modify: `sindri/src/index/build.ts` (real `GraphProvider`, `graphLayer`), `sindri/src/index/commands.ts` (construct the provider), `sindri/vitest.config.ts` (exclude `sandbox-real.ts`), `sindri/src/errors.ts`
- Test: `sindri/tests/graph.test.ts`, `sindri/tests/real.test.ts` (sandbox smoke test), `sindri/tests/index-build.test.ts` (graph cases)

**Interfaces:**
- Consumes: `inventory` (Task 2); `IndexDb`, `setLayer`, `stampOf` (Task 5).
- Produces (`graph.ts`):
  - `interface GraphData { nodes: { id: string; file: string | null; name: string | null; line: number | null }[]; edges: { src: string; dst: string; relation: string; confidence: string }[] }`.
  - `interface ProcessRunner { run(argv: string[], o: { cwd: string; timeoutMs: number }): Promise<{ code: number; stdout: string; stderr: string }> }`.
  - `interface GraphProvider { version: string; build(snapshotDir: string): Promise<GraphData> }`.
  - `sandboxArgv(platform: NodeJS.Platform, argv: string[], has: (bin: string) => boolean): string[] | null` — `sandbox-exec -p '(version 1)(allow default)(deny network*)' …` on macOS, `bwrap --dev-bind / / --unshare-net --die-with-parent …` on Linux, `null` otherwise (fail closed).
  - `parseGraphJson(text: string): GraphData` — NetworkX node-link (`nodes`, and `links` or `edges`); field names are read from candidate lists, so a graphify release that renames `source_file` → `file` keeps working.
  - `makeGraphifyProvider(o: { bin: string; version: string; runner: ProcessRunner; platform: NodeJS.Platform; has: (bin: string) => boolean }): GraphProvider` — runs `graphify extract <snapshot> --code-only --no-viz` sandboxed; throws `SND-INDEX-007` with no sandbox and `SND-INDEX-008` on a failed run.
- Produces (`sandbox-real.ts`): `realProcessRunner(): ProcessRunner`; `hasBinary(bin: string): boolean`.
- Produces (`pins.ts`): `GRAPHIFY_PIN: string` (the graphifyy version `index setup` installs and `doctor` expects).
- Produces (`build.ts`): the graph layer runs on a **snapshot** of tracked, non-denied files written to a temp dir (spec amendment 3), only when the structure changed, the stamp changed, or there is no graph yet; `ok` (stamp `graphify@<version>`), `disabled` or `unavailable`.

- [ ] **Step 1: Install graphify and record a real `graph.json` fixture**

```bash
uv tool install "graphifyy==$(pip index versions graphifyy 2>/dev/null | sed -n 's/^graphifyy (\(.*\))$/\1/p')" || pipx install graphifyy
graphify --version
```

Pick the pin: the newest `graphifyy` release that is **at least 14 days old** (`pip index versions graphifyy` lists them; check dates on https://pypi.org/project/graphifyy/#history). If the installed version is newer, reinstall that one with `uv tool install graphifyy==<pin>`. Then record the fixture from a tiny two-file repo:

```bash
FIX="$(mktemp -d)"
printf 'export function a() { return b(); }\nimport { b } from "./b";\n' > "$FIX/a.ts"
printf 'export function b() { return 1; }\n' > "$FIX/b.ts"
sandbox-exec -p '(version 1)(allow default)(deny network*)' graphify extract "$FIX" --code-only --no-viz
mkdir -p sindri/tests/fixtures/graphify
cp "$FIX/graphify-out/graph.json" sindri/tests/fixtures/graphify/graph.json
jq '{nodes: (.nodes | length), links: ((.links // .edges) | length), node_keys: (.nodes[0] | keys), link_keys: ((.links // .edges)[0] | keys)}' sindri/tests/fixtures/graphify/graph.json
```

Expected: at least 2 nodes and 1 link. Note the printed `node_keys` and `link_keys`. If a key that names the file, the symbol name, the line, the relation or the confidence is not already in `parseGraphJson`'s candidate lists (Step 4), add it there. Don't rename the fixture's keys. Write the pinned version into `sindri/src/index/pins.ts` (Step 4).

- [ ] **Step 2: Write the failing tests**

`sindri/tests/graph.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { makeGraphifyProvider, parseGraphJson, sandboxArgv, type ProcessRunner } from "../src/index/graph.js";
import { tempDir } from "./helpers.js";

const FIXTURE = path.resolve(import.meta.dirname, "fixtures/graphify/graph.json");

describe("parseGraphJson", () => {
  it("reads the recorded graphify fixture", () => {
    const g = parseGraphJson(fs.readFileSync(FIXTURE, "utf8"));
    expect(g.nodes.length).toBeGreaterThanOrEqual(2);
    expect(g.edges.length).toBeGreaterThanOrEqual(1);
    expect(g.nodes.some((n) => n.file !== null && n.file.endsWith("a.ts"))).toBe(true);
  });

  it("accepts the field-name variants and defaults missing fields", () => {
    const g = parseGraphJson(JSON.stringify({
      nodes: [{ id: 1, label: "f", source_file: "a.ts", source_location: "a.ts:12" }, { id: "n2", name: "g", file: "b.ts", line: 3 }, { id: "n3" }],
      edges: [{ source: 1, target: "n2", relation: "calls", confidence: "EXTRACTED" }, { source: "n2", target: "n3", type: "imports" }, { source: "n3", target: 1 }],
    }));
    expect(g.nodes).toEqual([
      { id: "1", file: "a.ts", name: "f", line: 12 },
      { id: "n2", file: "b.ts", name: "g", line: 3 },
      { id: "n3", file: null, name: null, line: null },
    ]);
    expect(g.edges).toEqual([
      { src: "1", dst: "n2", relation: "calls", confidence: "EXTRACTED" },
      { src: "n2", dst: "n3", relation: "imports", confidence: "UNKNOWN" },
      { src: "n3", dst: "1", relation: "related", confidence: "UNKNOWN" },
    ]);
    expect(parseGraphJson("{}")).toEqual({ nodes: [], edges: [] });
    expect(() => parseGraphJson("not json")).toThrow(SindriError);
  });
});

describe("sandboxArgv (Review Focus 4)", () => {
  it("wraps the command in a network-denying sandbox, or refuses", () => {
    expect(sandboxArgv("darwin", ["graphify", "x"], () => true)).toEqual(["sandbox-exec", "-p", "(version 1)(allow default)(deny network*)", "graphify", "x"]);
    expect(sandboxArgv("linux", ["graphify", "x"], () => true)).toEqual(["bwrap", "--dev-bind", "/", "/", "--unshare-net", "--die-with-parent", "graphify", "x"]);
    expect(sandboxArgv("linux", ["graphify"], () => false)).toBeNull();
    expect(sandboxArgv("darwin", ["graphify"], () => false)).toBeNull();
    expect(sandboxArgv("win32", ["graphify"], () => true)).toBeNull();
  });
});

describe("graphify provider", () => {
  function runner(code: number, write: boolean): ProcessRunner & { argv: string[][] } {
    const argv: string[][] = [];
    return {
      argv,
      run: async (a, o) => {
        argv.push(a);
        if (write) {
          fs.mkdirSync(path.join(o.cwd, "graphify-out"), { recursive: true });
          fs.writeFileSync(path.join(o.cwd, "graphify-out", "graph.json"), JSON.stringify({ nodes: [{ id: "a" }], links: [] }));
        }
        return { code, stdout: "", stderr: code === 0 ? "" : "Traceback: boom\nmore" };
      },
    };
  }

  it("runs graphify sandboxed in the snapshot and parses its output", async () => {
    const r = runner(0, true);
    const p = makeGraphifyProvider({ bin: "graphify", version: "1.2.3", runner: r, platform: "darwin", has: () => true });
    const snap = tempDir();
    expect((await p.build(snap)).nodes).toEqual([{ id: "a", file: null, name: null, line: null }]);
    expect(r.argv[0]).toEqual(["sandbox-exec", "-p", "(version 1)(allow default)(deny network*)", "graphify", "extract", snap, "--code-only", "--no-viz"]);
    expect(p.version).toBe("1.2.3");
  });

  it("fails closed without a sandbox, and reports a failed run", async () => {
    const none = makeGraphifyProvider({ bin: "graphify", version: "1", runner: runner(0, true), platform: "linux", has: () => false });
    await expect(none.build(tempDir())).rejects.toThrow(/SND-INDEX-007|no network sandbox/);
    const failing = makeGraphifyProvider({ bin: "graphify", version: "1", runner: runner(1, false), platform: "darwin", has: () => true });
    await expect(failing.build(tempDir())).rejects.toThrow("graphify failed (exit 1): Traceback: boom");
    const noOutput = makeGraphifyProvider({ bin: "graphify", version: "1", runner: runner(0, false), platform: "darwin", has: () => true });
    await expect(noOutput.build(tempDir())).rejects.toThrow("graphify wrote no graph.json");
  });
});
```

Add to `sindri/tests/real.test.ts`:

```ts
import { hasBinary, realProcessRunner } from "../src/index/sandbox-real.js";
import { sandboxArgv } from "../src/index/graph.js";

describe("sandboxed process runner (smoke)", () => {
  it("runs a command, and the sandbox denies network where one exists", async () => {
    const run = realProcessRunner();
    expect((await run.run(["echo", "hi"], { cwd: process.cwd(), timeoutMs: 5000 })).stdout.trim()).toBe("hi");
    const argv = sandboxArgv(process.platform, ["curl", "-sS", "--max-time", "3", "https://example.com"], hasBinary);
    if (argv !== null && hasBinary("curl")) {
      expect((await run.run(argv, { cwd: process.cwd(), timeoutMs: 10_000 })).code).not.toBe(0);
    }
    expect(hasBinary("definitely-not-a-binary-xyz")).toBe(false);
  });
});
```

Add to `sindri/tests/index-build.test.ts`:

```ts
import type { GraphProvider } from "../src/index/graph.js";
import { graphEdges } from "../src/index/db.js";

function fakeGraph(fail = false): GraphProvider & { snapshots: string[][] } {
  const snapshots: string[][] = [];
  return {
    version: "9.9",
    snapshots,
    build: async (dir) => {
      snapshots.push(fs.readdirSync(dir, { recursive: true }).map(String).sort());
      if (fail) throw new Error("graphify failed (exit 1): boom");
      return { nodes: [{ id: "a", file: "src/a.ts", name: "add", line: 1 }], edges: [{ src: "a", dst: "a", relation: "calls", confidence: "EXTRACTED" }] };
    },
  };
}

describe("graph layer", () => {
  it("runs on a snapshot without denied files, only when something changed, and degrades on failure", async () => {
    const root = gitRepo({ ...FILES, "docs/readme.md": "# r\n" });
    const d = makeDeps();
    const p = profileFor(root);
    const g = fakeGraph();
    const first = await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(first.layers.graph).toEqual({ status: "ok", detail: "graphify 9.9, 1 nodes, 1 edges" });
    expect(g.snapshots[0]).toEqual(expect.arrayContaining(["docs/readme.md", "src/a.ts"]));
    expect(g.snapshots[0]).not.toContain(".env.local.ts");
    const db = openIndexReadOnly(indexPath(d, "r"));
    expect(db === null ? [] : graphEdges(db)).toHaveLength(1);
    db?.close();
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(g.snapshots).toHaveLength(1);
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function add2() { return 1; }\n");
    const failed = await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: fakeGraph(true) });
    expect(failed.layers.graph).toEqual({ status: "unavailable", detail: "graphify failed (exit 1): boom" });
    expect(failed.layers.structure.status).toBe("ok");
  });
});
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/graph.test.ts tests/index-build.test.ts tests/real.test.ts`
Expected: FAIL with `Failed to load url ../src/index/graph.js` (and `sandbox-real.js`).

- [ ] **Step 4: Implement**

`sindri/src/index/pins.ts` (use the version chosen in Step 1):

```ts
// graphifyy release installed by `sindri index setup` and expected by `doctor`.
// Upgrades go through a pack-upgrade proposal (spec §7.4, §16 pin policy).
export const GRAPHIFY_PIN = "<the version chosen in Step 1, e.g. 0.4.2>";
```

(The angle-bracket text is replaced by the real version string in this step; the test in Task 10 fails while it's still there.)

`sindri/src/index/graph.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import { makeScrubber } from "../scrub/scrub.js";

export interface GraphData {
  nodes: { id: string; file: string | null; name: string | null; line: number | null }[];
  edges: { src: string; dst: string; relation: string; confidence: string }[];
}

export interface ProcessRunner {
  run(argv: string[], o: { cwd: string; timeoutMs: number }): Promise<{ code: number; stdout: string; stderr: string }>;
}

export interface GraphProvider {
  version: string;
  build(snapshotDir: string): Promise<GraphData>;
}

const MAC_PROFILE = "(version 1)(allow default)(deny network*)";

// Spec §6.2: graphify runs with network denied and fails closed without a sandbox.
export function sandboxArgv(platform: NodeJS.Platform, argv: string[], has: (bin: string) => boolean): string[] | null {
  if (platform === "darwin" && has("sandbox-exec")) return ["sandbox-exec", "-p", MAC_PROFILE, ...argv];
  if (platform === "linux" && has("bwrap")) return ["bwrap", "--dev-bind", "/", "/", "--unshare-net", "--die-with-parent", ...argv];
  return null;
}

type Obj = Record<string, unknown>;
const str = (o: Obj, keys: string[]): string | null => {
  for (const k of keys) if (typeof o[k] === "string" || typeof o[k] === "number") return String(o[k]);
  return null;
};

function lineOf(o: Obj): number | null {
  for (const k of ["line", "lineno", "source_line", "start_line"]) if (typeof o[k] === "number") return o[k] as number;
  const loc = str(o, ["source_location", "location"]);
  const m = loc === null ? null : /:(\d+)$/.exec(loc);
  return m === null ? null : Number(m[1]);
}

// NetworkX node-link JSON. Field names come from candidate lists because
// graphify's schema is undocumented; Task 7 Step 1 records a real fixture.
export function parseGraphJson(text: string): GraphData {
  let json: Obj;
  try {
    json = JSON.parse(text) as Obj;
  } catch {
    throw new SindriError("SND-INDEX-008", "graphify wrote invalid JSON");
  }
  const nodes = (Array.isArray(json.nodes) ? json.nodes : []) as Obj[];
  const links = (Array.isArray(json.links) ? json.links : Array.isArray(json.edges) ? json.edges : []) as Obj[];
  return {
    nodes: nodes.map((n) => ({
      id: String(n.id),
      file: str(n, ["source_file", "file", "path", "filepath"]),
      name: str(n, ["label", "name", "qualname"]),
      line: lineOf(n),
    })),
    edges: links.map((l) => ({
      src: String(l.source),
      dst: String(l.target),
      relation: str(l, ["relation", "type", "label", "kind"]) ?? "related",
      confidence: str(l, ["confidence"]) ?? "UNKNOWN",
    })),
  };
}

const scrubber = makeScrubber();

export function makeGraphifyProvider(o: {
  bin: string;
  version: string;
  runner: ProcessRunner;
  platform: NodeJS.Platform;
  has: (bin: string) => boolean;
}): GraphProvider {
  return {
    version: o.version,
    async build(snapshotDir) {
      const argv = sandboxArgv(o.platform, [o.bin, "extract", snapshotDir, "--code-only", "--no-viz"], o.has);
      if (argv === null) {
        throw new SindriError("SND-INDEX-007", "no network sandbox available (sandbox-exec on macOS, bwrap on Linux); graphify never runs unsandboxed");
      }
      const r = await o.runner.run(argv, { cwd: snapshotDir, timeoutMs: 600_000 });
      if (r.code !== 0) {
        const first = scrubber.scrub(r.stderr.split("\n")[0]).text;
        throw new SindriError("SND-INDEX-008", `graphify failed (exit ${r.code}): ${first}`);
      }
      const out = path.join(snapshotDir, "graphify-out", "graph.json");
      if (!fs.existsSync(out)) throw new SindriError("SND-INDEX-008", "graphify wrote no graph.json");
      return parseGraphJson(fs.readFileSync(out, "utf8"));
    },
  };
}
```

`sindri/src/index/sandbox-real.ts`:

```ts
import { execFile, execFileSync } from "node:child_process";

import type { ProcessRunner } from "./graph.js";

export function realProcessRunner(): ProcessRunner {
  return {
    run: (argv, o) =>
      new Promise((resolve) => {
        execFile(argv[0], argv.slice(1), { cwd: o.cwd, timeout: o.timeoutMs, maxBuffer: 64 * 1024 * 1024, encoding: "utf8" }, (err, stdout, stderr) => {
          const code = err === null ? 0 : typeof err.code === "number" ? err.code : 1;
          resolve({ code, stdout, stderr });
        });
      }),
  };
}

export function hasBinary(bin: string): boolean {
  try {
    execFileSync("/bin/sh", ["-c", `command -v "$1"`, "sh", bin], { stdio: "ignore" });
    return true;
  } catch {
    return false;
  }
}
```

Add `"src/index/sandbox-real.ts"` to the coverage `exclude` list in `sindri/vitest.config.ts` (it has the smoke test above).

In `sindri/src/index/build.ts`:
- Replace `export type GraphProvider = never;` with `export type { GraphProvider } from "./graph.js";` and add `import type { GraphProvider } from "./graph.js";` and `import os from "node:os";`.
- Add after `embedLayer`:

```ts
async function graphLayer(
  db: IndexDb, provider: GraphProvider | null, deps: Deps, repoPath: string, deny: string[], structureChanged: boolean, ix: LoadedProfile["profile"]["index"], now: Date,
): Promise<void> {
  if (provider === null) {
    setLayer(db, "graph", "none", "disabled", "no graph provider configured", now);
    return;
  }
  const stamp = `graphify@${provider.version}`;
  const hasGraph = (db.prepare("SELECT COUNT(*) AS n FROM graph_nodes").get() as { n: number }).n > 0;
  if (!structureChanged && stampOf(db, "graph") === stamp && hasGraph) return;
  // Snapshot of tracked, non-denied files: graphify writes graphify-out/ into the
  // directory it reads, so it never runs on the working tree (spec amendment 3).
  const snap = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-graph-"));
  try {
    const inv = await inventory(deps.git, repoPath, { denyPaths: deny, maxFileKB: ix.maxFileKB, maxTotalMB: ix.maxTotalMB, select: () => true });
    for (const f of inv.files) {
      fs.mkdirSync(path.dirname(path.join(snap, f.path)), { recursive: true });
      fs.writeFileSync(path.join(snap, f.path), f.text);
    }
    const g = await provider.build(snap);
    db.transaction(() => {
      db.exec("DELETE FROM graph_nodes; DELETE FROM graph_edges;");
      for (const n of g.nodes) db.prepare("INSERT OR REPLACE INTO graph_nodes (id, file, name, line) VALUES (?, ?, ?, ?)").run(n.id, n.file, n.name, n.line);
      for (const e of g.edges) db.prepare("INSERT INTO graph_edges (src, dst, relation, confidence) VALUES (?, ?, ?, ?)").run(e.src, e.dst, e.relation, e.confidence);
    })();
    setLayer(db, "graph", stamp, "ok", `graphify ${provider.version}, ${g.nodes.length} nodes, ${g.edges.length} edges`, now);
  } catch (e) {
    setLayer(db, "graph", stampOf(db, "graph") ?? "none", "unavailable", (e as Error).message, now);
  } finally {
    fs.rmSync(snap, { recursive: true, force: true });
  }
}
```

- In `buildIndex`, compute `const deny = [...ix.denyPaths, ...cfg.index.denyPaths];` once (use it for the first `inventory` call too), and replace `setLayer(db, "graph", "none", "disabled", "no graph provider configured", now);` with `await graphLayer(db, providers.graph, deps, cfg.path, deny, changed + removed > 0, ix, now);`.

In `sindri/src/index/commands.ts`, construct the provider when `index.graph` is `graphify` and the binary exists:

```ts
import { makeGraphifyProvider, type GraphProvider } from "./graph.js";
import { GRAPHIFY_PIN } from "./pins.js";
import { hasBinary, realProcessRunner } from "./sandbox-real.js";

export function graphFor(loaded: LoadedProfile, deps: Deps): GraphProvider | null {
  if (loaded.profile.index.graph === "none") return null;
  return makeGraphifyProvider({ bin: "graphify", version: GRAPHIFY_PIN, runner: realProcessRunner(), platform: deps.system.platform, has: hasBinary });
}
```

and pass `graph: graphFor(loaded, deps)` in `build()`. In the Task 5 CLI test, also append `  graph: none\n` under the `index:` key you added in Task 6, so CLI tests never run graphify.

Add to `ERRORS`:

```ts
  "SND-INDEX-007": { summary: "No network sandbox is available for graphify.", fix: "macOS: sandbox-exec ships with the OS; Linux: install bubblewrap (bwrap), or set index.graph: none" },
  "SND-INDEX-008": { summary: "graphify failed or wrote no usable graph.", fix: "sindri index setup, then sindri index build --full" },
```

- [ ] **Step 5: Run the tests**

Run: `cd sindri && npm run gen && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS (the real sandbox smoke test runs `curl` inside the sandbox and expects a failure); 100% coverage.

- [ ] **Step 6: Commit**

```bash
git add sindri/src sindri/tests sindri/vitest.config.ts docs/sindri/errors.md
git commit -m "feat: sindri graph layer via sandboxed graphify"
```

---

### Task 8: Staged overlay, shape signals and `sindri index query`

**Files:**
- Create: `sindri/src/index/overlay.ts`, `sindri/src/index/signals.ts`
- Modify: `sindri/src/index/commands.ts` (add `query`)
- Test: `sindri/tests/overlay.test.ts`, `sindri/tests/signals.test.ts`

**Interfaces:**
- Consumes: `IndexDb`, `allSymbols`, `symbolsByAstHash`, `bandCandidates`, `symbolById`, `depRows`, `embeddingRows`, `layers` (Task 5); `typescriptParser` (Task 3); `signature`, `bandKeys`, `estimateJaccard` (Task 4); `readManifestDeps` (Task 5); `Embedder`, `embeddingText`, `decodeVec`, `cosine` (Task 6).
- Produces (`overlay.ts`):
  - `interface StagedChange { path: string; text: string | null }` (`null` = deleted).
  - `stagedChanges(git, repoPath): Promise<{ changes: StagedChange[]; addedLines: number }>` — from `git diff --cached` (`-M`, renames count as changes; binary files count no lines).
  - `interface OverlaySymbol extends ParsedSymbol { minhash: Uint32Array }`; `interface Overlay { symbols: OverlaySymbol[]; changedPaths: Set<string>; manifests: { path: string; text: string }[]; addedLines: number }`.
  - `buildOverlay(changes: StagedChange[], addedLines: number): Overlay` — parses only TS/JS files and `package.json` manifests.
- Produces (`signals.ts`):
  - `type SignalType = "reinvented:exact" | "reinvented:name" | "reinvented:embedding" | "reinvented:graph" | "reinvented:dependency" | "generalize:near-clone" | "simpler:diff-size" | "simpler:complexity" | "simpler:exports"`.
  - `interface Signal { type: SignalType; layer: Layer; value: number; threshold: number; at: string; existing: string | null; detail: string }` — `at` and `existing` are `path:line`; names in `detail` are wrapped in `<untrusted>…</untrusted>` (spec §6.2 evidence, M4).
  - `interface Thresholds` = the profile's `shape.thresholds`.
  - `computeSignals(i: { base: IndexDb; overlay: Overlay; t: Thresholds; sizeBudget: number; exportAllowance: number; embed: { embedder: Embedder; deadline: number; now: () => number } | null }): Promise<{ signals: Signal[]; deferred: Layer[] }>`.
  - `nameSimilarity(a: string, b: string): number` (1 − normalized Levenshtein on lower-cased word parts).
  - A symbol is a reinvention candidate only if it is not a class and has at least 20 tokens; base candidates are other files' symbols that are exported or under `index.utilityGlobs` (the things meant for reuse).

- [ ] **Step 1: Write the failing tests**

`sindri/tests/overlay.test.ts`:

```ts
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { realGitRunner } from "../src/git-real.js";
import { buildOverlay, stagedChanges } from "../src/index/overlay.js";
import { gitRepo } from "./helpers.js";

describe("staged overlay", () => {
  it("reads staged contents (not the working tree), deletions and added lines", async () => {
    const root = gitRepo({ "a.ts": "export const a = 1;\n", "b.ts": "export const b = 1;\n", "logo.png": "x" });
    fs.writeFileSync(path.join(root, "a.ts"), "export function a() { return 2; }\n");
    execFileSync("git", ["add", "a.ts"], { cwd: root });
    fs.writeFileSync(path.join(root, "a.ts"), "UNSTAGED EDIT\n");
    execFileSync("git", ["rm", "-q", "b.ts"], { cwd: root });
    fs.writeFileSync(path.join(root, "logo.png"), Buffer.from([0, 1, 2, 0]));
    execFileSync("git", ["add", "logo.png"], { cwd: root });
    const { changes, addedLines } = await stagedChanges(realGitRunner(), root);
    expect(changes).toEqual([
      { path: "a.ts", text: "export function a() { return 2; }\n" },
      { path: "b.ts", text: null },
      { path: "logo.png", text: expect.any(String) },
    ]);
    expect(addedLines).toBe(1);
  });

  it("parses only code and manifests into the overlay", () => {
    const o = buildOverlay([
      { path: "src/x.ts", text: "export function x(a: number) { return a + 1; }\n" },
      { path: "src/gone.ts", text: null },
      { path: "package.json", text: "{}" },
      { path: "README.md", text: "# r" },
    ], 3);
    expect(o.symbols.map((s) => s.name)).toEqual(["x"]);
    expect(o.symbols[0].minhash).toHaveLength(64);
    expect([...o.changedPaths].sort()).toEqual(["README.md", "package.json", "src/gone.ts", "src/x.ts"]);
    expect(o.manifests).toEqual([{ path: "package.json", text: "{}" }]);
    expect(o.addedLines).toBe(3);
  });

  it("fails outside a git repo", async () => {
    await expect(stagedChanges(realGitRunner(), "/")).rejects.toThrow(/SND-INDEX-002|not a git repo/);
  });
});
```

`sindri/tests/signals.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { buildIndex } from "../src/index/build.js";
import { indexPath, openIndexReadOnly, type IndexDb } from "../src/index/db.js";
import type { Embedder } from "../src/index/embed.js";
import { buildOverlay } from "../src/index/overlay.js";
import { computeSignals, nameSimilarity } from "../src/index/signals.js";
import { ProfileSchema } from "../src/profile/schema.js";
import { loadProfile } from "../src/profile/load.js";
import { gitRepo, makeDeps, tempDir } from "./helpers.js";

const BODY = (name: string, extra = "") => `export function ${name}(items: string[], limit: number): string[] {
  const out: string[] = [];
  for (const item of items) {
    if (item.length > limit) { out.push(item.slice(0, limit)); } else { out.push(item.trim()); }
  }
  ${extra}
  return out.filter((x) => x !== "").map((x) => x.toLowerCase());
}
`;

const t = ProfileSchema.parse({ schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"] }).shape.thresholds;

async function baseIndex(files: Record<string, string>, embedder: Embedder | null = null): Promise<IndexDb> {
  const root = gitRepo(files);
  const dir = tempDir();
  fs.mkdirSync(path.join(dir, "repos"));
  fs.writeFileSync(path.join(dir, "profile.yaml"), "schemaVersion: 1\nuser: me\nhosts:\n  active: test-host\ntracker:\n  type: plan-file\n  repo: r\nrepos:\n  - r\nindex:\n  utilityGlobs:\n    - \"src/util/**\"\n");
  fs.writeFileSync(path.join(dir, "repos/r.yaml"), `schemaVersion: 1\nname: r\npath: ${root}\n`);
  const p = loadProfile(dir);
  if (!p.ok) throw new Error("profile");
  const d = makeDeps();
  await buildIndex(d, p.value, "r", { full: false }, { embedder, graph: null });
  const db = openIndexReadOnly(indexPath(d, "r"));
  if (db === null) throw new Error("no index");
  return db;
}

const run = (base: IndexDb, changes: { path: string; text: string | null }[], addedLines = 5, embed: Parameters<typeof computeSignals>[0]["embed"] = null) =>
  computeSignals({ base, overlay: buildOverlay(changes, addedLines), t, sizeBudget: 250, exportAllowance: 3, embed });

describe("shape signals", () => {
  it("flags an exact clone of an exported function in another file", async () => {
    const base = await baseIndex({ "src/util/text.ts": BODY("clip") });
    const { signals } = await run(base, [{ path: "src/feature.ts", text: BODY("shorten") }]);
    const exact = signals.find((s) => s.type === "reinvented:exact");
    expect(exact).toMatchObject({ layer: "clones", value: 1, threshold: 1, at: "src/feature.ts:1", existing: "src/util/text.ts:1" });
    expect(exact?.detail).toBe("<untrusted>shorten</untrusted> has the same structure as <untrusted>clip</untrusted>");
  });

  it("flags a near-clone (second case) and not an unrelated function", async () => {
    const base = await baseIndex({ "src/util/text.ts": BODY("clip") });
    const near = await run(base, [{ path: "src/feature.ts", text: BODY("shorten", "out.reverse(); out.sort();") }]);
    expect(near.signals.map((s) => s.type)).toContain("generalize:near-clone");
    const other = await run(base, [{ path: "src/other.ts", text: "export function add(a: number, b: number) { return a + b + a * b - (a / b) + Math.max(a, b) + Math.min(a, b); }\n" }]);
    expect(other.signals.filter((s) => s.type.startsWith("reinvented") || s.type.startsWith("generalize"))).toEqual([]);
  });

  it("flags similar names and signatures, and overlapping call sets", async () => {
    const base = await baseIndex({ "src/util/fmt.ts": "export function formatDate(d: Date): string { return pad(d.getFullYear()) + sep() + pad(d.getMonth()) + sep() + pad(d.getDate()) + suffix(d); }\n" });
    const r = await run(base, [{ path: "src/x.ts", text: "export function formatDates(d: Date): string { const y = pad(d.getFullYear()); return y + sep() + pad(d.getMonth()) + sep() + pad(d.getDate()) + suffix(d); }\n" }]);
    expect(r.signals.map((s) => s.type)).toEqual(expect.arrayContaining(["reinvented:name", "reinvented:graph"]));
    expect(nameSimilarity("formatDate", "format_dates")).toBeGreaterThan(0.85);
    expect(nameSimilarity("parse", "render")).toBeLessThan(0.5);
    expect(nameSimilarity("", "")).toBe(1);
  });

  it("flags a duplicate-purpose dependency, an oversized diff, a complexity jump and too many exports", async () => {
    const base = await baseIndex({
      "package.json": JSON.stringify({ dependencies: { dayjs: "^1" } }),
      "src/a.ts": "export function grow(x: number) { return x; }\n",
    });
    const complex = "export function grow(x: number) {\n" + Array.from({ length: 12 }, (_, i) => `  if (x > ${i}) { x++; }`).join("\n") + "\n  return x;\n}\n";
    const many = Array.from({ length: 5 }, (_, i) => `export const e${i} = ${i};`).join("\n");
    const r = await run(base, [
      { path: "package.json", text: JSON.stringify({ dependencies: { dayjs: "^1", moment: "^2" } }) },
      { path: "src/a.ts", text: complex },
      { path: "src/many.ts", text: many },
    ], 400);
    const types = r.signals.map((s) => s.type);
    expect(types).toEqual(expect.arrayContaining(["reinvented:dependency", "simpler:diff-size", "simpler:complexity"]));
    expect(r.signals.find((s) => s.type === "reinvented:dependency")?.detail).toBe("adds <untrusted>moment</untrusted> (date) while <untrusted>dayjs</untrusted> (date) is already a dependency");
    expect(r.signals.find((s) => s.type === "simpler:diff-size")).toMatchObject({ value: 400, threshold: 250, at: "(diff)" });
    const exportsOnly = await run(base, [{ path: "src/many.ts", text: Array.from({ length: 5 }, (_, i) => `export function f${i}() { return ${i}; }`).join("\n") }]);
    expect(exportsOnly.signals.find((s) => s.type === "simpler:exports")).toMatchObject({ value: 5, threshold: 3 });
  });

  it("uses embeddings only within the deadline and records them as deferred otherwise", async () => {
    const vec = (t: string) => new Float32Array(t.includes("items") ? [1, 0, 0] : [0, 1, 0]);
    const embedder: Embedder = { model: "m", embed: async (texts) => texts.map(vec) };
    const base = await baseIndex({ "src/util/text.ts": BODY("clip") }, embedder);
    const now = () => 0;
    const hit = await run(base, [{ path: "src/f.ts", text: BODY("shorten", "out.reverse();") }], 5, { embedder, deadline: 1000, now });
    expect(hit.signals.map((s) => s.type)).toContain("reinvented:embedding");
    const late = await run(base, [{ path: "src/f.ts", text: BODY("shorten") }], 5, { embedder, deadline: 0, now });
    expect(late.deferred).toEqual(["embeddings"]);
    const slow: Embedder = { model: "m", embed: async () => { throw new Error("timeout"); } };
    const failed = await run(base, [{ path: "src/f.ts", text: BODY("shorten") }], 5, { embedder: slow, deadline: 1000, now });
    expect(failed.deferred).toEqual(["embeddings"]);
  });

  it("records nothing for a change with no code (Review Focus 5)", async () => {
    const base = await baseIndex({ "src/a.ts": "export const a = 1;\n" });
    expect(await run(base, [{ path: "README.md", text: "# docs" }, { path: "src/a.ts", text: null }], 3)).toEqual({ signals: [], deferred: [] });
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/overlay.test.ts tests/signals.test.ts`
Expected: FAIL with `Failed to load url ../src/index/overlay.js` (and `signals.js`).

- [ ] **Step 3: Implement**

`sindri/src/index/overlay.ts`:

```ts
import path from "node:path";

import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import { isSourcePath } from "./files.js";
import { signature } from "./minhash.js";
import { typescriptParser, type ParsedSymbol } from "./parse-ts.js";

export interface StagedChange {
  path: string;
  text: string | null;
}

export interface OverlaySymbol extends ParsedSymbol {
  minhash: Uint32Array;
}

export interface Overlay {
  symbols: OverlaySymbol[];
  changedPaths: Set<string>;
  manifests: { path: string; text: string }[];
  addedLines: number;
}

// The commit as it will be: staged blobs (git show :path), never the working tree.
export async function stagedChanges(git: GitRunner, repoPath: string): Promise<{ changes: StagedChange[]; addedLines: number }> {
  const names = await git.run(["-c", "core.quotePath=false", "diff", "--cached", "--name-status", "-M", "-z"], repoPath);
  if (!names.ok) throw new SindriError("SND-INDEX-002", `${repoPath} is not a git repo`);
  const parts = names.stdout.split("\0").filter((p) => p !== "");
  const changes: StagedChange[] = [];
  for (let i = 0; i < parts.length; i++) {
    const status = parts[i];
    if (status.startsWith("R") || status.startsWith("C")) {
      i += 2;
      const shown = await git.run(["show", `:${parts[i]}`], repoPath);
      changes.push({ path: parts[i], text: shown.ok ? shown.stdout : null });
    } else {
      i += 1;
      const deleted = status === "D";
      const shown = deleted ? null : await git.run(["show", `:${parts[i]}`], repoPath);
      changes.push({ path: parts[i], text: shown === null || !shown.ok ? null : shown.stdout });
    }
  }
  const numstat = await git.run(["diff", "--cached", "--numstat", "-M"], repoPath);
  const addedLines = (numstat.ok ? numstat.stdout : "")
    .split("\n")
    .map((l) => Number(l.split("\t")[0]))
    .filter((n) => Number.isFinite(n))
    .reduce((a, b) => a + b, 0);
  return { changes: changes.sort((a, b) => a.path.localeCompare(b.path)), addedLines };
}

export function buildOverlay(changes: StagedChange[], addedLines: number): Overlay {
  const symbols: OverlaySymbol[] = [];
  const manifests: { path: string; text: string }[] = [];
  for (const c of changes) {
    if (c.text === null) continue;
    if (isSourcePath(c.path)) {
      for (const s of typescriptParser.parse(c.path, c.text)) symbols.push({ ...s, minhash: signature(s.tokens) });
    } else if (path.basename(c.path) === "package.json") {
      manifests.push({ path: c.path, text: c.text });
    }
  }
  return { symbols, changedPaths: new Set(changes.map((c) => c.path)), manifests, addedLines };
}
```

`sindri/src/index/signals.ts`:

```ts
import { allSymbols, bandCandidates, depRows, embeddingRows, layers, symbolById, symbolsByAstHash, type IndexDb, type Layer, type SymbolRow } from "./db.js";
import { readManifestDeps } from "./deps-layer.js";
import { cosine, decodeVec, embeddingText, type Embedder } from "./embed.js";
import { bandKeys, estimateJaccard } from "./minhash.js";
import type { Overlay, OverlaySymbol } from "./overlay.js";

export type SignalType =
  | "reinvented:exact" | "reinvented:name" | "reinvented:embedding" | "reinvented:graph" | "reinvented:dependency"
  | "generalize:near-clone" | "simpler:diff-size" | "simpler:complexity" | "simpler:exports";

export interface Signal {
  type: SignalType;
  layer: Layer;
  value: number;
  threshold: number;
  at: string;
  existing: string | null;
  detail: string;
}

export interface Thresholds {
  nameSimilarity: number;
  embedding: number;
  embeddingAst: number;
  nearCloneTokens: number;
  nearCloneJaccard: number;
  callOverlap: number;
  complexityDelta: number;
}

const MIN_TOKENS = 20;
const MIN_CALLS = 3;
const u = (s: string): string => `<untrusted>${s}</untrusted>`;
const loc = (s: { file: string; startLine: number }): string => `${s.file}:${s.startLine}`;

function words(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_\-.]+/g, " ").toLowerCase().trim();
}

function levenshtein(a: string, b: string): number {
  const prev = Array.from({ length: b.length + 1 }, (_, j) => j);
  for (let i = 1; i <= a.length; i++) {
    let diag = prev[0];
    prev[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const up = prev[j];
      prev[j] = Math.min(prev[j] + 1, prev[j - 1] + 1, diag + (a[i - 1] === b[j - 1] ? 0 : 1));
      diag = up;
    }
  }
  return prev[b.length];
}

export function nameSimilarity(a: string, b: string): number {
  const x = words(a);
  const y = words(b);
  const max = Math.max(x.length, y.length);
  return max === 0 ? 1 : 1 - levenshtein(x, y) / max;
}

function setJaccard(a: string[], b: string[]): number {
  const A = new Set(a);
  const B = new Set(b);
  const inter = [...A].filter((x) => B.has(x)).length;
  const union = new Set([...A, ...B]).size;
  return union === 0 ? 0 : inter / union;
}

export async function computeSignals(i: {
  base: IndexDb;
  overlay: Overlay;
  t: Thresholds;
  sizeBudget: number;
  exportAllowance: number;
  embed: { embedder: Embedder; deadline: number; now: () => number } | null;
}): Promise<{ signals: Signal[]; deferred: Layer[] }> {
  const { base, overlay, t } = i;
  const signals: Signal[] = [];
  const deferred: Layer[] = [];
  const unchanged = (s: SymbolRow): boolean => !overlay.changedPaths.has(s.file);
  const baseAll = allSymbols(base);
  const reusable = baseAll.filter((s) => unchanged(s) && s.kind !== "class" && (s.exported || s.utility));
  const before = new Map(baseAll.map((s) => [`${s.file}#${s.name}`, s]));
  const fresh = overlay.symbols.filter((s) => before.get(`${s.file}#${s.name}`)?.astHash !== s.astHash);
  const candidates = fresh.filter((s) => s.kind !== "class" && s.tokens.length >= MIN_TOKENS);

  for (const s of candidates) {
    const exact = symbolsByAstHash(base, s.astHash).find((b) => unchanged(b) && b.name !== s.name);
    if (exact !== undefined) {
      signals.push({ type: "reinvented:exact", layer: "clones", value: 1, threshold: 1, at: loc(s), existing: loc(exact), detail: `${u(s.name)} has the same structure as ${u(exact.name)}` });
      continue;
    }
    const near = bandCandidates(base, bandKeys(s.minhash))
      .map((id) => symbolById(base, id))
      .filter((b): b is SymbolRow => b !== null && unchanged(b) && b.tokenCount >= t.nearCloneTokens)
      .map((b) => ({ b, j: estimateJaccard(s.minhash, b.minhash) }))
      .sort((x, y) => y.j - x.j)[0];
    if (near !== undefined && s.tokens.length >= t.nearCloneTokens && near.j >= t.nearCloneJaccard) {
      signals.push({ type: "generalize:near-clone", layer: "clones", value: near.j, threshold: t.nearCloneJaccard, at: loc(s), existing: loc(near.b), detail: `${u(s.name)} is a near-copy of ${u(near.b.name)}; generalize at the second case` });
    }
    const named = reusable.find((b) => nameSimilarity(s.name, b.name) >= t.nameSimilarity && nameSimilarity(s.signature, b.signature) >= t.nameSimilarity);
    if (named !== undefined) {
      signals.push({ type: "reinvented:name", layer: "structure", value: nameSimilarity(s.name, named.name), threshold: t.nameSimilarity, at: loc(s), existing: loc(named), detail: `${u(s.name)} looks like ${u(named.name)}` });
    }
    const calls = s.callees.length >= MIN_CALLS ? reusable.find((b) => b.callees.length >= MIN_CALLS && setJaccard(s.callees, b.callees) >= t.callOverlap) : undefined;
    if (calls !== undefined) {
      signals.push({ type: "reinvented:graph", layer: "graph", value: setJaccard(s.callees, calls.callees), threshold: t.callOverlap, at: loc(s), existing: loc(calls), detail: `${u(s.name)} calls what ${u(calls.name)} calls` });
    }
  }

  if (i.embed !== null && candidates.length > 0) {
    const ready = layers(base).some((l) => l.layer === "embeddings" && l.status === "ok");
    const left = i.embed.deadline - i.embed.now();
    let vectors: Float32Array[] | null = null;
    if (ready && left > 0) {
      vectors = await Promise.race([
        i.embed.embedder.embed(candidates.map((s) => embeddingText({ name: s.name, signature: s.signature, body: s.text }))).catch(() => null),
        new Promise<null>((r) => setTimeout(() => r(null), left)),
      ]);
    }
    if (vectors === null) {
      deferred.push("embeddings");
    } else {
      const rows = embeddingRows(base, i.embed.embedder.model).map((r) => ({ id: r.symbolId, v: decodeVec(r.vector) }));
      candidates.forEach((s, k) => {
        const best = rows
          .map((r) => ({ r, c: cosine(vectors[k], r.v) }))
          .sort((x, y) => y.c - x.c)
          .map(({ r, c }) => ({ b: symbolById(base, r.id), c }))
          .find(({ b, c }) => b !== null && unchanged(b) && c >= t.embedding && estimateJaccard(s.minhash, b.minhash) >= t.embeddingAst);
        if (best !== undefined && best.b !== null) {
          signals.push({ type: "reinvented:embedding", layer: "embeddings", value: best.c, threshold: t.embedding, at: loc(s), existing: loc(best.b), detail: `${u(s.name)} means what ${u(best.b.name)} means` });
        }
      });
    }
  }

  const existingDeps = depRows(base);
  for (const m of overlay.manifests) {
    const known = new Set(existingDeps.filter((d) => d.manifest === m.path).map((d) => d.name));
    for (const added of readManifestDeps(m.path, m.text).filter((d) => !known.has(d.name))) {
      const twin = existingDeps.find((d) => d.name !== added.name && d.tags.some((tag) => added.tags.includes(tag)));
      if (twin !== undefined) {
        const tag = twin.tags.find((x) => added.tags.includes(x)) as string;
        signals.push({ type: "reinvented:dependency", layer: "deps", value: 1, threshold: 1, at: m.path, existing: twin.manifest, detail: `adds ${u(added.name)} (${tag}) while ${u(twin.name)} (${tag}) is already a dependency` });
      }
    }
  }

  if (overlay.addedLines > i.sizeBudget) {
    signals.push({ type: "simpler:diff-size", layer: "structure", value: overlay.addedLines, threshold: i.sizeBudget, at: "(diff)", existing: null, detail: `${overlay.addedLines} added lines; the size budget is ${i.sizeBudget}` });
  }
  for (const s of fresh) {
    const old = before.get(`${s.file}#${s.name}`);
    if (old !== undefined && s.complexity - old.complexity > t.complexityDelta) {
      signals.push({ type: "simpler:complexity", layer: "structure", value: s.complexity - old.complexity, threshold: t.complexityDelta, at: loc(s), existing: loc(old), detail: `${u(s.name)} grew from complexity ${old.complexity} to ${s.complexity}` });
    }
  }
  const newExports = fresh.filter((s) => s.exported && !before.has(`${s.file}#${s.name}`)).length;
  if (newExports > i.exportAllowance) {
    signals.push({ type: "simpler:exports", layer: "structure", value: newExports, threshold: i.exportAllowance, at: "(diff)", existing: null, detail: `${newExports} new exports; the allowance is ${i.exportAllowance}` });
  }
  return { signals, deferred };
}
```

In `sindri/src/index/commands.ts`, add a `query` subcommand: `sindri index query <name> [--repo NAME] [--json]` prints every indexed symbol with that name and, for each, its exact clones (same `astHash`) and near clones (shared band, estimated Jaccard ≥ `shape.thresholds.nearCloneJaccard`), one per line as `path:line name (exact|near 0.87)`. It prints `No symbol named <name> in <repo>.` when there are none. Implement it with `allSymbols`, `symbolsByAstHash`, `bandCandidates`, `symbolById` and `estimateJaccard`. Add the `query` line to the `index` usage text, and this test to `sindri/tests/index-build.test.ts`:

```ts
  it("query lists a symbol's exact and near clones", async () => {
    const root = gitRepo({ ...FILES, "docs/superpowers/plans/p.md": "# P\n" });
    const d = await approved(root);
    await runCli(["index", "build"], d);
    const r = await runCli(["index", "query", "add"], d);
    expect(r.stdout).toContain("src/a.ts:1 add");
    expect(r.stdout).toContain("src/b.ts:1 sum (exact)");
    expect((await runCli(["index", "query", "nope"], d)).stdout).toContain("No symbol named nope in");
    expect((await runCli(["index", "query"], d)).stderr).toContain("SND-CLI-002");
  });
```

(The Task 5 `approved()` helper already disables embeddings and the graph for CLI tests.)

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage. If a fixture misses a threshold by a hair (for example, the near-clone Jaccard), fix the fixture body so the case is unambiguous; never lower the default threshold to make a test pass.

- [ ] **Step 5: Commit**

```bash
git add sindri/src/index sindri/tests
git commit -m "feat: sindri staged overlay, shape signals and index query"
```

---

### Task 9: `sindri shape --record` at commit, the spool, ingestion and `shape report`

**Files:**
- Create: `sindri/src/index/spool.ts`, `sindri/src/index/shape.ts`
- Modify: `sindri/src/scrub/commands.ts` (pre-commit hook v2), `sindri/src/observe/observe.ts` (ingest the spool while recording), `sindri/src/main.ts` (register `shape`)
- Test: `sindri/tests/spool.test.ts`, `sindri/tests/shape.test.ts`, `sindri/tests/scrub-commands.test.ts` (hook v2)

**Interfaces:**
- Consumes: `stagedChanges`, `buildOverlay` (Task 8); `computeSignals`, `Signal` (Task 8); `openIndexReadOnly`, `indexPath`, `meta` (Task 5); `embedderFor` (Task 6); `approvedProfile` (Plan 2); `acquireTickLock`, `withEpoch` (Plan 2); `ulid` (Plan 2).
- Produces (`spool.ts`):
  - `spoolDir(deps): string` → `$AW_STATE_DIR/sindri/spool`.
  - `interface ShapeRun { runId: string; repo: string; ts: string; head: string | null; elapsedMs: number; deferred: string[]; signals: Signal[] }`.
  - `writeShapeRun(deps, run: ShapeRun): string` — one JSON file `shape-<runId>.json` written to a temp name and renamed (the hook side never touches the ledger, spec §5.2).
  - `ingestSpool(db: Ledger, deps, epoch: number): { runs: number; signals: number; quarantined: number }` — call inside the tick lock; schema-validated (Zod), scrubbed, capped; bad files move to `spool/quarantine/`, ingested ones are deleted.
- Produces (`shape.ts`):
  - `shapeCommand: Command` — `sindri shape --record --staged [--repo NAME] [--size XS|S|M|L|XL]` (always exit 0) and `sindri shape report [--since 7d] [--json]`.
  - `recordStaged(deps, o: { repo?: string; size?: Size }): Promise<{ written: string | null; note: string }>`.
- Produces (`scrub/commands.ts`): hook v2 — marker `# sindri-pre-commit v2`; scrubs (and refuses on a hit), then runs `"$SINDRI" shape --record --staged || true`. `install` upgrades a v1 hook in place; `hookBinary` reads both versions.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/spool.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { bumpEpoch, openMemoryLedger } from "../src/ledger/db.js";
import { ingestSpool, spoolDir, writeShapeRun, type ShapeRun } from "../src/index/spool.js";
import { makeDeps } from "./helpers.js";

const run = (over: Partial<ShapeRun> = {}): ShapeRun => ({
  runId: "01k0000000000000000000000a", repo: "r", ts: "2026-10-08T12:00:00.000Z", head: "abc", elapsedMs: 40, deferred: ["embeddings"],
  signals: [{ type: "reinvented:exact", layer: "clones", value: 1, threshold: 1, at: "src/a.ts:1", existing: "src/b.ts:1", detail: "<untrusted>a</untrusted> has the same structure as <untrusted>b</untrusted>" }],
  ...over,
});

describe("shape spool", () => {
  it("writes runs atomically and ingests them into the ledger once", () => {
    const d = makeDeps();
    const file = writeShapeRun(d, run());
    expect(path.basename(file)).toBe("shape-01k0000000000000000000000a.json");
    expect(fs.readdirSync(spoolDir(d)).filter((n) => n.includes(".tmp"))).toEqual([]);
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 1, signals: 1, quarantined: 0 });
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 0, signals: 0, quarantined: 0 });
    expect(db.prepare("SELECT type, layer, at, existing, epoch FROM shape_signals").all()).toEqual([
      { type: "reinvented:exact", layer: "clones", at: "src/a.ts:1", existing: "src/b.ts:1", epoch },
    ]);
    expect(db.prepare("SELECT repo, deferred, signal_count FROM shape_runs").get()).toEqual({ repo: "r", deferred: '["embeddings"]', signal_count: 1 });
  });

  it("quarantines files that fail validation, are not regular files, or are too big, and scrubs details", () => {
    const d = makeDeps();
    fs.mkdirSync(spoolDir(d), { recursive: true });
    fs.writeFileSync(path.join(spoolDir(d), "shape-bad.json"), JSON.stringify({ nope: 1 }));
    fs.writeFileSync(path.join(spoolDir(d), "shape-huge.json"), "x".repeat(2 * 1024 * 1024));
    fs.symlinkSync("/etc/hosts", path.join(spoolDir(d), "shape-link.json"));
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    writeShapeRun(d, run({ runId: "01k0000000000000000000000b", signals: [{ ...run().signals[0], detail: `leak ${secret}` }] }));
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 1, signals: 1, quarantined: 3 });
    expect(JSON.stringify(db.prepare("SELECT detail FROM shape_signals").all())).not.toContain(secret);
    expect(fs.readdirSync(path.join(spoolDir(d), "quarantine")).sort()).toEqual(["shape-bad.json", "shape-huge.json", "shape-link.json"]);
  });

  it("does nothing when there is no spool", () => {
    const db = openMemoryLedger();
    expect(ingestSpool(db, makeDeps(), bumpEpoch(db))).toEqual({ runs: 0, signals: 0, quarantined: 0 });
  });
});
```

`sindri/tests/shape.test.ts`:

```ts
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import type { Deps } from "../src/deps.js";
import { spoolDir } from "../src/index/spool.js";
import { runCli } from "../src/main.js";
import { gitRepo, makeDeps } from "./helpers.js";

const CLIP = `export function clip(items: string[], limit: number): string[] {
  const out: string[] = [];
  for (const item of items) { if (item.length > limit) { out.push(item.slice(0, limit)); } else { out.push(item.trim()); } }
  return out.filter((x) => x !== "").map((x) => x.toLowerCase());
}
`;

async function ready(): Promise<{ d: Deps; root: string }> {
  const root = gitRepo({ "src/util/text.ts": CLIP, "docs/superpowers/plans/p.md": "# P\n" });
  const d = makeDeps({ cwd: root });
  await runCli(["profile", "init", "--ring0"], d);
  fs.appendFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "index:\n  embeddings:\n    enabled: false\n  graph: none\n");
  const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
  await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
  await runCli(["index", "build"], d);
  return { d, root };
}

describe("sindri shape --record --staged", () => {
  it("records signals for a staged clone to the spool and always exits 0", async () => {
    const { d, root } = await ready();
    fs.writeFileSync(path.join(root, "src/feature.ts"), CLIP.replace("clip", "shorten"));
    execFileSync("git", ["add", "src/feature.ts"], { cwd: root });
    const r = await runCli(["shape", "--record", "--staged"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stderr).toMatch(/^sindri-shape: 1 signal\(s\) recorded \(reinvented:exact\)/);
    const files = fs.readdirSync(spoolDir(d)).filter((n) => n.startsWith("shape-"));
    expect(files).toHaveLength(1);
    const run = JSON.parse(fs.readFileSync(path.join(spoolDir(d), files[0]), "utf8"));
    expect(run.signals[0]).toMatchObject({ type: "reinvented:exact", at: "src/feature.ts:1", existing: "src/util/text.ts:1" });
  });

  it("records nothing for a docs-only commit, and finishes quickly (Review Focus 5)", async () => {
    const { d, root } = await ready();
    fs.writeFileSync(path.join(root, "README.md"), "# hi\n");
    execFileSync("git", ["add", "README.md"], { cwd: root });
    const t0 = Date.now();
    const r = await runCli(["shape", "--record", "--staged"], d);
    expect(Date.now() - t0).toBeLessThan(2000);
    expect(r).toMatchObject({ exitCode: 0, stdout: "" });
    expect(r.stderr).toBe("");
  });

  it("never fails a commit: no profile, no index, or outside a repo all exit 0 with a note", async () => {
    const noProfile = await runCli(["shape", "--record", "--staged"], makeDeps());
    expect(noProfile.exitCode).toBe(0);
    expect(noProfile.stderr).toContain("sindri-shape: skipped (no approved profile)");
    const root = gitRepo({ "docs/superpowers/plans/p.md": "# P\n" });
    const d = makeDeps({ cwd: root });
    await runCli(["profile", "init", "--ring0"], d);
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    const noIndex = await runCli(["shape", "--record", "--staged"], d);
    expect(noIndex.stderr).toContain("sindri-shape: skipped (no index; sindri index build)");
    const elsewhere = await runCli(["shape", "--record", "--staged"], { ...d, cwd: "/" });
    expect(elsewhere.exitCode).toBe(0);
  });

  it("shape report ingests the spool and counts signals by type and layer", async () => {
    const { d, root } = await ready();
    fs.writeFileSync(path.join(root, "src/feature.ts"), CLIP.replace("clip", "shorten"));
    execFileSync("git", ["add", "src/feature.ts"], { cwd: root });
    await runCli(["shape", "--record", "--staged"], d);
    const r = await runCli(["shape", "report"], d);
    expect(r.stdout).toContain("Ingested 1 run(s), 1 signal(s).");
    expect(r.stdout).toMatch(/reinvented:exact\s+1\s+clones/);
    const json = JSON.parse((await runCli(["shape", "report", "--json"], d)).stdout);
    expect(json.byType["reinvented:exact"]).toBe(1);
    expect((await runCli(["shape", "frob"], d)).stderr).toContain("SND-CLI-002");
  });
});
```

Add to `sindri/tests/scrub-commands.test.ts`:

```ts
describe("pre-commit hook v2", () => {
  it("scans, then records shape signals without blocking, and upgrades a v1 hook in place", async () => {
    const root = repo();
    const hookPath = path.join(root, ".git/hooks/pre-commit");
    fs.mkdirSync(path.dirname(hookPath), { recursive: true });
    fs.writeFileSync(hookPath, "#!/bin/sh\n# sindri-scrub-pre-commit v1\nSINDRI='/old/sindri'\nexec \"$SINDRI\" scrub --staged\n");
    await runCli(["scrub", "--install-pre-commit"], makeDeps({ cwd: root, env: { SINDRI_BIN: "/new/sindri" } }));
    const text = fs.readFileSync(hookPath, "utf8");
    expect(text).toContain("# sindri-pre-commit v2");
    expect(text).toContain('"$SINDRI" scrub --staged || exit 1');
    expect(text).toContain('"$SINDRI" shape --record --staged || true');
    expect(hookBinary(text)).toBe("/new/sindri");
    expect(hookBinary("#!/bin/sh\n# sindri-scrub-pre-commit v1\nSINDRI='/old/sindri'\n")).toBe("/old/sindri");
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/spool.test.ts tests/shape.test.ts tests/scrub-commands.test.ts`
Expected: FAIL with `Failed to load url ../src/index/spool.js` (and `shape.js`), and the v2 hook test failing on the marker.

- [ ] **Step 3: Implement**

`sindri/src/index/spool.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { stateDir, type Deps } from "../deps.js";
import type { Ledger } from "../ledger/db.js";
import { makeScrubber } from "../scrub/scrub.js";
import type { Signal } from "./signals.js";

export interface ShapeRun {
  runId: string;
  repo: string;
  ts: string;
  head: string | null;
  elapsedMs: number;
  deferred: string[];
  signals: Signal[];
}

const MAX_BYTES = 1024 * 1024;
const RunSchema = z.object({
  runId: z.string().regex(/^[0-9a-z]{26}$/),
  repo: z.string().max(64),
  ts: z.string().max(40),
  head: z.string().max(64).nullable(),
  elapsedMs: z.number().int().nonnegative(),
  deferred: z.array(z.string().max(32)).max(8),
  signals: z.array(z.object({
    type: z.string().max(40), layer: z.string().max(16), value: z.number(), threshold: z.number(),
    at: z.string().max(512), existing: z.string().max(512).nullable(), detail: z.string().max(2000),
  })).max(500),
});

export const spoolDir = (deps: Deps): string => path.join(stateDir(deps), "spool");

// Hook side: one file per run, written under a temp name and renamed into place.
export function writeShapeRun(deps: Deps, run: ShapeRun): string {
  const dir = spoolDir(deps);
  fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
  const file = path.join(dir, `shape-${run.runId}.json`);
  fs.writeFileSync(`${file}.tmp`, JSON.stringify(run), { mode: 0o600 });
  fs.renameSync(`${file}.tmp`, file);
  return file;
}

// Sindri side, inside the tick lock: spool files are untrusted data (spec §8.2).
export function ingestSpool(db: Ledger, deps: Deps, epoch: number): { runs: number; signals: number; quarantined: number } {
  const dir = spoolDir(deps);
  const counts = { runs: 0, signals: 0, quarantined: 0 };
  if (!fs.existsSync(dir)) return counts;
  const scrubber = makeScrubber();
  const quarantine = (name: string): void => {
    fs.mkdirSync(path.join(dir, "quarantine"), { recursive: true, mode: 0o700 });
    fs.renameSync(path.join(dir, name), path.join(dir, "quarantine", name));
    counts.quarantined++;
  };
  for (const name of fs.readdirSync(dir).filter((n) => n.startsWith("shape-") && n.endsWith(".json")).sort()) {
    const st = fs.lstatSync(path.join(dir, name));
    if (!st.isFile() || st.size > MAX_BYTES) {
      quarantine(name);
      continue;
    }
    let parsed: z.infer<typeof RunSchema>;
    try {
      const r = RunSchema.safeParse(JSON.parse(fs.readFileSync(path.join(dir, name), "utf8")));
      if (!r.success) throw new Error("schema");
      parsed = r.data;
    } catch {
      quarantine(name);
      continue;
    }
    db.transaction(() => {
      db.prepare("INSERT OR IGNORE INTO shape_runs (run_id, repo, ts, head, elapsed_ms, deferred, signal_count, epoch) VALUES (?, ?, ?, ?, ?, ?, ?, ?)").run(
        parsed.runId, parsed.repo, parsed.ts, parsed.head, parsed.elapsedMs, JSON.stringify(parsed.deferred), parsed.signals.length, epoch,
      );
      for (const s of parsed.signals) {
        db.prepare("INSERT INTO shape_signals (run_id, type, layer, value, threshold, at, existing, detail, epoch) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)").run(
          parsed.runId, s.type, s.layer, s.value, s.threshold, scrubber.scrub(s.at).text, s.existing === null ? null : scrubber.scrub(s.existing).text, scrubber.scrub(s.detail).text, epoch,
        );
      }
    })();
    fs.rmSync(path.join(dir, name));
    counts.runs++;
    counts.signals += parsed.signals.length;
  }
  return counts;
}
```

`sindri/src/index/shape.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { ulid } from "../ids.js";
import { ledgerPath, openLedger, withEpoch } from "../ledger/db.js";
import { acquireTickLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { approvedProfile } from "../profile/approve.js";
import { SIZES, type Size } from "../profile/schema.js";
import { embedderFor } from "./commands.js";
import { indexPath, openIndexReadOnly } from "./db.js";
import { buildOverlay, stagedChanges } from "./overlay.js";
import { computeSignals } from "./signals.js";
import { ingestSpool, writeShapeRun } from "./spool.js";

// Record-only (spec amendment 5): every path returns normally; the hook never blocks.
export async function recordStaged(deps: Deps, o: { repo?: string; size?: Size }): Promise<{ written: string | null; note: string }> {
  const started = Date.now();
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return { written: null, note: "skipped (no approved profile)" };
  const db = openLedger(file);
  const loaded = approvedProfile(deps, db);
  db.close();
  if (loaded === null) return { written: null, note: "skipped (no approved profile)" };
  if (!loaded.profile.shape.record) return { written: null, note: "" };
  const top = await deps.git.run(["rev-parse", "--show-toplevel"], deps.cwd);
  const root = top.ok ? top.stdout.trim() : "";
  const repo = o.repo ?? Object.entries(loaded.repos).find(([, r]) => fs.existsSync(root) && fs.realpathSync(r.path) === fs.realpathSync(root))?.[0];
  if (repo === undefined) return { written: null, note: "skipped (this repo is not in the profile)" };
  const base = openIndexReadOnly(indexPath(deps, repo));
  if (base === null) return { written: null, note: "skipped (no index; sindri index build)" };
  try {
    const { changes, addedLines } = await stagedChanges(deps.git, loaded.repos[repo].path);
    const overlay = buildOverlay(changes, addedLines);
    if (overlay.symbols.length === 0 && overlay.manifests.length === 0) return { written: null, note: "" };
    const shape = loaded.profile.shape;
    const size = o.size ?? shape.defaultSize;
    const embedder = embedderFor(loaded);
    const { signals, deferred } = await computeSignals({
      base, overlay, t: shape.thresholds, sizeBudget: shape.sizeBudget[size], exportAllowance: shape.exportAllowance[size],
      embed: embedder === null ? null : { embedder, deadline: started + shape.budgetMs, now: () => Date.now() },
    });
    const head = await deps.git.run(["rev-parse", "HEAD"], loaded.repos[repo].path);
    const written = writeShapeRun(deps, {
      runId: ulid(deps.now()), repo, ts: deps.now().toISOString(), head: head.ok ? head.stdout.trim() : null, elapsedMs: Date.now() - started, deferred, signals,
    });
    const types = [...new Set(signals.map((s) => s.type))].join(", ");
    return { written, note: signals.length === 0 ? "" : `${signals.length} signal(s) recorded (${types}); record-only, the commit proceeds` };
  } finally {
    base.close();
  }
}

async function report(args: string[], deps: Deps): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" }, since: { type: "string" } });
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    const lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
    let ingested = { runs: 0, signals: 0, quarantined: 0 };
    if (lock.ok) {
      try {
        ingested = withEpoch(db, lock.owner.epoch, () => ingestSpool(db, deps, lock.owner.epoch));
      } finally {
        lock.release();
      }
    }
    const rows = db.prepare("SELECT type, layer, COUNT(*) AS n FROM shape_signals GROUP BY type, layer ORDER BY n DESC, type").all() as { type: string; layer: string; n: number }[];
    const byType = Object.fromEntries(rows.map((r) => [r.type, r.n]));
    const lines = [
      `Ingested ${ingested.runs} run(s), ${ingested.signals} signal(s).${ingested.quarantined > 0 ? ` Quarantined ${ingested.quarantined} bad spool file(s).` : ""}${lock.ok ? "" : " (Another run holds the lock; showing what's already ingested.)"}`,
      ...(rows.length === 0 ? ["No shape signals recorded yet."] : rows.map((r) => `${r.type.padEnd(24)} ${String(r.n).padStart(5)}  ${r.layer}`)),
    ];
    return success(lines.join("\n"), { ingested, byType, rows }, values.json === true);
  } finally {
    db.close();
  }
}

export const shapeCommand: Command = async (args, deps) => {
  const json = args.includes("--json");
  if (args[0] === "report") {
    try {
      return await report(args.slice(1), deps);
    } catch (e) {
      return fromError(e, json);
    }
  }
  if (!args.includes("--record")) return failure("SND-CLI-002", `unknown shape subcommand: ${args[0] ?? "(none)"}; use --record --staged or report`, json, { fix: "sindri shape --help" });
  try {
    const { values } = parseFlags(args, { record: { type: "boolean" }, staged: { type: "boolean" }, repo: { type: "string" }, size: { type: "string" } });
    const size = SIZES.find((s) => s === values.size);
    const r = await recordStaged(deps, { repo: values.repo, size });
    return { exitCode: 0, stdout: "", stderr: r.note === "" ? "" : `sindri-shape: ${r.note}\n` };
  } catch (e) {
    // Record-only: a failure is reported, never blocks the commit.
    return { exitCode: 0, stdout: "", stderr: `sindri-shape: skipped (${(e as Error).message})\n` };
  }
};
```

In `sindri/src/observe/observe.ts`, inside `record()`'s `withEpoch` callback (after `setCursor`), add `ingestSpool(db, deps, epoch);`. Import it from `../index/spool.js`. That way the hourly `observe` moves commit-time signals into the ledger.

In `sindri/src/scrub/commands.ts`:
- Change `PRE_COMMIT_MARKER` to `"# sindri-pre-commit v2"` and add `const LEGACY_MARKERS = ["# sindri-scrub-pre-commit v1"];`.
- In `install`, treat a file containing any of `[PRE_COMMIT_MARKER, ...LEGACY_MARKERS]` as sindri's own, which upgrades it in place.
- Replace the last line of `preCommitHook`'s script (`exec "$SINDRI" scrub --staged`) with:

```sh
"$SINDRI" scrub --staged || exit 1
# Record-only shape signals (spec §6.2): never blocks the commit.
"$SINDRI" shape --record --staged || true
```

- `hookBinary` is unchanged (it reads the `SINDRI='…'` line, which both versions have). In `doctor`, accept either marker.

Register in `sindri/src/main.ts`:

```ts
import { shapeCommand } from "./index/shape.js";

  shape: {
    summary: "Record shape signals for staged changes (pre-commit), or report them",
    usage: "Usage:\n  sindri shape --record --staged [--repo NAME] [--size XS|S|M|L|XL]   (always exits 0)\n  sindri shape report [--json]",
    run: shapeCommand,
  },
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests
git commit -m "feat: sindri record-only shape signals at commit, spool ingestion and report"
```

---

### Task 10: `sindri repo add`, `sindri index setup` and the `doctor` index checks

**Files:**
- Create: `sindri/src/index/probes.ts`, `sindri/src/index/setup.ts`, `sindri/src/index/repo-add.ts`
- Modify: `sindri/src/index/sandbox-real.ts` (add `realIndexProbes`), `sindri/src/index/commands.ts` (add `setup`), `sindri/src/main.ts` (register `repo`), `sindri/src/doctor/doctor.ts`, `sindri/src/errors.ts`
- Test: `sindri/tests/index-setup.test.ts`, `sindri/tests/repo-add.test.ts`, `sindri/tests/doctor.test.ts` (index checks), `sindri/tests/pins.test.ts`

**Interfaces:**
- Consumes: `hasBinary`, `realProcessRunner`, `ProcessRunner`, `sandboxArgv` (Task 7); `GRAPHIFY_PIN` (Task 7); `openIndexReadOnly`, `indexPath`, `meta`, `layers` (Task 5); `heavyLockState`, `heavyLockDir` (Task 1); `requireProfile`, `sanitizeName` (Plan 2 Task 6); `runChecks`, `Check` (Plan 2 Task 10).
- Produces (`probes.ts`): `interface IndexProbes { has(bin: string): boolean; run: ProcessRunner["run"]; getJson(url: string, timeoutMs: number): Promise<unknown | null> }`. Real: `realIndexProbes()` in `sandbox-real.ts`. `getJson` only ever calls the profile's loopback URL.
- Produces (`setup.ts`): `runSetup(loaded, probes, o: { dryRun: boolean; platform: NodeJS.Platform }): Promise<{ steps: { name: string; status: "ok" | "done" | "would" | "fail"; detail: string; fix?: string }[] }>`. Steps in order: `ollama`, `ollama-server`, `embedding-model`, `graphify`, `sandbox`. Steps for a layer the profile turns off are skipped.
- Produces (`repo-add.ts`): `repoAdd(deps, target: string, name?: string): Promise<{ name: string; path: string; mirror: string; added: boolean }>` — adds `repos/<name>.yaml` and the `repos` entry to the **live** profile (comments preserved), then creates or refreshes a bare mirror at `$AW_STATE_DIR/sindri/mirrors/<name>.git`. The change takes effect after `profile approve`.
- Produces (`doctor.ts`): `runChecks(deps, nodeVersion?, probes?: IndexProbes)` adds, after the Plan 2 checks: `index:<repo>` per repo, `embeddings`, `graphify`, `heavy-lock`.

- [ ] **Step 1: Write the failing tests**

`sindri/tests/pins.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { GRAPHIFY_PIN } from "../src/index/pins.js";

describe("pins", () => {
  it("pins graphify to an exact release (set in Task 7 Step 1)", () => {
    expect(GRAPHIFY_PIN).toMatch(/^\d+\.\d+\.\d+$/);
  });
});
```

`sindri/tests/index-setup.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import type { IndexProbes } from "../src/index/probes.js";
import { GRAPHIFY_PIN } from "../src/index/pins.js";
import { runSetup } from "../src/index/setup.js";
import { ProfileSchema } from "../src/profile/schema.js";
import type { LoadedProfile } from "../src/profile/load.js";

const loaded = (index: object = {}): LoadedProfile => ({
  root: "/p", files: [], bytes: {}, hash: "h", raw: { profile: {}, repos: {} }, repos: {},
  profile: ProfileSchema.parse({ schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"], index }),
});

function probes(o: { bins?: string[]; tags?: unknown; graphify?: string; netProbeCode?: number } = {}): IndexProbes & { ran: string[][] } {
  const ran: string[][] = [];
  return {
    ran,
    has: (b) => (o.bins ?? ["ollama", "uv", "sandbox-exec", "curl", "graphify"]).includes(b),
    getJson: async () => (o.tags === undefined ? { models: [{ name: "nomic-embed-text:latest" }] } : o.tags),
    run: async (argv) => {
      ran.push(argv);
      if (argv[0] === "graphify") return { code: o.graphify === undefined ? 0 : 1, stdout: `graphify ${o.graphify ?? GRAPHIFY_PIN}\n`, stderr: "" };
      if (argv[0] === "sandbox-exec") return { code: o.netProbeCode ?? 6, stdout: "", stderr: "" };
      return { code: 0, stdout: "", stderr: "" };
    },
  };
}

describe("sindri index setup", () => {
  it("reports everything ok when installed, pinned and sandboxed", async () => {
    const r = await runSetup(loaded(), probes(), { dryRun: false, platform: "darwin" });
    expect(r.steps.map((s) => [s.name, s.status])).toEqual([["ollama", "ok"], ["ollama-server", "ok"], ["embedding-model", "ok"], ["graphify", "ok"], ["sandbox", "ok"]]);
  });

  it("pulls a missing model and installs the pinned graphify, or says what it would do", async () => {
    const p = probes({ tags: { models: [] }, graphify: "missing" });
    const dry = await runSetup(loaded(), p, { dryRun: true, platform: "darwin" });
    expect(dry.steps.find((s) => s.name === "embedding-model")).toMatchObject({ status: "would", detail: "ollama pull nomic-embed-text" });
    expect(dry.steps.find((s) => s.name === "graphify")).toMatchObject({ status: "would", detail: `uv tool install graphifyy==${GRAPHIFY_PIN}` });
    expect(p.ran.some((a) => a[0] === "ollama")).toBe(false);
    const real = await runSetup(loaded(), probes({ tags: { models: [] }, graphify: "missing" }), { dryRun: false, platform: "darwin" });
    expect(real.steps.find((s) => s.name === "embedding-model")?.status).toBe("done");
    expect(real.steps.find((s) => s.name === "graphify")?.status).toBe("done");
  });

  it("fails with a fix when Ollama or the server is missing, or the sandbox leaks", async () => {
    const noOllama = await runSetup(loaded(), probes({ bins: ["uv", "sandbox-exec", "curl", "graphify"] }), { dryRun: false, platform: "darwin" });
    expect(noOllama.steps[0]).toMatchObject({ name: "ollama", status: "fail", fix: "install Ollama (https://ollama.com/download), then rerun" });
    const down = await runSetup(loaded(), { ...probes(), getJson: async () => null }, { dryRun: false, platform: "darwin" });
    expect(down.steps.find((s) => s.name === "ollama-server")).toMatchObject({ status: "fail", fix: "start Ollama (the app, or `ollama serve`), then rerun" });
    const leak = await runSetup(loaded(), probes({ netProbeCode: 0 }), { dryRun: false, platform: "darwin" });
    expect(leak.steps.find((s) => s.name === "sandbox")).toMatchObject({ status: "fail", detail: "a network request succeeded inside the sandbox" });
    const noSandbox = await runSetup(loaded(), probes({ bins: ["ollama", "uv", "curl", "graphify"] }), { dryRun: false, platform: "linux" });
    expect(noSandbox.steps.find((s) => s.name === "sandbox")?.status).toBe("fail");
    const pipx = await runSetup(loaded(), probes({ bins: ["ollama", "pipx", "sandbox-exec"], graphify: "missing" }), { dryRun: true, platform: "darwin" });
    expect(pipx.steps.find((s) => s.name === "graphify")?.detail).toBe(`pipx install graphifyy==${GRAPHIFY_PIN}`);
    const neither = await runSetup(loaded(), probes({ bins: ["ollama", "sandbox-exec"], graphify: "missing" }), { dryRun: false, platform: "darwin" });
    expect(neither.steps.find((s) => s.name === "graphify")?.status).toBe("fail");
  });

  it("skips layers the profile turns off", async () => {
    const r = await runSetup(loaded({ embeddings: { enabled: false }, graph: "none" }), probes(), { dryRun: false, platform: "darwin" });
    expect(r.steps).toEqual([]);
  });
});
```

`sindri/tests/repo-add.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { runCli } from "../src/main.js";
import { gitRepo, makeDeps } from "./helpers.js";

describe("sindri repo add", () => {
  it("adds a repo to the live profile, keeps comments, mirrors it, and asks for approval", async () => {
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    const target = gitRepo({ "a.ts": "export const a = 1;\n" });
    const r = await runCli(["repo", "add", target, "--name", "webapp"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Added webapp");
    expect(r.stdout).toContain("sindri profile approve");
    const profile = fs.readFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "utf8");
    expect(profile).toMatch(/repos:\n {2}- example\n {2}- webapp/);
    expect(profile).toContain("# Example Sindri profile.");
    expect(fs.readFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "repos", "webapp.yaml"), "utf8")).toContain(`path: ${fs.realpathSync(target)}`);
    expect(fs.existsSync(path.join(d.env.AW_STATE_DIR as string, "sindri", "mirrors", "webapp.git", "HEAD"))).toBe(true);
    expect((await runCli(["profile", "validate"], d)).exitCode).toBe(0);
    const again = await runCli(["repo", "add", target, "--name", "webapp"], d);
    expect(again.stdout).toContain("already in the profile; mirror refreshed");
  });

  it("refuses a non-repo path, a name clash with another path, and a missing profile", async () => {
    const d = makeDeps();
    expect((await runCli(["repo", "add", "."], d)).stderr).toContain("SND-PROFILE-002");
    await runCli(["profile", "init"], d);
    expect((await runCli(["repo", "add", "/"], d)).stderr).toContain("SND-PROFILE-009");
    const a = gitRepo({ "a.ts": "1" });
    const b = gitRepo({ "b.ts": "2" });
    await runCli(["repo", "add", a, "--name", "same"], d);
    expect((await runCli(["repo", "add", b, "--name", "same"], d)).stderr).toContain("SND-PROFILE-013");
    expect((await runCli(["repo"], d)).stderr).toContain("SND-CLI-002");
  });
});
```

Add to `sindri/tests/doctor.test.ts`:

```ts
import type { IndexProbes } from "../src/index/probes.js";
import { GRAPHIFY_PIN } from "../src/index/pins.js";
import { heavyLockDir } from "../src/index/heavy-lock.js";
import { awStateDir } from "../src/deps.js";

const goodProbes: IndexProbes = {
  has: () => true,
  getJson: async () => ({ models: [{ name: "nomic-embed-text:latest" }] }),
  run: async (argv) => ({ code: argv[0] === "graphify" ? 0 : 6, stdout: `graphify ${GRAPHIFY_PIN}\n`, stderr: "" }),
};

describe("doctor index checks", () => {
  it("warns on a missing index, then reports it once built; checks embeddings, graphify and the heavy lock", async () => {
    const root = tempDir("sindri-doc-ix-");
    fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
    execFileSync("git", ["init", "-q"], { cwd: root });
    const d = makeDeps({ cwd: root });
    await runCli(["profile", "init", "--ring0"], d);
    const file = path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    fs.appendFileSync(file, "index:\n  graph: none\n"); // embeddings stay on; probes are faked
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    const before = Object.fromEntries((await runChecks(d, "22.0.0", goodProbes)).map((c) => [c.name, c]));
    const indexCheck = Object.entries(before).find(([k]) => k.startsWith("index:"))?.[1];
    expect(indexCheck).toMatchObject({ status: "warn", detail: "no index" });
    expect(before.embeddings).toMatchObject({ status: "ok", detail: "nomic-embed-text on http://127.0.0.1:11434" });
    expect(before.graphify).toMatchObject({ status: "ok", detail: "off (index.graph: none)" });
    expect(before["heavy-lock"]).toMatchObject({ status: "ok", detail: "free" });
    const down = Object.fromEntries((await runChecks(d, "22.0.0", { ...goodProbes, getJson: async () => null })).map((c) => [c.name, c]));
    expect(down.embeddings).toMatchObject({ status: "warn", fix: "sindri index setup" });
    fs.mkdirSync(heavyLockDir(awStateDir(d)), { recursive: true });
    const old = new Date(Date.now() - 7 * 3_600_000);
    fs.utimesSync(heavyLockDir(awStateDir(d)), old, old);
    const stuck = Object.fromEntries((await runChecks({ ...d, now: () => new Date() }, "22.0.0", goodProbes)).map((c) => [c.name, c]));
    expect(stuck["heavy-lock"].status).toBe("warn");
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd sindri && npx vitest run tests/pins.test.ts tests/index-setup.test.ts tests/repo-add.test.ts tests/doctor.test.ts`
Expected: FAIL with `Failed to load url ../src/index/setup.js` (and `probes.js`, `repo-add.js`). `pins.test.ts` passes once Task 7 Step 1 set a real version.

- [ ] **Step 3: Implement**

`sindri/src/index/probes.ts`:

```ts
import type { ProcessRunner } from "./graph.js";

// What `index setup` and `doctor` need from the machine. Real: sandbox-real.ts.
export interface IndexProbes {
  has(bin: string): boolean;
  run: ProcessRunner["run"];
  getJson(url: string, timeoutMs: number): Promise<unknown | null>;
}
```

Add to `sindri/src/index/sandbox-real.ts`:

```ts
import type { IndexProbes } from "./probes.js";

export function realIndexProbes(): IndexProbes {
  const runner = realProcessRunner();
  return {
    has: hasBinary,
    run: (argv, o) => runner.run(argv, o),
    getJson: async (url, timeoutMs) => {
      try {
        const res = await fetch(url, { signal: AbortSignal.timeout(timeoutMs) });
        return res.ok ? ((await res.json()) as unknown) : null;
      } catch {
        return null;
      }
    },
  };
}
```

`sindri/src/index/setup.ts`:

```ts
import type { LoadedProfile } from "../profile/load.js";
import { sandboxArgv } from "./graph.js";
import { GRAPHIFY_PIN } from "./pins.js";
import type { IndexProbes } from "./probes.js";

type Step = { name: string; status: "ok" | "done" | "would" | "fail"; detail: string; fix?: string };

function hasModel(tags: unknown, model: string): boolean {
  const models = (tags as { models?: { name?: unknown }[] } | null)?.models ?? [];
  return models.some((m) => typeof m.name === "string" && (m.name === model || m.name.startsWith(`${model}:`)));
}

// Spec §11.3 index dependencies: Ollama + the embedding model, pinned graphify,
// and a network sandbox that really denies the network.
export async function runSetup(loaded: LoadedProfile, probes: IndexProbes, o: { dryRun: boolean; platform: NodeJS.Platform }): Promise<{ steps: Step[] }> {
  const steps: Step[] = [];
  const ix = loaded.profile.index;
  const act = async (name: string, argv: string[]): Promise<Step> => {
    if (o.dryRun) return { name, status: "would", detail: argv.join(" ") };
    const r = await probes.run(argv, { cwd: "/", timeoutMs: 1_800_000 });
    return r.code === 0 ? { name, status: "done", detail: argv.join(" ") } : { name, status: "fail", detail: `${argv.join(" ")} exited ${r.code}`, fix: `run \`${argv.join(" ")}\` by hand and read its output` };
  };
  if (ix.embeddings.enabled) {
    if (!probes.has("ollama")) {
      steps.push({ name: "ollama", status: "fail", detail: "ollama is not installed", fix: "install Ollama (https://ollama.com/download), then rerun" });
      return { steps };
    }
    steps.push({ name: "ollama", status: "ok", detail: "installed" });
    const tags = await probes.getJson(`${ix.embeddings.url.replace(/\/+$/, "")}/api/tags`, 3000);
    if (tags === null) {
      steps.push({ name: "ollama-server", status: "fail", detail: `no answer from ${ix.embeddings.url}`, fix: "start Ollama (the app, or `ollama serve`), then rerun" });
      return { steps };
    }
    steps.push({ name: "ollama-server", status: "ok", detail: ix.embeddings.url });
    steps.push(hasModel(tags, ix.embeddings.model) ? { name: "embedding-model", status: "ok", detail: ix.embeddings.model } : await act("embedding-model", ["ollama", "pull", ix.embeddings.model]));
  }
  if (ix.graph === "graphify") {
    const v = probes.has("graphify") ? await probes.run(["graphify", "--version"], { cwd: "/", timeoutMs: 30_000 }) : null;
    const installed = v !== null && v.code === 0 ? (/(\d+\.\d+\.\d+)/.exec(v.stdout)?.[1] ?? null) : null;
    if (installed === GRAPHIFY_PIN) {
      steps.push({ name: "graphify", status: "ok", detail: `graphify ${installed}` });
    } else if (probes.has("uv")) {
      steps.push(await act("graphify", ["uv", "tool", "install", `graphifyy==${GRAPHIFY_PIN}`]));
    } else if (probes.has("pipx")) {
      steps.push(await act("graphify", ["pipx", "install", `graphifyy==${GRAPHIFY_PIN}`]));
    } else {
      steps.push({ name: "graphify", status: "fail", detail: "neither uv nor pipx is installed", fix: "install uv (https://docs.astral.sh/uv/), then rerun" });
    }
    const probe = sandboxArgv(o.platform, ["curl", "-sS", "--max-time", "3", "https://example.com"], (b) => probes.has(b));
    if (probe === null) {
      steps.push({ name: "sandbox", status: "fail", detail: "no network sandbox (sandbox-exec or bwrap)", fix: "Linux: install bubblewrap; or set index.graph: none" });
    } else {
      const r = await probes.run(probe, { cwd: "/", timeoutMs: 15_000 });
      steps.push(r.code !== 0 ? { name: "sandbox", status: "ok", detail: "network denied inside the sandbox" } : { name: "sandbox", status: "fail", detail: "a network request succeeded inside the sandbox", fix: "report this; graphify stays off until the sandbox denies the network" });
    }
  }
  return { steps };
}
```

`sindri/src/index/repo-add.ts`:

```ts
import fs from "node:fs";
import path from "node:path";
import YAML from "yaml";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { requireProfileRoot, sanitizeName } from "../profile/commands.js";
import { loadProfile } from "../profile/load.js";
import { PROFILE_SCHEMA_VERSION } from "../profile/schema.js";

export async function repoAdd(deps: Deps, target: string, name?: string): Promise<{ name: string; path: string; mirror: string; added: boolean }> {
  const root = requireProfileRoot(deps);
  const top = await deps.git.run(["rev-parse", "--show-toplevel"], path.resolve(deps.cwd, target));
  if (!top.ok) throw new SindriError("SND-PROFILE-009", `${target} is not inside a git repo`);
  const repoPath = fs.realpathSync(top.stdout.trim());
  const repoName = name ?? sanitizeName(path.basename(repoPath));
  const loaded = loadProfile(root);
  const existing = loaded.ok ? loaded.value.repos[repoName] : undefined;
  if (existing !== undefined && fs.realpathSync(existing.path) !== repoPath) {
    throw new SindriError("SND-PROFILE-013", `repo name ${repoName} is already used for ${existing.path}`, { fix: "pass --name <another name>" });
  }
  let added = false;
  if (existing === undefined) {
    const repoFile = path.join(root, "repos", `${repoName}.yaml`);
    fs.mkdirSync(path.dirname(repoFile), { recursive: true, mode: 0o700 });
    fs.writeFileSync(repoFile, YAML.stringify({ schemaVersion: PROFILE_SCHEMA_VERSION, name: repoName, path: repoPath, defaultBranch: "main", protectedPaths: [] }), { flag: "wx", mode: 0o600 });
    const doc = YAML.parseDocument(fs.readFileSync(path.join(root, "profile.yaml"), "utf8"));
    const repos = doc.get("repos");
    if (YAML.isSeq(repos)) repos.add(repoName);
    else doc.set("repos", [repoName]);
    fs.writeFileSync(path.join(root, "profile.yaml"), doc.toString());
    added = true;
  }
  const mirror = path.join(stateDir(deps), "mirrors", `${repoName}.git`);
  fs.mkdirSync(path.dirname(mirror), { recursive: true, mode: 0o700 });
  const r = fs.existsSync(mirror)
    ? await deps.git.run(["--git-dir", mirror, "fetch", "--prune", "--quiet"], repoPath)
    : await deps.git.run(["clone", "--mirror", "--quiet", repoPath, mirror], repoPath);
  if (!r.ok) throw new SindriError("SND-INDEX-002", `could not mirror ${repoPath}: ${r.stderr.split("\n")[0]}`);
  return { name: repoName, path: repoPath, mirror, added };
}
```

In `sindri/src/profile/commands.ts`, export a helper used above (and refactor `validate`/`migrate` to use it):

```ts
export function requireProfileRoot(deps: Deps, flag?: string): string {
  const root = resolveProfileRoot(deps, flag);
  if (root === null) throw new SindriError("SND-PROFILE-002", "no profile found");
  return root;
}
```

In `sindri/src/index/commands.ts`, add `setup` (`sindri index setup [--dry-run] [--json]`): load the **live** profile with `requireProfile` (setup prepares the machine, so it may run before approval), call `runSetup(loaded, realIndexProbes(), { dryRun, platform: deps.system.platform })`, print one line per step as `<status padded to 5> <name>  <detail>` with an indented `fix:` line for `fail`, and exit 2 when any step failed. Add a `repo` command in `sindri/src/main.ts`: `sindri repo add <path> [--name NAME] [--json]` calls `repoAdd`. It prints `Added <name> (<path>). Mirror: <mirror>. The profile changed: sindri profile approve, then sindri index build --repo <name>.`, or `<name> is already in the profile; mirror refreshed.` Any other subcommand is `SND-CLI-002`.

In `sindri/src/doctor/doctor.ts`, give `runChecks` a third parameter `probes: IndexProbes = realIndexProbes()` and, after the profile checks (only when the profile is valid), append:

```ts
async function indexChecks(deps: Deps, loaded: LoadedProfile, probes: IndexProbes): Promise<Check[]> {
  const out: Check[] = [];
  const ix = loaded.profile.index;
  for (const repo of Object.keys(loaded.repos).sort()) {
    const db = openIndexReadOnly(indexPath(deps, repo));
    if (db === null) {
      out.push({ name: `index:${repo}`, status: "warn", detail: "no index", fix: `sindri index build --repo ${repo}` });
      continue;
    }
    const m = meta(db);
    const bad = layers(db).filter((l) => l.status === "unavailable");
    db.close();
    const ageH = (deps.now().getTime() - Date.parse(m.builtAt ?? "")) / 3_600_000;
    if (!(ageH <= ix.maxAgeHours)) out.push({ name: `index:${repo}`, status: "warn", detail: `stale (built ${Math.floor(ageH)} h ago)`, fix: `sindri index build --repo ${repo}` });
    else if (bad.length > 0) out.push({ name: `index:${repo}`, status: "warn", detail: bad.map((l) => `${l.layer} unavailable: ${l.detail}`).join("; "), fix: "sindri index setup" });
    else out.push({ name: `index:${repo}`, status: "ok", detail: `built ${Math.floor(ageH)} h ago` });
  }
  if (!ix.embeddings.enabled) out.push({ name: "embeddings", status: "ok", detail: "off (index.embeddings.enabled: false)" });
  else {
    const tags = await probes.getJson(`${ix.embeddings.url.replace(/\/+$/, "")}/api/tags`, 2000);
    const has = JSON.stringify(tags ?? {}).includes(`"${ix.embeddings.model}`);
    out.push(has ? { name: "embeddings", status: "ok", detail: `${ix.embeddings.model} on ${ix.embeddings.url}` } : { name: "embeddings", status: "warn", detail: tags === null ? "Ollama not answering on loopback" : `model ${ix.embeddings.model} not pulled`, fix: "sindri index setup" });
  }
  if (ix.graph === "none") out.push({ name: "graphify", status: "ok", detail: "off (index.graph: none)" });
  else {
    const v = probes.has("graphify") ? await probes.run(["graphify", "--version"], { cwd: "/", timeoutMs: 30_000 }) : null;
    const pinned = v !== null && v.stdout.includes(GRAPHIFY_PIN);
    const sandboxed = sandboxArgv(deps.system.platform, ["true"], (b) => probes.has(b)) !== null;
    out.push(pinned && sandboxed ? { name: "graphify", status: "ok", detail: `${GRAPHIFY_PIN}, sandboxed` } : { name: "graphify", status: "warn", detail: !pinned ? `not installed at ${GRAPHIFY_PIN}` : "no network sandbox", fix: "sindri index setup" });
  }
  const heavy = heavyLockState(awStateDir(deps), deps.now);
  if (!heavy.held) out.push({ name: "heavy-lock", status: "ok", detail: "free" });
  else if ((heavy.ageMs ?? 0) > 6 * 3_600_000) out.push({ name: "heavy-lock", status: "warn", detail: `held for over 6 h${heavy.holder === null ? "" : ` by ${heavy.holder.kind} (pid ${heavy.holder.pid})`}`, fix: `if that process is gone: rmdir ${heavyLockDir(awStateDir(deps))}` });
  else out.push({ name: "heavy-lock", status: "ok", detail: `held by ${heavy.holder?.kind ?? "an unknown job"}` });
  return out;
}
```

Add to `ERRORS`:

```ts
  "SND-PROFILE-013": { summary: "That repo name is already used for another path.", fix: "pass --name <another name>" },
```

- [ ] **Step 4: Run the tests**

Run: `cd sindri && npm run gen && npx vitest run && npm run typecheck && npm run test:coverage`
Expected: all tests PASS; 100% coverage. Before running, update Plan 2's `sindri/tests/doctor.test.ts` so it never touches Ollama or graphify. In `ring0Deps()`, after `profile init --ring0`, append `index:\n  embeddings:\n    enabled: false\n  graph: none\n` to `$AW_STATE_DIR/profile/profile.yaml`. In the "is all ok after ring-0 init, approve and pre-commit install" test, run `await runCli(["index", "build"], d);` after the approval, so the new `index:<repo>` check is `ok`.

- [ ] **Step 5: Commit**

```bash
git add sindri/src sindri/tests docs/sindri/errors.md
git commit -m "feat: sindri repo add, index setup and doctor index checks"
```

---

### Task 11: Docs, merge gate and spec amendments

**Files:**
- Create: `docs/sindri/index.md`
- Modify: `docs/sindri/README.md`, `AGENTS.md`, `.agents/rules/testing.md`, `planning/ERD.md`, `planning/ARCHITECTURE.md`, `docs/superpowers/specs/2026-10-07-sindri-design.md`

**Interfaces:**
- Consumes: everything in Tasks 1–10.
- Produces: the index documentation and the amended spec. No code.

- [ ] **Step 1: Write `docs/sindri/index.md`**

````markdown
# Sindri code index

One index per repo at `$AW_STATE_DIR/sindri/index/<repo>.db`, built from tracked files only (`git ls-files`, minus `index.denyPaths`; symlinks never followed). Spec: §6.2.

| Layer | What it holds | Built with | Off switch |
|---|---|---|---|
| structure | functions, methods, arrows, classes: name, signature, lines, exported, complexity, callees | TypeScript compiler API (TS and JS) | — |
| clones | normalized AST hash per symbol; MinHash/LSH bands over 5-token shingles | core | — |
| deps | `package.json` dependencies with purpose tags (date, http, id, validation, …) | core | — |
| embeddings | one vector per symbol | Ollama on loopback (`index.embeddings`) | `index.embeddings.enabled: false` |
| graph | module and call graph | graphify in a network-less sandbox, on a snapshot of the tracked files | `index.graph: none` |

**Offline guarantee.** Code never leaves the machine. The embedding URL must be loopback, or the profile is invalid. graphify runs under `sandbox-exec` (macOS) or `bwrap --unshare-net` (Linux) and doesn't run at all without one. `sindri index setup` proves the sandbox denies the network.

## Setup and use

```bash
sindri index setup              # checks Ollama, pulls the embedding model, installs pinned graphify, probes the sandbox
sindri index build              # incremental; --full rebuilds; takes the box-wide heavy-job lock
sindri index status             # age, commit and per-layer status; exit 1 when stale
sindri index query <name>       # a symbol's exact and near clones
sindri repo add <path>          # another repo: profile entry + bare mirror (then sindri profile approve)
```

A failed layer is `unavailable` with its reason; the other layers stay usable. Builds write a temp copy and rename it, so an interrupted build leaves the previous index in place.

## Shape signals (record-only)

The pre-commit hook (installed by `sindri scrub --install-pre-commit`) scans for secrets, then runs `sindri shape --record --staged`. That compares the staged changes (an in-memory overlay) with the index and writes the signals to `$AW_STATE_DIR/sindri/spool/`. It always exits 0: nothing blocks a commit until rollout step 3b. The hourly `observe` and `sindri shape report` move the spool into the ledger.

| Signal | Fires when | Default threshold (`shape.thresholds`) |
|---|---|---|
| `reinvented:exact` | a new symbol has the same normalized AST as another file's symbol | — |
| `generalize:near-clone` | MinHash similarity with another symbol, both at least N tokens | Jaccard 0.8, 60 tokens |
| `reinvented:name` | name and signature similar to an exported or utility symbol | 0.85 |
| `reinvented:graph` | call set overlaps an exported or utility symbol's (≥ 3 calls each) | 0.5 |
| `reinvented:embedding` | embedding cosine and AST similarity both high (only within the commit budget) | 0.9 and 0.6 |
| `reinvented:dependency` | a new dependency shares a purpose tag with an existing one | — |
| `simpler:diff-size` | added lines over the size budget (`shape.defaultSize`, default S = 250) | `shape.sizeBudget` |
| `simpler:complexity` | a symbol's complexity grew by more than the limit | 10 |
| `simpler:exports` | more new exports than the size class allows | `shape.exportAllowance` |

Names in signal details are wrapped in `<untrusted>…</untrusted>`: they come from the repo, not from Sindri.
````

- [ ] **Step 2: Update the other docs and the spec**

- `docs/sindri/README.md`: add rows for `sindri index setup|build|status|query`, `sindri repo add`, and `sindri shape --record --staged | report`, and a line under "Where things live" for `$AW_STATE_DIR/sindri/index/<repo>.db`, `…/spool/` and `…/mirrors/<repo>.git`.
- `AGENTS.md` Commands: add `sindri index setup && sindri index build    # code index (Ollama + graphify, offline)` and `sindri shape report                      # record-only shape signals`.
- `.agents/rules/testing.md`: add `src/index/sandbox-real.ts` to the `sindri` coverage excludes, update the `sindri` test count from `npx vitest run`, then run `scripts/sync-rules.sh`.
- `planning/ERD.md`: add `shape_runs` and `shape_signals` (ledger v2) to the Sindri ledger diagram, one attribute per line, and a `## Sindri code index` section with the index tables from `src/index/db.ts` (`meta`, `layers`, `files`, `symbols`, `bands`, `deps`, `embeddings`, `graph_nodes`, `graph_edges`), noting the index is rebuilt, not migrated.
- `planning/ARCHITECTURE.md` `## Sindri`: add one paragraph on the index: five layers, offline guarantee, temp-copy builds under the heavy-job lock, record-only shape signals through the spool.
- `docs/superpowers/specs/2026-10-07-sindri-design.md`:
  - §6.2 index table, Structure row "Built with": "TypeScript compiler API for TS/JS (v1); tree-sitter grammars for other languages in a later plan; LSP (Serena) when available".
  - §6.2 Embeddings row: "Ollama on loopback (`index.embeddings.url` must be loopback)".
  - §6.2 Offline guarantee: add "graphify runs on a snapshot of the tracked, non-denied files, never on the working tree."
  - §6.2 Signals intro: add "Until rollout step 3b, signals are recorded only: the pre-commit hook writes them to the spool and never blocks."
  - §8.2 Heavy-job lock: add "Host side: a `mkdir` lock at `$AW_STATE_DIR/locks/heavy`, the same primitive as `config/lib/locks.sh`."
  - §13.3 row "Code index, record-only shape signals (P3)": switch-on command becomes `sindri index setup && sindri index build`, then `sindri scrub --install-pre-commit` (upgrades the hook to v2, which records shape signals), and a daily `sindri index build` (launchd).

- [ ] **Step 3: Run the merge gate, one job at a time**

```bash
cd sindri && npm run typecheck && npm run test:coverage && cd ..
bash scripts/tests/install-sindri.test.sh
scripts/sync-rules.sh --check
./setup.sh --providers claude,codex,cursor --dry-run > /dev/null && echo SETUP_DRY_RUN_OK
```

Expected: no type errors; 100% coverage; installer tests PASS; `sync-rules` exits 0; `SETUP_DRY_RUN_OK`.

- [ ] **Step 4: Commit**

```bash
git add docs/sindri AGENTS.md .agents/rules/testing.md planning docs/superpowers/specs/2026-10-07-sindri-design.md
git commit -m "docs: sindri code index docs, ERD and spec amendments"
```

---

### Task 12: Turn it on (bootstrapping ladder, spec §13.3 row 7)

From the merge on, every commit in this repo records shape signals against a daily-refreshed index, so the thresholds that rollout step 3b will enforce are calibrated on Sindri's own Plan 4 and Plan 5 commits. Steps 1–4 run on the PR branch; steps 5–6 run after merge.

**Files:**
- Create: `config/launchd/com.agentic-workflow.sindri-index.plist`
- Modify: `scripts/install-sindri.sh` (install the daily job), `scripts/tests/install-sindri.test.sh`

- [ ] **Step 1: Write the failing test**

Append to `scripts/tests/install-sindri.test.sh` (and add it to the list of calls):

```bash
test_index_job_is_daily() {
  local plist="$ROOT/config/launchd/com.agentic-workflow.sindri-index.plist"
  [ -f "$plist" ] || { echo "FAIL: $plist missing"; exit 1; }
  if command -v plutil >/dev/null 2>&1; then plutil -lint "$plist" >/dev/null || { echo "FAIL: plist invalid"; exit 1; }; fi
  grep -q '<string>__BIN__/sindri</string>' "$plist" && grep -q '<string>build</string>' "$plist" || { echo "FAIL: index build command missing"; exit 1; }
  grep -q '<key>Hour</key>' "$plist" || { echo "FAIL: not daily"; exit 1; }
  grep -q 'com.agentic-workflow.sindri-index' "$ROOT/scripts/install-sindri.sh" || { echo "FAIL: installer does not install the job"; exit 1; }
  echo "PASS: test_index_job_is_daily"
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `bash scripts/tests/install-sindri.test.sh`
Expected: the earlier tests PASS, then `FAIL: …/com.agentic-workflow.sindri-index.plist missing`.

- [ ] **Step 3: Implement**

`config/launchd/com.agentic-workflow.sindri-index.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>com.agentic-workflow.sindri-index</string>
  <key>ProgramArguments</key>
  <array>
    <string>__BIN__/sindri</string>
    <string>index</string>
    <string>build</string>
  </array>
  <key>StartCalendarInterval</key>
  <dict>
    <key>Hour</key>
    <integer>3</integer>
    <key>Minute</key>
    <integer>15</integer>
  </dict>
  <key>StandardOutPath</key>
  <string>__HOME__/.agentic-workflow/sindri/index-launchd.log</string>
  <key>StandardErrorPath</key>
  <string>__HOME__/.agentic-workflow/sindri/index-launchd.log</string>
</dict>
</plist>
```

In `scripts/install-sindri.sh`, turn the single-job launchd block from Plan 2 into a loop over `com.agentic-workflow.sindri-observe` and `com.agentic-workflow.sindri-index` (same `sed`, `bootout` and `bootstrap` lines), and change the message to `"  sindri: hourly observe, daily index build at 03:15 (launchd)"`. Add the second job to the dry-run lines.

- [ ] **Step 4: Run the tests and commit**

Run: `bash scripts/tests/install-sindri.test.sh`
Expected: every test PASS, including `test_index_job_is_daily`.

```bash
git add config/launchd/com.agentic-workflow.sindri-index.plist scripts/install-sindri.sh scripts/tests/install-sindri.test.sh
git commit -m "feat: daily sindri index build"
```

- [ ] **Step 5: After merge, switch on (builder)**

The profile file is unchanged by this plan (new keys have defaults), so the approved profile stays approved. No human step is needed.

```bash
scripts/install-sindri.sh              # CLI + hourly observe + daily index build
sindri index setup                     # Ollama model, pinned graphify, sandbox probe (heavy: run alone)
sindri index build                     # heavy: run alone
sindri index status
sindri scrub --install-pre-commit      # upgrades the hook to v2 (scan, then record shape signals)
sindri doctor; echo "doctor exit: $?"
```

Expected: every `index setup` step `ok` or `done`; `index status` shows `structure ok, clones ok, deps ok, embeddings ok, graph ok`; `doctor exit: 0`.

- [ ] **Step 6: Prove it records, then post the evidence**

Make the first ordinary commit of Plan 4 work (or any real commit), then:

```bash
sindri shape report
```

Expected: `Ingested N run(s), M signal(s).` with N ≥ 1, and a table of signal types (it may be empty if the commit was clean, which is also evidence). Post the Step 5 and Step 6 output as a comment on the Plan 3 PR. From then on (row 7):
- every build commit is recorded against the index;
- `shape report` shows per-layer counts, which rollout step 3b uses to set per-layer precision and decide enforcement.

## Done criteria for this plan
- Merge gate (AGENTS.md) green for `sindri`, plus `bash scripts/tests/install-sindri.test.sh`, `scripts/sync-rules.sh --check` and `./setup.sh --providers claude,codex,cursor --dry-run`.
- Every Review Focus item (1–5) has its pinned test passing.
- `GRAPHIFY_PIN` is a real version at least 14 days old, and the graphify fixture was recorded from it.
- **Switched on (Task 12):** after merge, `index status` shows every layer `ok`, `doctor` exits 0, and `shape report` has ingested at least one real commit's run. The evidence is posted on the PR. Plan 4 starts from `sindri observe`'s `Next up:`.
