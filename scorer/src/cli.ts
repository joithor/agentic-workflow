#!/usr/bin/env node
import { execFile } from "node:child_process";
import os from "node:os";
import { promisify } from "node:util";

import { runAudit } from "./audit/run-audit.js";
import { parseArgs } from "./args.js";
import { estimateCurrentContextTokens } from "./context-tokens.js";
import { makeGhLookup } from "./pr-state.js";
import { renderLive, runLive } from "./live.js";
import { runProbe } from "./probe/run-probe.js";
import { resolveJudgeDbPath, runReport } from "./run.js";

const exec = promisify(execFile);
const parsed = parseArgs(process.argv.slice(2), new Date(), os.homedir());
if (!parsed.ok) {
  console.error(`scorer: ${parsed.error}`);
  console.error("usage: scorer live --session ID [--cwd DIR] [--window TOKENS] [--json] | scorer [probe] [--since 7d|12h|ISO] [--provider claude|codex|cursor|all] [--projects-dir DIR] [--codex-dir DIR] [--cursor-dir DIR] [--state-dir DIR] [--no-pr-lookup] | scorer audit [--since 60d] [--out DIR] [--item-pattern RE] [--items FILE] [--max-size XS|S|M|L|XL]");
  process.exit(1);
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
    const s = await runAudit({ projectsDir: o.projectsDir, since: o.since, outDir: o.auditOut, itemPattern: new RegExp(o.itemPattern), itemsFile: o.itemsFile, maxSize: o.maxSize });
    console.log(`scorer audit: ${s.sessions} sessions, ${s.turns} turns → ${o.auditOut}/baseline.md`);
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
