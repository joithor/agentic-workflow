#!/usr/bin/env node
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import os from "node:os";
import path from "node:path";
import fs from "node:fs";

import {
  runApprove, runAskCheckCli, runBriefGet, runBriefMapByDispatch, runBriefMapByName, runBriefSave, runBriefSetAgentId,
  runConfigGet, runConfigSet, runHealth, runQuestion, runUiElementRepairCli, runUndo, runVisualCritiqueCli, runWhy,
} from "./commands.js";
import { adjudicate } from "./adjudicate.js";
import { buildChain } from "./chain.js";
import { judgeConfigPath, judgeDbPath, judgeStateDir, loadConfig, HOOK_KILL_MS } from "./config.js";
import { runPromptSortCommand } from "./prompt-sort/commands.js";
import { openDb, pruneDecisionDetails } from "./db.js";
import { makeRecorder, readReplay, runEval, writeEvalReport } from "./eval.js";
import { runLabelImport, runLabelSet, runLabelStatus } from "./label.js";
import { runOutcomeLabels } from "./outcomes.js";
import { AGENT_CLI_BINARIES, isOnPath, resolveAgentClis } from "./detect.js";
import { makeClaudeCliProvider } from "./providers/claude-cli.js";
import { makeCodexCliProvider } from "./providers/codex-cli.js";
import { makeCursorCliProvider } from "./providers/cursor-cli.js";
import { makeExecSpawn } from "./providers/exec-spawn.js";
import { makeJevProvider } from "./providers/jev.js";
import { makeRulesProvider } from "./providers/rules.js";
import { readApiKey } from "./keychain.js";
import type { AgentCliName, Provider } from "./types.js";

const exec = promisify(execFile);
// AW_STATE_DIR overrides ~/.agentic-workflow wholesale, so a smoke run or a
// test harness can point at a scratch state dir while still using the real
// HOME (and its real agent-CLI logins) for everything else.
const dbPath = judgeDbPath();
fs.mkdirSync(path.dirname(dbPath), { recursive: true });
// The hook path (`judge prompt-sort` with no subcommand) fails open: a db that
// will not open must never hold up or fail a prompt.
const isHookSort = process.argv[2] === "prompt-sort" && process.argv.length === 3;
function openDbOrExit(): ReturnType<typeof openDb> {
  try {
    return openDb(dbPath);
  } catch (e) {
    if (isHookSort) process.exit(0);
    throw e;
  }
}
const db = openDbOrExit();
try {
  pruneDecisionDetails(db, new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString());
} catch {
  /* retention is best-effort */
}
const config = loadConfig(judgeConfigPath());

async function readStdin(): Promise<string> {
  const chunks: Buffer[] = [];
  for await (const chunk of process.stdin) chunks.push(chunk as Buffer);
  return Buffer.concat(chunks).toString("utf8");
}

async function readKeychain(service: string, account: string): Promise<string | null> {
  try {
    const { stdout } = await exec("security", ["find-generic-password", "-s", service, "-a", account, "-w"]);
    return stdout.trim() || null;
  } catch {
    return null;
  }
}

const tmpDirFactory = (): string => fs.mkdtempSync(path.join(os.tmpdir(), "judge-cli-"));

const agentCliFactories: Record<AgentCliName, () => Provider> = {
  "claude-cli": () => makeClaudeCliProvider({ tmpDirFactory, spawn: makeExecSpawn(AGENT_CLI_BINARIES["claude-cli"]) }),
  "codex-cli": () =>
    makeCodexCliProvider({ tmpDirFactory, spawn: makeExecSpawn(AGENT_CLI_BINARIES["codex-cli"]), writeFile: (file, contents) => fs.writeFileSync(file, contents) }),
  "cursor-cli": () => makeCursorCliProvider({ tmpDirFactory, spawn: makeExecSpawn(AGENT_CLI_BINARIES["cursor-cli"]) }),
};

// Agent CLIs: installed ones only, config order > AW_PROVIDER > claude, codex, cursor.
const agentClis = resolveAgentClis({
  available: (name) => isOnPath(AGENT_CLI_BINARIES[name], process.env),
  awProvider: process.env.AW_PROVIDER,
  configured: config.providers?.agentClis,
});
const chain = buildChain({ agentClis, jev: config.providers?.jev ?? true });

const providers: Provider[] = [
  makeRulesProvider(),
  ...agentClis.map((name) => agentCliFactories[name]()),
  makeJevProvider({ fetch: (...args) => fetch(...args), apiKey: () => readApiKey({ env: process.env, readKeychain }) }),
];

const sessionId = process.env.AW_SESSION_ID;
const [, , cmd, ...rest] = process.argv;

function flag(name: string): string | undefined {
  const i = rest.indexOf(name);
  return i === -1 ? undefined : rest[i + 1];
}

async function main(): Promise<{ exitCode: number; stdout: string; stderr?: string }> {
  switch (cmd) {
    case "undo":
      return runUndo(db, rest[0] ?? "", () => new Date());
    case "why":
      return runWhy(db, rest[0] ?? "");
    case "approve":
      return runApprove(db, rest[0] ?? "");
    case "health":
      return runHealth(db);
    case "config": {
      if (rest[0] === "get") return runConfigGet();
      if (rest[0] === "set") return runConfigSet(rest[1] ?? "", rest[2] as "enabled" | "threshold", rest[3] ?? "");
      return { exitCode: 1, stdout: "", stderr: "usage: judge config get|set <question> <enabled|threshold> <value>" };
    }
    case "label": {
      const sub = rest[0];
      if (sub === "import") {
        const days = Number.parseInt((flag("--since") ?? "14d").replace(/d$/, ""), 10);
        const sinceIso = new Date(Date.now() - (Number.isFinite(days) ? days : 14) * 24 * 60 * 60 * 1000).toISOString();
        return runLabelImport(db, { question: flag("--question"), sinceIso }, () => new Date());
      }
      if (sub === "outcomes") {
        const r = runOutcomeLabels(db, { projectsDir: path.join(os.homedir(), ".claude", "projects"), now: () => new Date() });
        return { exitCode: 0, stdout: JSON.stringify(r) };
      }
      if (sub === "set") return runLabelSet(db, rest[1] ?? "", rest[2] ?? "", () => new Date());
      if (sub === "status") return runLabelStatus(db);
      return { exitCode: 1, stdout: "", stderr: "usage: judge label import|outcomes|set <itemId> <label|skip>|status" };
    }
    case "adjudicate": {
      if (!isOnPath(AGENT_CLI_BINARIES["claude-cli"], process.env)) return { exitCode: 1, stdout: "", stderr: "claude CLI not found on PATH" };
      const provider = makeClaudeCliProvider({ tmpDirFactory, spawn: makeExecSpawn(AGENT_CLI_BINARIES["claude-cli"]), model: "opus", effort: "high" });
      const limit = Number.parseInt(flag("--limit") ?? "60", 10);
      try {
        const r = await adjudicate(db, rest[0] ?? "", { provider, limit: Number.isFinite(limit) ? limit : 60, now: () => new Date() });
        return { exitCode: 0, stdout: JSON.stringify(r) };
      } catch (e) {
        return { exitCode: 1, stdout: "", stderr: (e as Error).message };
      }
    }
    case "eval": {
      const q = rest[0] ?? "";
      const providerName = flag("--provider") ?? "jev";
      const provider = providers.find((p) => p.name === providerName && p.name !== "rules");
      if (provider === undefined) return { exitCode: 1, stdout: "", stderr: `provider not available: ${providerName}` };
      const labels = flag("--labels") ?? "any";
      if (labels !== "any" && labels !== "outcome" && labels !== "adjudicator" && labels !== "override") return { exitCode: 1, stdout: "", stderr: "--labels must be outcome, adjudicator, override or any" };
      const recordFile = flag("--record");
      const replayFile = flag("--replay");
      let replay;
      try {
        replay = replayFile === undefined ? undefined : readReplay(replayFile);
      } catch (e) {
        return { exitCode: 1, stdout: "", stderr: `cannot read replay file: ${(e as Error).message}` };
      }
      const r = await runEval(db, { question: q, provider, variant: flag("--variant") ?? "as-is", labels, record: recordFile === undefined ? undefined : makeRecorder(recordFile), replay });
      if (r.report !== undefined) writeEvalReport(path.join(judgeStateDir(), "judge", "evals"), r.report, Date.now());
      return r;
    }
    case "prompt-sort": {
      // Hard stop for the hook's path (no subcommand): fail open with no output
      // rather than ever hold up a prompt. Eval-side subcommands run unbounded.
      const hookBudget = Number(process.env.AW_PROMPT_SORT_BUDGET_MS);
      const hookBudgetMs = Number.isInteger(hookBudget) && hookBudget > 0 ? hookBudget : HOOK_KILL_MS;
      if (rest.length === 0) setTimeout(() => process.exit(0), hookBudgetMs).unref();
      return runPromptSortCommand(rest, {
        db, config, configFile: judgeConfigPath(), stateDir: judgeStateDir(), hookBudgetMs,
        jev: { fetch: (...args) => fetch(...args), apiKey: () => readApiKey({ env: process.env, readKeychain }) },
        readStdin,
        projectsDir: path.join(os.homedir(), ".claude", "projects"),
        evalsDir: path.join(judgeStateDir(), "judge", "evals"),
        adjudicator: isOnPath(AGENT_CLI_BINARIES["claude-cli"], process.env)
          ? () => makeClaudeCliProvider({ tmpDirFactory, spawn: makeExecSpawn(AGENT_CLI_BINARIES["claude-cli"]), model: "opus", effort: "high" })
          : null,
      });
    }
    case "brief": {
      const sub = rest[0];
      if (sub === "save") {
        const raw = await readStdin();
        let input: unknown;
        try {
          input = JSON.parse(raw);
        } catch {
          return { exitCode: 1, stdout: "", stderr: "input is not valid JSON" };
        }
        return runBriefSave(db, input);
      }
      if (sub === "get") return runBriefGet(db, rest[1] ?? "");
      if (sub === "map-by-name") return runBriefMapByName(db, rest[1] ?? "");
      if (sub === "map-by-dispatch") return runBriefMapByDispatch(db, rest[1] ?? "", rest[2] ?? "", rest[3] ?? "");
      if (sub === "set-agent-id") return runBriefSetAgentId(db, rest[1] ?? "", rest[2] ?? "");
      return { exitCode: 1, stdout: "", stderr: "usage: judge brief save|get|map-by-name|map-by-dispatch|set-agent-id ..." };
    }
    default: {
      if (cmd === undefined) return { exitCode: 1, stdout: "", stderr: "usage: judge <question> < input.json" };
      const raw = await readStdin();
      let input: unknown;
      try {
        input = JSON.parse(raw);
      } catch {
        return { exitCode: 1, stdout: "", stderr: "input is not valid JSON" };
      }
      // ui-element-repair (and visual-critique, below) get a dedicated thin
      // subcommand instead of the generic runQuestion envelope — each carries
      // a field (chosenIndex, reasons) that rides in a provider's `extra`
      // rather than evaluate()'s typed Decision<O> (review fix #2).
      if (cmd === "ui-element-repair") return runUiElementRepairCli(input, { db, config, providers, chain, sessionId });
      if (cmd === "visual-critique") return runVisualCritiqueCli(input, { db, config, providers, chain, sessionId });
      if (cmd === "ask-check") return runAskCheckCli(input, { db, config, providers, chain, sessionId });
      return runQuestion(cmd, input, { db, config, providers, chain, sessionId });
    }
  }
}

const result = await main();
if (result.stdout) process.stdout.write(result.stdout + "\n");
if (result.stderr) process.stderr.write(result.stderr + "\n");
process.exit(result.exitCode);
