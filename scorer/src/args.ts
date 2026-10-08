import path from "node:path";

import type { Size } from "./audit/items.js";
import { SIZES } from "./audit/items.js";
import type { ProviderName } from "./transcript/source.js";
import { isProviderName, PROVIDERS } from "./transcript/source.js";

export interface CliOptions {
  command: "report" | "probe" | "context-tokens" | "live" | "audit";
  since: Date;
  until: Date;
  projectsDir: string; // Claude Code transcripts
  codexSessionsDir: string;
  cursorProjectsDir: string;
  // null = every provider whose transcript directory exists (resolved in run.ts).
  providers: ProviderName[] | null;
  stateDir: string;
  // Whether --state-dir was actually passed on the command line, vs. left at
  // its home-dir default — needed so the judge-db path lookup (run.ts's
  // resolveJudgeDbPath) can give an explicit --state-dir priority over
  // AW_STATE_DIR, and AW_STATE_DIR priority over the bare default, rather
  // than only ever checking AW_STATE_DIR regardless of --state-dir.
  stateDirExplicit: boolean;
  prLookup: boolean;
  contextTokensPath: string | null;
  // `scorer live`: one session's numbers. The window is the model's context window
  // (the host knows it; 200k is the fallback), so percent-of-window is right on any model.
  liveSession: string | null;
  liveCwd: string | null;
  liveWindow: number;
  json: boolean;
  auditOut: string;
  itemPattern: string;
  itemsFile: string | null;
  maxSize: Size;
  label: number;
  labelRepeat: number;
  labelModel: string;
  // false (--no-turns-file): `audit` writes no human-turns.jsonl (the weekly job keeps no verbatim copies).
  turnsFile: boolean;
  help: boolean;
}

type ParseResult = { ok: true; options: CliOptions } | { ok: false; error: string };

const UNIT_MS: Record<string, number> = { h: 3_600_000, d: 86_400_000 };

export function parseArgs(argv: string[], now: Date, home: string): ParseResult {
  const options: CliOptions = {
    command: "report",
    since: new Date(now.getTime() - UNIT_MS.d),
    until: now,
    projectsDir: path.join(home, ".claude", "projects"),
    codexSessionsDir: path.join(home, ".codex", "sessions"),
    cursorProjectsDir: path.join(home, ".cursor", "projects"),
    providers: null,
    stateDir: path.join(home, ".agentic-workflow"),
    stateDirExplicit: false,
    prLookup: true,
    contextTokensPath: null,
    liveSession: null,
    liveCwd: null,
    liveWindow: 200_000,
    json: false,
    auditOut: path.join(home, ".agentic-workflow", "audit"),
    itemPattern: "[A-Z][A-Z0-9]{1,9}-\\d+",
    itemsFile: null,
    maxSize: "XS",
    label: 0,
    labelRepeat: 50,
    labelModel: "sonnet",
    turnsFile: true,
    help: false,
  };
  const args = [...argv];
  while (args.length > 0) {
    const arg = args.shift() as string;
    if (arg === "probe") { options.command = "probe"; continue; }
    if (arg === "context-tokens") {
      options.command = "context-tokens";
      const value = args.shift();
      if (value === undefined) return { ok: false, error: "context-tokens needs a path" };
      options.contextTokensPath = value;
      continue;
    }
    if (arg === "live") { options.command = "live"; continue; }
    if (arg === "audit") { options.command = "audit"; continue; }
    if (arg === "--help" || arg === "-h") { options.help = true; continue; }
    if (arg === "--json") { options.json = true; continue; }
    if (arg === "--no-pr-lookup") { options.prLookup = false; continue; }
    if (arg === "--no-turns-file") { options.turnsFile = false; continue; }
    if (!VALUE_FLAGS.has(arg)) return { ok: false, error: `unknown argument: ${arg}` };
    const value = args.shift();
    if (value === undefined) return { ok: false, error: `${arg} needs a value` };
    if (arg === "--projects-dir") options.projectsDir = value;
    if (arg === "--codex-dir") options.codexSessionsDir = value;
    if (arg === "--cursor-dir") options.cursorProjectsDir = value;
    if (arg === "--provider") {
      const providers = parseProviders(value);
      if (typeof providers === "string") return { ok: false, error: providers };
      options.providers = providers;
    }
    if (arg === "--state-dir") { options.stateDir = value; options.stateDirExplicit = true; }
    if (arg === "--session") {
      // The id becomes part of file paths (live db, outbox file), so it must be a plain token.
      if (!SESSION_ID.test(value)) return { ok: false, error: `--session must be letters, digits, '.', '_' or '-': ${value}` };
      options.liveSession = value;
    }
    if (arg === "--cwd") options.liveCwd = value;
    if (arg === "--window") {
      const window = Number(value);
      if (!Number.isInteger(window) || window <= 0) return { ok: false, error: `--window must be a positive integer: ${value}` };
      options.liveWindow = window;
    }
    if (arg === "--out") options.auditOut = value;
    if (arg === "--items") options.itemsFile = value;
    if (arg === "--item-pattern") {
      try {
        new RegExp(value);
      } catch {
        return { ok: false, error: `--item-pattern is not a valid regular expression: ${value}` };
      }
      options.itemPattern = value;
    }
    if (arg === "--max-size") {
      if (!(SIZES as readonly string[]).includes(value)) return { ok: false, error: `--max-size must be ${SIZES.join("|")}: ${value}` };
      options.maxSize = value as Size;
    }
    if (arg === "--label" || arg === "--label-repeat") {
      const count = Number(value);
      if (!/^\d+$/.test(value) || !Number.isSafeInteger(count)) return { ok: false, error: `${arg} must be a non-negative integer: ${value}` };
      if (arg === "--label") options.label = count;
      else options.labelRepeat = count;
    }
    if (arg === "--label-model") {
      if (!/^[A-Za-z0-9][A-Za-z0-9._:[\]-]*$/.test(value)) return { ok: false, error: `--label-model must be a model name or alias: ${value}` };
      options.labelModel = value;
    }
    if (arg === "--since") {
      const since = parseSince(value, now);
      if (typeof since === "string") return { ok: false, error: since };
      options.since = since;
    }
  }
  if (options.command === "live" && options.liveSession === null) return { ok: false, error: "live needs --session <id>" };
  return { ok: true, options };
}

const VALUE_FLAGS: ReadonlySet<string> = new Set(["--since", "--projects-dir", "--codex-dir", "--cursor-dir", "--state-dir", "--provider", "--session", "--cwd", "--window", "--out", "--item-pattern", "--items", "--max-size", "--label", "--label-repeat", "--label-model"]);
const SESSION_ID = /^[A-Za-z0-9._-]+$/;

// "all" | "claude" | "codex,cursor" …
function parseProviders(value: string): ProviderName[] | string {
  if (value === "all") return [...PROVIDERS];
  const names = value.split(",").map((n) => n.trim());
  const valid = names.filter(isProviderName);
  if (valid.length !== names.length) return `--provider must be ${PROVIDERS.join("|")}|all (comma-separated ok): ${value}`;
  return [...new Set(valid)];
}

function parseSince(value: string, now: Date): Date | string {
  const rel = /^(\d+)([hd])$/.exec(value);
  const date = rel ? new Date(now.getTime() - Number(rel[1]) * UNIT_MS[rel[2]]) : new Date(value);
  if (Number.isNaN(date.getTime())) return `--since must be like 7d, 12h or an ISO date: ${value}`;
  if (date.getTime() >= now.getTime()) return `--since must be in the past: ${value}`;
  return date;
}
