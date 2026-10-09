import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../src/deps.js";
import { buildIndex } from "../src/index/build.js";
import type { ProcessRunner } from "../src/index/io.js";
import { loadProfile, type LoadedProfile } from "../src/profile/load.js";
import type { ScopeIo } from "../src/scope/commands.js";
import { ModelAnswerError, type ModelCall, type ModelRunner, type ModelUsage } from "../src/scope/model.js";
import type { GraphqlFetch } from "../src/scope/sources/linear.js";
import { gitRepo, tempDir } from "./helpers.js";
import { approvedIndexDeps, OFF, ring0Repo } from "./index-fixtures.js";

// A loaded profile with one repo `name` over the given files, embeddings and graph off.
export function scopeProfile(root: string, name = "r", extra = ""): LoadedProfile {
  const dir = tempDir("sindri-scope-prof-");
  fs.mkdirSync(path.join(dir, "repos"));
  fs.writeFileSync(path.join(dir, "profile.yaml"), `schemaVersion: 1\nuser: me\nhosts:\n  active: test-host\ntracker:\n  type: plan-file\n  repo: ${name}\nrepos:\n  - ${name}\nindex:\n  embeddings:\n    enabled: false\n  graph: none\n${extra}`);
  fs.writeFileSync(path.join(dir, `repos/${name}.yaml`), `schemaVersion: 1\nname: ${name}\npath: ${root}\n`);
  const r = loadProfile(dir);
  if (!r.ok) throw new Error(JSON.stringify(r.issues));
  return r.value;
}

export async function buildIndexForTest(d: Deps, name: string, files: Record<string, string>): Promise<LoadedProfile> {
  const root = gitRepo(files);
  const p = scopeProfile(root, name);
  await buildIndex(d, p, name, { full: false }, { embedder: null, graph: null });
  return p;
}

// A model runner whose answers come from a script, in order (an Error is thrown,
// anything else is parsed by the call's schema, a mismatch is a ModelAnswerError).
// Every answer costs `usage` (default 55 tokens). It records each call and its input.
export function scriptedRunner(
  answers: unknown[],
  usage: ModelUsage = { inputTokens: 50, outputTokens: 5 },
): ModelRunner & { inputs: string[]; calls: { role: string; model: string; input: string }[] } {
  const inputs: string[] = [];
  const calls: { role: string; model: string; input: string }[] = [];
  return {
    inputs,
    calls,
    async run<T>(call: ModelCall<T>) {
      inputs.push(call.input);
      calls.push({ role: call.role, model: call.model, input: call.input });
      const a = answers.shift();
      if (a instanceof Error) throw a;
      try {
        return { value: call.parse(a), usage };
      } catch (e) {
        throw new ModelAnswerError(`the model's answer didn't match the schema: ${(e as Error).message.slice(0, 80)}`, usage);
      }
    },
  };
}

// A ScopeIo over a scripted runner. It also records each progress line.
export function scriptedIo(answers: unknown[], fetch?: GraphqlFetch): ScopeIo & { inputs: string[]; lines: string[] } {
  const lines: string[] = [];
  const runner = scriptedRunner(answers);
  const proc: ProcessRunner = { run: async () => ({ code: 0, stdout: "tok\n", stderr: "" }) };
  return {
    inputs: runner.inputs, lines, runner: () => runner, process: proc, progress: (l) => { lines.push(l); },
    fetch: fetch ?? (async () => ({ ok: false, status: 500, json: async () => ({}) })),
  };
}

// An approved ring-0 profile in a git repo. `sourcesYaml` is the indented body of a `sources:` key;
// `extraYaml` is appended as further top-level keys (for example a `scope:` budget); `extraRepos`
// adds more profile repos (see approvedIndexDeps).
export async function approvedScopeDeps(sourcesYaml = "", extraYaml = "", extraRepos?: { name: string; path: string }[]): Promise<Deps> {
  const root = ring0Repo({ "src/a.ts": "export const a = 1;\n" });
  const sources = sourcesYaml === "" ? "" : `sources:\n${sourcesYaml}`;
  return approvedIndexDeps(root, { index: `${OFF}${sources}${extraYaml}`, extraRepos });
}
