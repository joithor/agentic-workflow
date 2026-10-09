import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import { stateDir, type Deps } from "../deps.js";
import { ledgerPath, readLedger } from "../ledger/db.js";
import { defaultPrompt, hasSafetyClause, PROMPT_IDS, type PromptId } from "./prompts.js";

export const overlayDir = (deps: Deps): string => path.join(stateDir(deps), "overlay", "prompts");
export const overlayFile = (deps: Deps, id: PromptId): string => path.join(overlayDir(deps), `${id}.txt`);

const MAX_BYTES = 65_536;
export const sha256 = (s: string): string => createHash("sha256").update(s).digest("hex");

export type OverlayState = "none" | "active" | "unsafe" | "unadopted" | "no-clause";

// Read-only (readLedger): doctor and every scope run call this, and neither may migrate, back up or
// chmod the ledger. Any failure (no file, a newer or older schema, a locked file) means no adoption.
function latestAdoption(deps: Deps, id: PromptId): string | null {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return null;
  try {
    return readLedger(file, (db) => {
      const row = db.prepare("SELECT sha256 FROM adoptions WHERE prompt_id = ? ORDER BY seq DESC LIMIT 1").get(id) as { sha256: string } | undefined;
      return row?.sha256 ?? null;
    });
  } catch {
    return null;
  }
}

// An overlay is used only if it is a plain, private, small file in a real directory, keeps the
// safety clause, and is exactly what `evolve adopt` recorded (its sha256 matches the latest
// adoptions row). Otherwise the built-in prompt is used and doctor warns.
export function inspectOverlay(deps: Deps, id: PromptId): { state: OverlayState; text: string | null } {
  const none = { state: "none" as const, text: null };
  const dir = fs.lstatSync(overlayDir(deps), { throwIfNoEntry: false });
  if (dir === undefined) return none;
  if (!dir.isDirectory()) return { state: "unsafe", text: null };
  let fd: number;
  try {
    fd = fs.openSync(overlayFile(deps, id), fs.constants.O_RDONLY | fs.constants.O_NOFOLLOW);
  } catch (e) {
    return (e as NodeJS.ErrnoException).code === "ENOENT" ? none : { state: "unsafe", text: null };
  }
  try {
    const st = fs.fstatSync(fd);
    if (!st.isFile() || (st.mode & 0o022) !== 0 || st.size > MAX_BYTES) return { state: "unsafe", text: null };
    const text = fs.readFileSync(fd, "utf8");
    if (!hasSafetyClause(id, text)) return { state: "no-clause", text: null };
    return sha256(text) === latestAdoption(deps, id) ? { state: "active", text } : { state: "unadopted", text: null };
  } finally {
    fs.closeSync(fd);
  }
}

export const loadPrompt = (deps: Deps, id: PromptId): string => inspectOverlay(deps, id).text ?? defaultPrompt(id);

export const effectivePrompts = (deps: Deps): { id: PromptId; text: string }[] => PROMPT_IDS.map((id) => ({ id, text: loadPrompt(deps, id) }));

const REASONS: Record<Exclude<OverlayState, "none" | "active">, string> = {
  unsafe: "the file is unsafe: not a plain private file in a real directory",
  unadopted: "its hash doesn't match the latest adoption",
  "no-clause": "it is missing the safety clause",
};

export function overlayProblems(deps: Deps): string[] {
  return PROMPT_IDS.flatMap((id) => {
    const s = inspectOverlay(deps, id).state;
    return s === "none" || s === "active" ? [] : [`${id}: ignored (${REASONS[s]})`];
  });
}

// Shaped like doctor's Check; doctor.ts adds it to its list.
export function evolveOverlayCheck(deps: Deps): { name: string; status: "ok" | "warn"; detail: string; fix?: string } {
  const problems = overlayProblems(deps);
  return problems.length === 0
    ? { name: "evolve-overlay", status: "ok", detail: "no ignored prompt overlays" }
    : { name: "evolve-overlay", status: "warn", detail: problems.join("; "), fix: "sindri evolve adopt <proposal-id> to adopt it properly, or delete the file in the overlay dir" };
}
