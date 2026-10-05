// judge/src/prompt-sort/session-state.ts
// Per-session cooldown + the "UI evidence required" flag done-gate.sh reads.
// File: <stateDir>/judge/sessions/<sid>.sort.json. That directory is already
// swept for *.json older than 24h by turn-origin.sh, so a stale requirement
// expires on its own. Never touches <sid>.json (done-gate's auto-continue file).
import fs from "node:fs";
import path from "node:path";

import { SCAFFOLD_IDS, type ScaffoldId } from "./scaffolds.js";

// session_id becomes a file name: only a conservative character set is accepted (RF-4).
export const SESSION_ID_RE = /^[A-Za-z0-9._-]{1,80}$/;

export interface SortSessionState {
  prompts: number;
  lastFired: Partial<Record<ScaffoldId, number>>;
  requirements: { uiEvidence?: true };
}

export const emptyState = (): SortSessionState => ({ prompts: 0, lastFired: {}, requirements: {} });

export function sortStatePath(stateDir: string, sessionId: string): string | null {
  if (!SESSION_ID_RE.test(sessionId) || sessionId.includes("..")) return null;
  return path.join(stateDir, "judge", "sessions", `${sessionId}.sort.json`);
}

export function readSortState(file: string): SortSessionState {
  try {
    const raw = JSON.parse(fs.readFileSync(file, "utf8")) as Record<string, unknown>;
    if (typeof raw.prompts !== "number") return emptyState();
    const lastFired: Partial<Record<ScaffoldId, number>> = {};
    const rawFired = raw.lastFired;
    if (typeof rawFired === "object" && rawFired !== null && !Array.isArray(rawFired)) {
      for (const id of SCAFFOLD_IDS) {
        const n = (rawFired as Record<string, unknown>)[id];
        if (typeof n === "number") lastFired[id] = n;
      }
    }
    const req = raw.requirements;
    const uiEvidence = typeof req === "object" && req !== null && (req as { uiEvidence?: unknown }).uiEvidence === true;
    return { prompts: raw.prompts, lastFired, requirements: uiEvidence ? { uiEvidence: true } : {} };
  } catch {
    return emptyState();
  }
}

export function writeSortState(file: string, state: SortSessionState): void {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const tmp = `${file}.${process.pid}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(state));
  fs.renameSync(tmp, file);
}
