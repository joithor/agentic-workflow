import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import type { Deps } from "../deps.js";
import { ulid } from "../ids.js";
import { overlayDir, overlayFile, sha256 } from "./overlay.js";
import type { PromptId } from "./prompts.js";

// A symlinked or non-directory overlay (or overlay/prompts) would make adopt report success while inspectOverlay refuses the file.
function requirePlainDirs(deps: Deps): void {
  for (const dir of [path.dirname(overlayDir(deps)), overlayDir(deps)]) {
    const st = fs.lstatSync(dir, { throwIfNoEntry: false });
    if (st !== undefined && !st.isDirectory()) {
      throw new SindriError("SND-EVOLVE-004", `${dir} isn't a plain directory (a symlink or a file), so an adopted prompt there would never be used`, { fix: `remove ${dir} and run sindri evolve adopt again` });
    }
  }
}

// Atomic: a temp file in the same directory, then a rename (which replaces a symlink rather than writing through it).
export function writeOverlay(deps: Deps, id: PromptId, text: string): { file: string; sha: string } {
  requirePlainDirs(deps);
  fs.mkdirSync(overlayDir(deps), { recursive: true, mode: 0o700 });
  const file = overlayFile(deps, id);
  const tmp = path.join(overlayDir(deps), `.${id}.tmp-${ulid(deps.now())}`);
  fs.writeFileSync(tmp, text, { flag: "wx", mode: 0o600 });
  fs.renameSync(tmp, file);
  return { file, sha: sha256(text) };
}

export const removeOverlay = (deps: Deps, id: PromptId): boolean => {
  const present = fs.lstatSync(overlayFile(deps, id), { throwIfNoEntry: false }) !== undefined;
  fs.rmSync(overlayFile(deps, id), { force: true });
  return present;
};

// The overlay file as it is now (null when there is none or it isn't a plain file), so a failed adoption can put it back.
export function snapshotOverlay(deps: Deps, id: PromptId): string | null {
  const st = fs.lstatSync(overlayFile(deps, id), { throwIfNoEntry: false });
  return st?.isFile() === true ? fs.readFileSync(overlayFile(deps, id), "utf8") : null;
}

export function restoreOverlay(deps: Deps, id: PromptId, snapshot: string | null): void {
  if (snapshot === null) removeOverlay(deps, id);
  else writeOverlay(deps, id, snapshot);
}
