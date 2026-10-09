import fs from "node:fs";
import path from "node:path";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";

export const mirrorPath = (deps: Deps, name: string): string => path.join(stateDir(deps), "mirrors", `${name}.git`);

// A bare mirror of the repo: all refs, full history, unfiltered (see docs/sindri/index.md).
// Every full `sindri index build` creates or refreshes it; `repo add` does not.
export async function refreshMirror(deps: Deps, name: string, repoPath: string): Promise<string> {
  const mirror = mirrorPath(deps, name);
  fs.mkdirSync(path.dirname(mirror), { recursive: true, mode: 0o700 });
  const r = fs.existsSync(mirror)
    ? await deps.git.run(["--git-dir", mirror, "fetch", "--prune", "--quiet"], repoPath)
    : await deps.git.run(["clone", "--mirror", "--quiet", repoPath, mirror], repoPath);
  if (!r.ok) throw new SindriError("SND-INDEX-002", `could not mirror ${repoPath}: ${r.stderr.split("\n")[0]}`);
  return mirror;
}
