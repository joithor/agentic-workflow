// scripts/scaffold/lib/common.mjs — shared plumbing for the C# package and slice scaffolds:
// argument parsing, rule-id refusals, a write journal that can revert everything, the heavy-job
// lock, and command running. Node builtins only: no install step, no network.

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";

/** A refusal or a failed check: printed as "<rule>: <message>" plus "fix: <fix>", exit 1. */
export class ScaffoldError extends Error {
  constructor(rule, message, fix) {
    super(`${rule}: ${message}`);
    this.rule = rule;
    this.fix = fix;
  }
}

/**
 * Parses `--key value` and `--flag` arguments. Keys are kebab-case on the command line and camelCase in the
 * result. A key given twice, a positional argument or an unknown key is a usage error (exit 2).
 */
export function parseArgs(argv, known, flags = []) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const raw = argv[i];
    if (!raw.startsWith("--")) throw new UsageError(`unexpected argument '${raw}'`);
    const key = raw.slice(2).replace(/-([a-z])/g, (_, c) => c.toUpperCase());
    if (!known.includes(key) && !flags.includes(key)) throw new UsageError(`unknown option '${raw}'`);
    if (key in out) throw new UsageError(`option '${raw}' given twice`);
    if (flags.includes(key)) {
      out[key] = true;
      continue;
    }
    if (i + 1 >= argv.length || argv[i + 1].startsWith("--")) throw new UsageError(`option '${raw}' needs a value`);
    out[key] = argv[++i];
  }
  return out;
}

export class UsageError extends Error {}

/** Splits a comma-separated list; empty or missing gives []. */
export const list = (value) =>
  (value ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);

/**
 * Records every file the scaffold creates or edits, so a failed check can put the tree back exactly as it was
 * (new-package.md "Failure behavior"). Edits keep the original text; created files and directories are removed.
 */
export class Journal {
  constructor(root) {
    this.root = root;
    this.created = [];
    this.createdDirs = [];
    this.edited = new Map();
  }

  abs(rel) {
    return path.join(this.root, rel);
  }

  exists(rel) {
    return fs.existsSync(this.abs(rel));
  }

  read(rel) {
    return fs.readFileSync(this.abs(rel), "utf8");
  }

  /** Writes a new file; refuses to overwrite one (the generators check for collisions before writing). */
  create(rel, text) {
    const file = this.abs(rel);
    if (fs.existsSync(file)) throw new Error(`refusing to overwrite ${rel}`);
    this.mkdirs(path.dirname(rel));
    fs.writeFileSync(file, text);
    this.created.push(rel);
  }

  /** Rewrites an existing file through fn(text) -> text, keeping the original for revert. */
  edit(rel, fn) {
    const before = this.read(rel);
    const after = fn(before);
    if (after === before) return;
    if (!this.edited.has(rel) && !this.created.includes(rel)) this.edited.set(rel, before);
    fs.writeFileSync(this.abs(rel), after);
  }

  /** Marks a file a command will rewrite (a lock file), so revert restores it. */
  watch(rel) {
    if (this.created.includes(rel) || this.edited.has(rel)) return;
    if (this.exists(rel)) this.edited.set(rel, this.read(rel));
    else this.created.push(rel);
  }

  mkdirs(relDir) {
    const parts = relDir.split(path.sep).filter(Boolean);
    let cur = "";
    for (const part of parts) {
      cur = cur ? path.join(cur, part) : part;
      if (!fs.existsSync(this.abs(cur))) {
        fs.mkdirSync(this.abs(cur));
        this.createdDirs.push(cur);
      }
    }
  }

  /** Every path the scaffold touched, sorted (for the summary and the git-status check). */
  touched() {
    return [...new Set([...this.created, ...this.edited.keys()])].sort();
  }

  revert() {
    for (const [rel, text] of this.edited) fs.writeFileSync(this.abs(rel), text);
    for (const rel of [...this.created].reverse()) fs.rmSync(this.abs(rel), { force: true });
    for (const dir of [...this.createdDirs].reverse()) fs.rmSync(this.abs(dir), { recursive: true, force: true });
  }
}

/**
 * The box-wide heavy-job lock (config/lib/locks.sh's mkdir primitive): builds and test runs take it so only one
 * runs at a time. AW_HEAVY_LOCK_HELD=1 means a caller (the `heavy` wrapper, or new-package calling new-slice)
 * already holds it.
 */
export function withHeavyLock(fn, { timeoutS = 5400, log = console.error } = {}) {
  if (process.env.AW_HEAVY_LOCK_HELD === "1") return fn();
  const state = process.env.AW_STATE_DIR || path.join(os.homedir(), ".agentic-workflow");
  const lock = path.join(state, "locks", "heavy-job.lock");
  fs.mkdirSync(path.dirname(lock), { recursive: true });
  const start = Date.now();
  let told = false;
  for (;;) {
    try {
      fs.mkdirSync(lock);
      break;
    } catch (e) {
      if (e.code !== "EEXIST") throw e;
      if (!told) {
        log(`waiting on lock: ${lock}`);
        told = true;
      }
      if (Date.now() - start > timeoutS * 1000) throw new ScaffoldError("SCAFFOLD-LOCK", `the heavy-job lock ${lock} was held for ${timeoutS} s`, "wait for the other build, or remove a stale lock directory");
      spawnSync("sleep", ["1"]);
    }
  }
  process.env.AW_HEAVY_LOCK_HELD = "1";
  try {
    return fn();
  } finally {
    delete process.env.AW_HEAVY_LOCK_HELD;
    fs.rmSync(lock, { recursive: true, force: true });
  }
}

/** Runs a command in root; returns { code, out } with stdout and stderr together. Never throws on a non-zero exit. */
export function run(root, cmd, args, { log = console.error } = {}) {
  log(`$ ${[cmd, ...args].join(" ")}`);
  const r = spawnSync(cmd, args, { cwd: root, encoding: "utf8", maxBuffer: 256 * 1024 * 1024, env: process.env });
  if (r.error) return { code: 127, out: String(r.error.message) };
  return { code: r.status ?? 1, out: `${r.stdout ?? ""}${r.stderr ?? ""}` };
}

export const lower = (name) => name.toLowerCase();
export const camel = (name) => name[0].toLowerCase() + name.slice(1);
/** CheckRepo -> check-repo. */
export const kebab = (name) => name.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
