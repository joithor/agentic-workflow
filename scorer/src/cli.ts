#!/usr/bin/env node
import { execFile } from "node:child_process";
import os from "node:os";
import { promisify } from "node:util";

import { makeClaudeRunner } from "./audit/claude-runner.js";
import { runAudit } from "./audit/run-audit.js";
import { parseArgs } from "./args.js";
import { estimateCurrentContextTokens } from "./context-tokens.js";
import { makeGhLookup } from "./pr-state.js";
import { renderLive, runLive } from "./live.js";
import { runProbe } from "./probe/run-probe.js";
import { resolveJudgeDbPath, runReport } from "./run.js";

const USAGE = [
  "usage: scorer live --session ID [--cwd DIR] [--window TOKENS] [--json]",
  "       scorer [probe] [--since 7d|12h|ISO] [--provider claude|codex|cursor|all] [--projects-dir DIR] [--codex-dir DIR] [--cursor-dir DIR] [--state-dir DIR] [--no-pr-lookup]",
  "       scorer audit [--since 60d; default 1d] [--out DIR] [--item-pattern RE] [--items FILE] [--max-size XS|S|M|L|XL] [--label N] [--label-repeat K] [--label-model MODEL]",
  "",
  "audit --label N (default 0 = off, fully offline) has a model label N sampled human turns to calibrate the regex patterns",
  "and measure wrong-approach corrections. It sends the text of those turns (and the tail of the preceding assistant message)",
  "to the model provider your Claude Code login already uses, one `claude -p` call per 20 turns plus ceil(K/20) repeat calls (up to 2x with retries).",
  "Everything is written under --out (default ~/.agentic-workflow/audit); never commit labels.jsonl or human-turns.jsonl.",
].join("\n");

const exec = promisify(execFile);
const parsed = parseArgs(process.argv.slice(2), new Date(), os.homedir());
if (!parsed.ok) {
  console.error(`scorer: ${parsed.error}`);
  console.error(USAGE);
  process.exit(1);
}
if (parsed.options.help) {
  console.log(USAGE);
  process.exit(0);
}
if (parsed.options.command === "probe") {
  process.stdout.write(runProbe(parsed.options.stateDir));
} else if (parsed.options.command === "live") {
  // Fail silent for the mod: any error is a bare exit 1 with nothing on stdout.
  try {
    const snapshot = runLive(parsed.options);
    process.stdout.write(parsed.options.json ? `${JSON.stringify(snapshot)}\n` : renderLive(snapshot));
  } catch {
    process.exit(1);
  }
} else if (parsed.options.command === "context-tokens") {
  const tokens = estimateCurrentContextTokens(parsed.options.contextTokensPath as string);
  process.stdout.write(tokens === null ? "null" : String(tokens));
} else if (parsed.options.command === "audit") {
  const o = parsed.options;
  try {
    const label =
      o.label > 0
        ? { n: o.label, repeat: o.labelRepeat, model: o.labelModel, runner: makeClaudeRunner({ model: o.labelModel }), windowDays: (o.until.getTime() - o.since.getTime()) / 86_400_000 }
        : undefined;
    const s = await runAudit({ projectsDir: o.projectsDir, since: o.since, outDir: o.auditOut, itemPattern: new RegExp(o.itemPattern), itemsFile: o.itemsFile, maxSize: o.maxSize, label });
    console.log(`scorer audit: ${s.sessions} sessions, ${s.turns} turns, ${s.duplicates} copied turns skipped → ${o.auditOut}/baseline.md`);
    if (s.labeling !== null) console.log(`scorer audit: labeled ${s.labeling.labeled}/${s.labeling.sampled} sampled turns, ${s.labeling.labelErrors} failed batches`);
  } catch (e) {
    console.error(`scorer audit: ${(e as Error).message}`);
    process.exit(1);
  }
} else {
  const lookup = makeGhLookup(async (cmd, args) => (await exec(cmd, args, { timeout: 10_000 })).stdout);
  const result = await runReport(parsed.options, { lookup, log: (l) => console.log(l), judgeDbPath: resolveJudgeDbPath(parsed.options) });
  if (result.status === "unknown-format") {
    console.error(`scorer: ⚠ UNKNOWN TRANSCRIPT FORMAT — see ${result.markdownPath}`);
    process.exit(3);
  }
}
