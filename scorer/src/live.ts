import fs from "node:fs";
import path from "node:path";

import type { CliOptions } from "./args.js";
import { firesForSession, readFiresLog } from "./context-guard-fires.js";
import { openDb } from "./db.js";
import type { Db } from "./db.js";
import { ingestAll } from "./ingest.js";
import { readJudgeLive } from "./live-judge.js";
import type { LiveSnapshot } from "./live-snapshot.js";
import { sessionUsage } from "./live-snapshot.js";
import { queuedForSession } from "./outbox.js";
import { formatPct, formatTokens } from "./render.js";
import { resolveStateDir } from "./run.js";
import { discoverSession } from "./transcript/discover.js";
import type { TranscriptFile } from "./transcript/source.js";
import { projectFromCwd } from "./transcript/source.js";

const PRUNE_ABOVE = 30; // files in the live dir before an old-session sweep runs
const MAX_AGE_MS = 7 * 86_400_000;

// The cwd names the project directory directly; without it (or when the session moved
// projects) every project directory is checked.
export function findSessionFiles(projectsDir: string, sessionId: string, cwd: string | null): TranscriptFile[] {
  if (cwd !== null) {
    const hinted = discoverSession(projectsDir, sessionId, projectFromCwd(cwd));
    if (hinted.length > 0) return hinted;
  }
  return discoverSession(projectsDir, sessionId);
}

// Per-session databases keep the live path off scorer.sqlite (the daily report's) and make
// "this session" a whole database, not a filter. Sessions untouched for a week are swept.
export function pruneLiveDbs(dir: string, keepSession: string, nowMs: number): number {
  const entries = fs.readdirSync(dir);
  if (entries.length <= PRUNE_ABOVE) return 0;
  let removed = 0;
  for (const name of entries) {
    if (name.startsWith(`${keepSession}.`)) continue;
    const file = path.join(dir, name);
    const stat = fs.statSync(file, { throwIfNoEntry: false });
    if (stat !== undefined && nowMs - stat.mtimeMs > MAX_AGE_MS) {
      fs.rmSync(file, { force: true });
      removed += 1;
    }
  }
  return removed;
}

function openLiveDb(stateDir: string, sessionId: string, hasFiles: boolean, nowMs: number): Db {
  if (!hasFiles) return openDb(":memory:");
  const dir = path.join(stateDir, "scorer", "live");
  fs.mkdirSync(dir, { recursive: true });
  pruneLiveDbs(dir, sessionId, nowMs);
  return openDb(path.join(dir, `${sessionId}.sqlite`));
}

// `scorer live`: this session's numbers. Incremental: each call reads only the bytes
// appended to the session's transcripts since the last call (ingest.ts keeps the offsets).
export function runLive(options: CliOptions, nowMs: () => number = Date.now): LiveSnapshot {
  const sessionId = options.liveSession as string; // args.ts guarantees it for the live command
  const stateDir = resolveStateDir(options);
  const started = performance.now();
  const files = findSessionFiles(options.projectsDir, sessionId, options.liveCwd);
  const db = openLiveDb(stateDir, sessionId, files.length > 0, nowMs());
  try {
    const health = ingestAll(db, files);
    const { context, usage } = sessionUsage(db, options.liveWindow);
    return {
      v: 1,
      sessionId,
      at: new Date(nowMs()).toISOString(),
      transcript: { found: files.length > 0, files: files.length },
      ingest: { newLines: health.lines, readErrors: health.readErrors.length, ms: Math.round(performance.now() - started) },
      context,
      usage,
      judge: readJudgeLive(path.join(stateDir, "judge", "decisions.sqlite"), sessionId),
      contextGuard: { fires: firesForSession(readFiresLog(path.join(stateDir, "context-guard")), sessionId).length },
      wakes: { queuedNow: queuedForSession(path.join(stateDir, "judge", "outbox"), sessionId) },
    };
  } finally {
    db.close();
  }
}

export function renderLive(s: LiveSnapshot): string {
  const lines = [
    s.transcript.found
      ? `context   ${s.context.tokens === null ? "unknown" : `${formatTokens(s.context.tokens)} of ${formatTokens(s.context.window)} (${s.context.percent}%)`}`
      : "context   no transcript found for this session",
    `session   ${s.usage.calls} calls (${s.usage.subagentCalls} subagent) · ${formatTokens(s.usage.contextTokens)} in · ${formatTokens(s.usage.outputTokens)} out · ${s.usage.callsOver200k} over 200k`,
  ];
  if (s.judge.state === "unscoped") lines.push("judge     not linked to sessions yet (judge predates decision_details.session_id)");
  if (s.judge.state === "ok") {
    const j = s.judge;
    const providers = Object.entries(j.byProvider).map(([name, n]) => `${name} ${formatPct(n / Math.max(1, j.calls))}`).join(" · ");
    lines.push(`judge     ${j.calls} calls${providers === "" ? "" : ` · ${providers}`} · agreed ${j.agreed} · overrode ${j.overrode} · unsure ${j.undecided} · p50 ${j.p50LatencyMs}ms`);
    lines.push(`gates     ${Object.entries(j.gates).map(([name, g]) => `${name} ${g.fired}`).join(" · ")}`);
  }
  if (s.ingest.readErrors > 0) lines.push(`warning   ${s.ingest.readErrors} transcript read errors, numbers may be stale`);
  lines.push(`wakes     ${s.wakes.queuedNow} queued · context-guard ${s.contextGuard.fires} fires`);
  return `${lines.join("\n")}\n`;
}
