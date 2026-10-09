import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import type { ProcessRunner } from "../index/io.js";

const Sha = z.string().regex(/^[0-9a-f]{40}$/);
const Entry = z.object({ sha: Sha, dir: z.string().min(1), installedAt: z.string() });
const Channels = z.object({ stable: Entry.extend({ previous: Entry.nullable() }).nullable(), next: Entry.nullable() });
export type ChannelEntry = z.infer<typeof Entry>;
export type ChannelState = z.infer<typeof Channels>;

export const channelsRoot = (deps: Deps): string => path.join(stateDir(deps), "channels");
const stateFile = (deps: Deps): string => path.join(stateDir(deps), "channels.json");
const SOAK_DAYS = 3;

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

export function readChannels(deps: Deps): ChannelState {
  let text: string;
  try {
    text = fs.readFileSync(stateFile(deps), "utf8");
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code === "ENOENT") return { stable: null, next: null };
    throw e;
  }
  const r = Channels.safeParse(parseJson(text));
  if (!r.success) {
    throw new SindriError("SND-EVOLVE-005", `channels.json is corrupt: ${r.error.issues[0].message}`, { fix: "restore it from a backup, or delete it and reinstall with scripts/install-sindri.sh --channel next --ref <sha>" });
  }
  return r.data;
}

export function writeChannels(deps: Deps, c: ChannelState): void {
  fs.mkdirSync(stateDir(deps), { recursive: true, mode: 0o700 });
  const tmp = `${stateFile(deps)}.tmp-${ulid(deps.now())}`;
  fs.writeFileSync(tmp, JSON.stringify(Channels.parse(c), null, 2), { flag: "wx", mode: 0o600 });
  fs.renameSync(tmp, stateFile(deps));
}

// A build must sit under the channels root (after realpath) and be runnable.
export function buildProblem(deps: Deps, e: ChannelEntry): string | null {
  let real: string;
  let root: string;
  try {
    real = fs.realpathSync(e.dir);
    root = fs.realpathSync(channelsRoot(deps));
  } catch {
    return `the build at ${e.dir} doesn't exist`;
  }
  if (!real.startsWith(`${root}${path.sep}`)) return `${e.dir} is outside the channels directory`;
  return fs.existsSync(path.join(real, "dist", "cli.js")) ? null : `${e.dir} has no dist/cli.js`;
}

export function canPromote(c: ChannelState, sha: string, suiteOk: boolean, now: Date): { ok: boolean; why: string; next: string } {
  if (c.next === null) return { ok: false, why: "nothing is installed on next", next: "scripts/install-sindri.sh --channel next --ref <sha>" };
  if (c.next.sha !== sha) return { ok: false, why: `${sha} is not what next runs (${c.next.sha})`, next: `sindri channel promote ${c.next.sha}` };
  const days = Math.floor((now.getTime() - Date.parse(c.next.installedAt)) / 86_400_000);
  if (days < SOAK_DAYS) return { ok: false, why: `next has soaked ${days} of ${SOAK_DAYS} days`, next: "sindri channel status (after the soak)" };
  if (!suiteOk) {
    const cmd = `sindri evolve check package:sindri --at ${sha}`;
    return { ok: false, why: `no passing package:sindri suite run for ${sha}; run: ${cmd}`, next: cmd };
  }
  return { ok: true, why: `soaked ${days} days on next; suite passed at that sha`, next: `sindri channel promote ${sha}` };
}

const shq = (s: string): string => `'${s.replace(/'/g, `'\\''`)}'`;
export const binDir = (deps: Deps): string => deps.env.CLAUDE_LOCAL_BIN ?? path.join(deps.home, ".local", "bin");

export function wrapperText(target: string, node: string, cli: string): string {
  return `#!/usr/bin/env bash\n# Written by sindri channel; change it with sindri channel promote or rollback.\nexport SINDRI_BIN=${shq(target)}\nexec ${shq(node)} ${shq(cli)} "$@"\n`;
}

// A temp file in the same directory, then a rename: a symlink at bin/sindri is replaced, never written through.
export function writeWrapper(deps: Deps, cli: string): void {
  fs.mkdirSync(binDir(deps), { recursive: true });
  const target = path.join(binDir(deps), "sindri");
  const tmp = path.join(binDir(deps), `.sindri.tmp-${ulid(deps.now())}`);
  fs.writeFileSync(tmp, wrapperText(target, process.execPath, cli), { flag: "wx", mode: 0o755 });
  fs.chmodSync(tmp, 0o755);
  fs.renameSync(tmp, target);
}

// The cli.js the current wrapper runs: ours (single-quoted) or the installer's in-place one (double-quoted).
export function wrapperTarget(deps: Deps): string | null {
  const file = path.join(binDir(deps), "sindri");
  if (!fs.existsSync(file)) return null;
  const text = fs.readFileSync(file, "utf8");
  const single = /^exec '(?:[^']|'\\'')*' '((?:[^']|'\\'')*)'/m.exec(text);
  if (single !== null) return single[1].replace(/'\\''/g, "'");
  const double = /^exec "[^"]*" "([^"]*)"/m.exec(text);
  return double === null ? null : double[1];
}

const cliOf = (e: ChannelEntry): string => path.join(e.dir, "dist", "cli.js");
const plain = (e: ChannelEntry): ChannelEntry => ({ sha: e.sha, dir: e.dir, installedAt: e.installedAt });

async function ensureRunnable(deps: Deps, run: ProcessRunner, e: ChannelEntry): Promise<void> {
  const problem = buildProblem(deps, e);
  if (problem !== null) throw new SindriError("SND-EVOLVE-005", problem);
  const r = await run.run([process.execPath, cliOf(e), "--version"], { cwd: e.dir, timeoutMs: 30_000 });
  if (r.code !== 0) throw new SindriError("SND-EVOLVE-005", `the build at ${e.dir} didn't start (--version exited ${r.code})`);
}

export async function promote(deps: Deps, run: ProcessRunner, sha: string, now: Date): Promise<ChannelState> {
  const c = readChannels(deps);
  if (c.next === null || c.next.sha !== sha) throw new SindriError("SND-EVOLVE-005", `${sha} is not what next runs`);
  await ensureRunnable(deps, run, c.next);
  writeWrapper(deps, cliOf(c.next));
  const state: ChannelState = { ...c, stable: { ...c.next, installedAt: now.toISOString(), previous: c.stable === null ? null : plain(c.stable) } };
  writeChannels(deps, state);
  return state;
}

// The build we roll back from is not kept as `previous`: a second rollback refuses, and going forward again needs promote.
export async function rollback(deps: Deps, run: ProcessRunner, now: Date): Promise<ChannelState> {
  const c = readChannels(deps);
  const stable = c.stable;
  if (stable === null || stable.previous === null) throw new SindriError("SND-EVOLVE-005", "there is no previous stable build to roll back to");
  const prev = stable.previous;
  await ensureRunnable(deps, run, prev);
  writeWrapper(deps, cliOf(prev));
  const state: ChannelState = { ...c, stable: { ...prev, installedAt: now.toISOString(), previous: null } };
  writeChannels(deps, state);
  return state;
}
