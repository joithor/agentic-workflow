import fs from "node:fs";
import path from "node:path";
import YAML from "yaml";

import type { Deps } from "../src/deps.js";
import type { GitRunner } from "../src/git.js";
import { realGitRunner } from "../src/git-real.js";
import type { FetchLike, IndexIo } from "../src/index/io.js";
import { runCli } from "../src/main.js";
import { loadProfile, type LoadedProfile } from "../src/profile/load.js";
import { gitRepo, makeDeps, tempDir } from "./helpers.js";

// CLI tests switch the network layers off, so nothing talks to Ollama or runs graphify.
export const OFF = "index:\n  embeddings:\n    enabled: false\n  graph: none\n";

// An IndexIo that behaves like a machine with nothing installed: Ollama refuses, no binaries.
export function fakeIndexIo(over: Partial<IndexIo> = {}): IndexIo {
  return {
    fetch: async () => {
      throw new Error("connect ECONNREFUSED");
    },
    probes: { has: () => false, run: async () => ({ code: 127, stdout: "", stderr: "not found" }), getJson: async () => null },
    ...over,
  };
}

// A temp repo that `profile init --ring0` accepts (it needs a plans directory).
export const ring0Repo = (files: Record<string, string>): string => gitRepo({ ...files, "docs/superpowers/plans/p.md": "# P\n" });

// Deps with this repo as ring 0 and an approved profile. `index` is appended to profile.yaml
// before approval; `extraRepos` adds repos whose path is a plain (non-git) temp dir.
export async function approvedIndexDeps(root: string, o: { index?: string; extraRepos?: string[] } = {}): Promise<Deps> {
  const d = makeDeps({ cwd: root });
  await runCli(["profile", "init", "--ring0"], d);
  const dir = path.join(d.env.AW_STATE_DIR as string, "profile");
  fs.appendFileSync(path.join(dir, "profile.yaml"), o.index ?? OFF);
  if (o.extraRepos !== undefined) {
    const doc = YAML.parseDocument(fs.readFileSync(path.join(dir, "profile.yaml"), "utf8"));
    for (const name of o.extraRepos) {
      fs.writeFileSync(path.join(dir, "repos", `${name}.yaml`), YAML.stringify({ schemaVersion: 1, name, path: tempDir("sindri-ghost-"), defaultBranch: "main", protectedPaths: [] }));
      doc.addIn(["repos"], name);
    }
    fs.writeFileSync(path.join(dir, "profile.yaml"), doc.toString());
  }
  const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
  await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
  return d;
}

// The ring-0 repo's name in the profile (the first entry of `repos`).
export function ring0Name(d: Deps): string {
  const text = fs.readFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "utf8");
  return (YAML.parse(text) as { repos: string[] }).repos[0];
}

// A live (unapproved) profile for one repo named `r`, for tests that call buildIndex directly.
// `yaml` is more children of `index:` (two-space indent).
export function profileFor(root: string, o: { utility?: string[]; yaml?: string } = {}): LoadedProfile {
  const dir = tempDir("sindri-prof-");
  fs.mkdirSync(path.join(dir, "repos"));
  const utility = o.utility ?? ["src/util/**"];
  const globs = utility.length === 0 ? "  utilityGlobs: []\n" : `  utilityGlobs:\n${utility.map((g) => `    - "${g}"\n`).join("")}`;
  fs.writeFileSync(
    path.join(dir, "profile.yaml"),
    `schemaVersion: 1\nuser: me\nhosts:\n  active: test-host\ntracker:\n  type: plan-file\n  repo: r\nrepos:\n  - r\nindex:\n${globs}${o.yaml ?? ""}`,
  );
  fs.writeFileSync(path.join(dir, "repos/r.yaml"), `schemaVersion: 1\nname: r\npath: ${root}\n`);
  const r = loadProfile(dir);
  if (!r.ok) throw new Error(JSON.stringify(r.issues));
  return r.value;
}

// A 79-token function. BODY("a") and BODY("b") are exact clones; `extra` makes a near-clone.
export const BODY = (name: string, extra = ""): string => `export function ${name}(items: string[], limit: number): string[] {
  const out: string[] = [];
  for (const item of items) {
    if (item.length > limit) { out.push(item.slice(0, limit)); } else { out.push(item.trim()); }
  }
  ${extra}
  return out.filter((x) => x !== "").map((x) => x.toLowerCase());
}
`;

// The same body as a method with a computed name (the name is repo-controlled text).
export const METHOD = (cls: string, key: string): string => `export class ${cls} {
  [${JSON.stringify(key)}](items: string[], limit: number): string[] {
    const out: string[] = [];
    for (const item of items) {
      if (item.length > limit) { out.push(item.slice(0, limit)); } else { out.push(item.trim()); }
    }
    return out.filter((x) => x !== "").map((x) => x.toLowerCase());
  }
}
`;

// Real git, except that any call whose arguments contain one of the needles fails.
export function failingGit(...needles: string[]): GitRunner {
  const real = realGitRunner();
  return {
    run: async (args, cwd) => (needles.some((n) => args.join(" ").includes(n)) ? { ok: false, stderr: `injected failure: ${args.join(" ")}` } : real.run(args, cwd)),
  };
}

// An Ollama /api/embed that answers `vec(text)` for every input.
export function embedFetch(vec: (text: string) => number[] = () => [1, 0, 0]): FetchLike {
  return async (_url, init) => {
    const input = (JSON.parse(init.body) as { input: string[] }).input;
    return { ok: true, status: 200, json: async () => ({ embeddings: input.map(vec) }) };
  };
}
