import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { ulid } from "../ids.js";
import { overlayDir, overlayFile, sha256 } from "./overlay.js";
import type { PromptId } from "./prompts.js";

// Atomic: a temp file in the same directory, then a rename (which replaces a symlink rather than writing through it).
export function writeOverlay(deps: Deps, id: PromptId, text: string): { file: string; sha: string } {
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
