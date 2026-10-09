import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../src/deps.js";
import { buildIndex } from "../src/index/build.js";
import { loadProfile, type LoadedProfile } from "../src/profile/load.js";
import { gitRepo, tempDir } from "./helpers.js";

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
