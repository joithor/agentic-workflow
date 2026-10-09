import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

import { stateDir, type Deps } from "../src/deps.js";
import { SindriError } from "../src/errors.js";
import { writers, type EvolveCtx, type EvolveIo } from "../src/evolve/ctx.js";
import type { ProcessRunner } from "../src/index/io.js";
import { STATUSES, transition, type ProposalStatus } from "../src/evolve/proposals.js";
import { ledgerPath, openLedger, type Ledger } from "../src/ledger/db.js";
import { runCli } from "../src/main.js";
import { requireApprovedProfile } from "../src/profile/approve.js";
import type { ModelCall, ModelRunner } from "../src/scope/model.js";
import { gitRepo, makeDeps, tempDir } from "./helpers.js";

export function git(root: string, ...args: string[]): string {
  return execFileSync("git", ["-c", "user.name=Tester", "-c", "user.email=tester@example.com", ...args], { cwd: root, encoding: "utf8" });
}

// A ProcessRunner whose answers come from a function; records every call.
export function fakeProc(
  handler: (argv: string[], cwd: string) => { code?: number; stdout?: string; stderr?: string },
): ProcessRunner & { calls: { argv: string[]; cwd: string }[] } {
  const calls: { argv: string[]; cwd: string }[] = [];
  return {
    calls,
    run: async (argv, o) => {
      calls.push({ argv, cwd: o.cwd });
      const r = handler(argv, o.cwd);
      return { code: r.code ?? 0, stdout: r.stdout ?? "", stderr: r.stderr ?? "" };
    },
  };
}

// A ModelRunner that answers from a function. Like the real runner, it reports an answer that
// doesn't match the schema as SND-SCOPE-004. Every call and input is recorded.
export function answeringRunner(
  answer: (call: ModelCall<unknown>) => unknown, usage = { inputTokens: 1, outputTokens: 1 },
): ModelRunner & { calls: ModelCall<unknown>[]; inputs: string[] } {
  const calls: ModelCall<unknown>[] = [];
  const inputs: string[] = [];
  return {
    calls,
    inputs,
    async run<T>(call: ModelCall<T>) {
      const asUnknown = call as ModelCall<unknown>;
      calls.push(asUnknown);
      inputs.push(call.input);
      const a = answer(asUnknown);
      try {
        return { value: call.parse(a), usage };
      } catch (e) {
        throw new SindriError("SND-SCOPE-004", `the model's answer didn't match the schema: ${(e as Error).message.slice(0, 200)}`);
      }
    },
  };
}

export type ScriptedEvolveIo = EvolveIo & { calls: ModelCall<unknown>[] };

// An EvolveIo whose model answers come from `script`; every call is recorded.
export function scriptedEvolveIo(script: (call: ModelCall<unknown>) => unknown, proc?: ProcessRunner): ScriptedEvolveIo {
  const runner = answeringRunner(script);
  return {
    calls: runner.calls,
    runner: () => runner,
    fetch: async () => ({ ok: false, status: 500, json: async () => ({}) }), // matches Plan 4's GraphqlFetch result: { ok, status, json() }
    process: proc ?? { run: async () => ({ code: 127, stdout: "", stderr: "no process expected" }) },
    progress: () => undefined, // Plan 4's ScopeIo requires it
  };
}

const SEED = "# Seed\n\n### Task 1: Seed work\n\n- [ ] **Step 1: x**\n";

export interface EvolveFixture {
  ctx: EvolveCtx;
  deps: Deps;
  repo: string;
  transcripts: string;
  io: EvolveIo;
  close(): void;
}

// A temp ring-0 git repo on branch main, an approved profile whose transcripts dir is a fresh
// temp dir, and an open ledger. `extraYaml` is appended at column 0 (top-level keys).
export async function evolveFixture(
  o: { files?: Record<string, string>; extraYaml?: string; io?: EvolveIo; plans?: string; prompts?: EvolveCtx["prompts"] } = {},
): Promise<EvolveFixture> {
  const transcripts = tempDir("sindri-transcripts-");
  const root = gitRepo({ "docs/superpowers/plans/2026-10-01-sindri-plan-0-seed.md": SEED, ...(o.files ?? {}) });
  const deps = makeDeps({ cwd: root });
  const init = await runCli(["profile", "init", "--ring0", "--plans", o.plans ?? "*-sindri-plan-*", "--json"], deps);
  if (init.exitCode !== 0) throw new Error(init.stderr);
  fs.appendFileSync(
    path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml"),
    `index:\n  embeddings:\n    enabled: false\n  graph: none\nsources:\n  transcripts:\n    enabled: true\n    dir: ${transcripts}\n${o.extraYaml ?? ""}`,
  );
  const pending = await runCli(["profile", "approve", "--json"], deps);
  const hash = (JSON.parse(pending.stdout) as { hash: string }).hash;
  const done = await runCli(["profile", "approve", hash], { ...deps, isTTY: true, prompt: async () => hash.slice(0, 6) });
  if (done.exitCode !== 0) throw new Error(done.stderr);
  const db = openLedger(ledgerPath(stateDir(deps)));
  const loaded = requireApprovedProfile(deps, db);
  const io = o.io ?? scriptedEvolveIo(() => { throw new Error("no model call expected"); });
  const repo = loaded.repos[loaded.profile.tracker.repo].path;
  const ctx: EvolveCtx = { deps, io, loaded, db, repo, prompts: o.prompts ?? (() => []), ...writers(deps, db) };
  return { ctx, deps, repo, transcripts, io, close: () => db.close() };
}

// The same context with some Deps replaced (a TTY, a fake git, a log collector).
export function withDeps(ctx: EvolveCtx, over: Partial<Deps>): EvolveCtx {
  const deps = { ...ctx.deps, ...over };
  return { ...ctx, deps, ...writers(deps, ctx.db) };
}

// Unconditional status change for arranging fixtures; commands move a status only through `transition`.
export const setStatus = (db: Ledger, id: string, status: ProposalStatus, epoch: number, now: Date): boolean => transition(db, id, STATUSES, status, epoch, now);
