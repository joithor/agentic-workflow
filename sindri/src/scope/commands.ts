import fs from "node:fs";
import path from "node:path";

import { unwrap } from "../adapters/types.js";
import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import { approvedOrThrow } from "../index/commands.js";
import type { ProcessRunner } from "../index/io.js";
import { ledgerPath, openLedger, openLedgerReadOnly, withEpoch } from "../ledger/db.js";
import { acquireTickLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import type { LoadedProfile } from "../profile/load.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "../scrub/scrub.js";
import { resolveSecret } from "../secrets.js";
import { measureMap, measureNotes, measureProblems, parseWindow, passBar, renderBacktest, splitProject, summarize, type BacktestReport, type Measured } from "./backtest.js";
import { gather, type Evidence } from "./gather.js";
import { renderIncomplete, renderMap, type RenderMeta } from "./map.js";
import { Budget, meteredRunner, type ModelAuditRow, type ModelRunner } from "./model.js";
import { trySaveReplay } from "../evolve/corpus.js";
import { loadPrompt } from "../evolve/overlay.js";
import { runScoping, type ScopeOptions, type ScopeResult } from "./run.js";
import type { Source, SourceRecord } from "./source.js";
import { codeSource } from "./sources/code.js";
import { fileSource } from "./sources/file.js";
import { fetchLinearProject, linearSource, projectSlug, type GraphqlFetch, type LinearProject } from "./sources/linear.js";
import { notesSource } from "./sources/notes.js";
import { TRANSCRIPT_CAPS, transcriptsSource } from "./sources/transcripts.js";

// The scrubber is the profile's (built-in plus scrub.extraPatterns): the real runner
// scrubs every prompt with it before it leaves the machine.
export interface ScopeIo {
  runner: (loaded: LoadedProfile, scrubber: Scrubber) => ModelRunner;
  fetch: GraphqlFetch;
  process: ProcessRunner;
  progress: (line: string) => void;
}

export const SOURCE_NAMES = ["file", "notes", "transcripts", "linear", "code"] as const;
export type SourceName = (typeof SOURCE_NAMES)[number];

const USAGE =
  "usage: sindri scope <brief.md | linear:<project-url>> [--section N] [--out DIR] [--sources LIST] [--dry-run] [--json]  |  sindri scope --backtest linear:<project-url> [--window 1d] [--with-index] [--out DIR] [--sources LIST] [--json]  |  sindri scope runs [--json]";

export function extractSection(markdown: string, n: string): string | null {
  const lines = markdown.split("\n");
  const start = lines.findIndex((l) => l.startsWith(`## ${n}. `) || l.startsWith(`## ${n} `));
  if (start < 0) return null;
  const end = lines.findIndex((l, i) => i > start && l.startsWith("## "));
  return lines.slice(start, end < 0 ? lines.length : end).join("\n").trimEnd();
}

export const slug = (s: string): string => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "").slice(0, 60) || "scope";

export const isLinearSubject = (s: string): boolean => s.startsWith("linear:") || s.includes("linear.app/");

// What the ledger and the --json output call the subject: never a path or a URL, and scrubbed.
export const subjectLabel = (s: string, scrubber: Scrubber): string =>
  scrubber.scrub(isLinearSubject(s) ? `linear:${projectSlug(s).replace(/[^A-Za-z0-9_-]+/g, "-")}` : path.basename(s)).text;

export function formatCounts(counts: Record<string, number>): string {
  const parts = Object.entries(counts).map(([k, v]) => `${k} ${v}`);
  return parts.length === 0 ? "none" : parts.join(", ");
}

// null means "every source the profile configures".
export function parseSources(list: string | undefined): Set<SourceName> | null {
  if (list === undefined) return null;
  const names = list.split(",").map((s) => s.trim()).filter((s) => s !== "");
  const bad = names.filter((n) => !(SOURCE_NAMES as readonly string[]).includes(n));
  if (bad.length > 0) throw new SindriError("SND-CLI-002", `--sources has unknown source ${bad.join(", ")}; choose from ${SOURCE_NAMES.join(", ")}`);
  return new Set(names as SourceName[]);
}

// The profile's scrubber, built once per run and passed to every source, gather, the model and the output.
export const profileScrubber = (loaded: LoadedProfile): Scrubber => makeScrubber(compileExtraPatterns(loaded.profile.scrub.extraPatterns));

// A source the profile doesn't configure is never read, whatever --sources says.
// The code source reads only `codeRepos` (see guardOutput).
export function localSources(
  deps: Deps,
  loaded: LoadedProfile,
  o: { withIndex: boolean; only: Set<SourceName> | null; codeRepos: string[]; scrubber: Scrubber },
): Source[] {
  const src = loaded.profile.sources;
  const want = (n: SourceName): boolean => o.only === null || o.only.has(n);
  const out: Source[] = [];
  if (src.notesDir !== undefined && want("notes")) out.push(notesSource(src.notesDir, o.scrubber));
  if (src.transcripts.enabled && want("transcripts")) out.push(transcriptsSource(src.transcripts.dir.replace(/^~(?=\/|$)/, deps.home), TRANSCRIPT_CAPS, o.scrubber));
  if (want("code")) out.push(codeSource(deps, o.codeRepos, { allowAsOf: o.withIndex, scrubber: o.scrubber }));
  return out;
}

export async function loadLinear(deps: Deps, loaded: LoadedProfile, io: ScopeIo, ref: string, scrubber: Scrubber): Promise<LinearProject> {
  const cfg = loaded.profile.sources.linear;
  if (cfg === undefined) throw new SindriError("SND-SCOPE-024", "sources.linear is not configured", { fix: "add sources.linear.token (a secret pointer) to the profile, then sindri profile approve" });
  const token = await resolveSecret(cfg.token, deps, io.process);
  return unwrap(await fetchLinearProject({ apiUrl: cfg.apiUrl, token, fetch: io.fetch, ref, scrubber }));
}

// --out is resolved against the command's cwd, not the process's.
export function outputDir(loaded: LoadedProfile, deps: Deps, flag: string | undefined): string {
  const dir = flag === undefined ? loaded.profile.sources.notesDir : path.resolve(deps.cwd, flag);
  if (dir === undefined) throw new SindriError("SND-SCOPE-021", "no output directory", { fix: "pass --out DIR or set sources.notesDir" });
  return dir;
}

function nearestExisting(p: string): string {
  let cur = p;
  while (!fs.existsSync(cur)) cur = path.dirname(cur);
  return cur;
}

// The path with symlinks resolved as far as it exists; the missing tail is kept as written.
function realPath(p: string): string {
  const base = nearestExisting(p);
  return path.join(fs.realpathSync(base), path.relative(base, p));
}

// `verified` false: git errored and no .git entry was found, so the guard could only fail closed.
const refuse = (dir: string, verified: boolean, why: string): SindriError =>
  new SindriError(
    "SND-SCOPE-025",
    verified ? `refusing to write ${path.basename(dir)} inside a git worktree: ${why}` : `refusing to write ${path.basename(dir)}: could not confirm --out is outside a git worktree (git failed), and ${why}`,
  );

// The nearest directory at or above `p` that holds a `.git` entry (a directory, or a
// file for a linked worktree or submodule), or null.
function dotGitHolder(p: string): string | null {
  let cur = p;
  while (!fs.existsSync(path.join(cur, ".git"))) {
    if (path.dirname(cur) === cur) return null;
    cur = path.dirname(cur);
  }
  return cur;
}

// Spec amendment 8: a map built from notes, transcripts or tracker text must not land
// in a git worktree (it may be public). Only a file brief and the code index are safe there,
// and then only the code of the profile repo whose root is that worktree's top level (another
// repo's code must not land in this one). It fails closed: a git error other than "not a git
// repository", or a `.git` entry above --out, counts as inside a worktree. Answers the repos
// the code source may read.
export async function guardOutput(deps: Deps, loaded: LoadedProfile, dir: string, only: Set<SourceName> | null, linear: boolean): Promise<string[]> {
  const all = Object.keys(loaded.repos).sort();
  const at = realPath(dir);
  const holder = dotGitHolder(at);
  const r = await deps.git.run(["rev-parse", "--show-toplevel"], nearestExisting(at), { foreign: true });
  const outside = !r.ok && holder === null && /not a git repository/i.test(r.stderr);
  if (outside) return all;
  const verified = r.ok || holder !== null;
  if (only === null || linear || ![...only].every((n) => n === "file" || n === "code")) {
    throw refuse(dir, verified, "the map may carry notes, transcripts or tracker text");
  }
  if (!only.has("code")) return [];
  const top = r.ok ? realPath(r.stdout.trim()) : holder;
  const repo = all.find((name) => realPath(loaded.repos[name].path) === top);
  if (repo === undefined) throw refuse(dir, verified, "it is not the top level of a profile repo, so the code source would carry another repo's code");
  return [repo];
}

// Files 0600 in a 0700 directory (spec §8.4); scrubbed once more with the profile's scrubber on the way out.
// Only `dir` is ever written: the file name is a slug, never model text.
export function writeOut(dir: string, base: string, md: string, json: unknown, scrubber: Scrubber): string {
  fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
  // Neither name may exist: a taken .json must not leave an orphan .md beside it.
  const free = (b: string): boolean => !fs.existsSync(path.join(dir, `${b}.md`)) && !fs.existsSync(path.join(dir, `${b}.json`));
  let name = base;
  for (let n = 2; !free(name); n++) name = `${base}-${n}`;
  const file = path.join(dir, `${name}.md`);
  fs.writeFileSync(file, scrubber.scrub(md).text, { flag: "wx", mode: 0o600 });
  fs.writeFileSync(file.replace(/\.md$/, ".json"), scrubber.scrub(`${JSON.stringify(scrubber.scrubDeep(json), null, 2)}\n`).text, { flag: "wx", mode: 0o600 });
  return file;
}

export interface RunRow {
  runId: string;
  subject: string;
  mode: "scope" | "backtest";
  status: string;
  rounds: number;
  surfaces: number;
  recall: number | null;
  precision: number | null;
  baselineRecall: number | null;
  baselinePrecision: number | null;
  leaky: boolean;
  tokens: number;
  outPath: string;
}

// The run's file is on disk but recordRun threw (a newer ledger, an I/O error): say where the file is
// before the error, so the run isn't lost. Its scope_runs row and model_calls rows are not kept, the
// same for a scope run and a backtest.
function unrecorded(e: unknown, file: string, scrubber: Scrubber, json: boolean): CommandResult {
  const wrote = `wrote ${file} and ${file.replace(/\.md$/, ".json")}; the run was not recorded`;
  const err = e instanceof SindriError ? e : new SindriError("SND-CLI-900", `could not record the run: ${scrubber.scrub((e as Error).message).text}`);
  const f = failure(err.code, err.message, json, { fix: err.fix, details: [...err.details, wrote], exitCode: err.exitCode });
  return json ? f : { ...f, stdout: `Wrote ${file}.\n` };
}

export const LEDGER_MISS = "Not recorded in the ledger: another run holds the lock.";

// A tick or index job holds the lock for seconds at most, and the ledger write takes milliseconds:
// wait for it (deps.sleep, so tests don't) before giving up on the run's rows.
export const RECORD_LOCK_WAIT = { totalMs: 10_000, stepMs: 250 };

// What a lock miss loses, on stderr: the run id and the tokens spent that the ledger never saw.
// The written files stay where they are.
export function missLine(runId: string, calls: ModelAuditRow[], file: string): string {
  const tokens = calls.reduce((n, c) => n + c.inputTokens + c.outputTokens, 0);
  return `Not recorded: run ${runId} spent ${tokens} tokens over ${calls.length} model calls; the lock stayed held for ${RECORD_LOCK_WAIT.totalMs / 1000} s. The files are kept: ${file} and ${file.replace(/\.md$/, ".json")}.\n`;
}

// One scope_runs row and one model_calls row per call (step = the run's mode), under the tick lock. False when the lock
// stays held for RECORD_LOCK_WAIT.totalMs.
export async function recordRun(deps: Deps, row: RunRow, calls: ModelAuditRow[]): Promise<boolean> {
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    const tries = Math.floor(RECORD_LOCK_WAIT.totalMs / RECORD_LOCK_WAIT.stepMs) + 1;
    let lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
    for (let i = 1; !lock.ok && i < tries; i++) {
      await deps.sleep(RECORD_LOCK_WAIT.stepMs);
      lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
    }
    if (!lock.ok) return false;
    const owner = lock.owner;
    try {
      withEpoch(db, owner.epoch, () => {
        db.prepare(
          "INSERT INTO scope_runs (run_id, subject, mode, ts, status, rounds, surfaces, recall, precision, baseline_recall, baseline_precision, leaky, tokens, out_path, epoch) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        ).run(row.runId, row.subject, row.mode, deps.now().toISOString(), row.status, row.rounds, row.surfaces, row.recall, row.precision, row.baselineRecall, row.baselinePrecision, row.leaky ? 1 : 0, row.tokens, row.outPath, owner.epoch);
        const insert = db.prepare("INSERT INTO model_calls (run_id, step, seq, role, model, input_tokens, output_tokens) VALUES (?, ?, ?, ?, ?, ?, ?)");
        calls.forEach((c, i) => insert.run(row.runId, row.mode, i + 1, c.role, c.model, c.inputTokens, c.outputTokens));
      });
      return true;
    } finally {
      lock.release();
    }
  } finally {
    db.close();
  }
}

export async function scopeOnce(
  loaded: LoadedProfile,
  io: ScopeIo,
  brief: SourceRecord,
  sources: Source[],
  asOf: Date | null,
  runner: ModelRunner,
  budget: Budget,
  scrubber: Scrubber,
  prompts?: ScopeOptions["prompts"],
): Promise<{ result: ScopeResult; evidence: Evidence }> {
  const evidence = await gather(brief, sources, { asOf, maxRecords: loaded.profile.scope.maxRecords, progress: io.progress, scrubber });
  const result = await runScoping(evidence, {
    runner,
    models: loaded.profile.models,
    maxRounds: loaded.profile.scope.maxRounds,
    budget,
    maxPackChars: loaded.profile.scope.maxPackChars,
    progress: io.progress,
    prompts,
  });
  return { result, evidence };
}

interface RunsRow {
  run_id: string;
  ts: string;
  mode: string;
  status: string;
  surfaces: number;
  recall: number | null;
  precision: number | null;
  baseline_recall: number | null;
  baseline_precision: number | null;
  leaky: number;
  out_path: string;
}

// Read-only: never creates or migrates the ledger. No ledger, or one at another schema version, has no runs to show.
function scopeRuns(args: string[], deps: Deps): CommandResult {
  const json = args.includes("--json");
  parseFlags(args, { json: { type: "boolean" } });
  const db = openLedgerReadOnly(ledgerPath(stateDir(deps)));
  let found: RunsRow[] = [];
  if (db !== null) {
    try {
      found = db.prepare(
        "SELECT run_id, ts, mode, status, surfaces, recall, precision, baseline_recall, baseline_precision, leaky, out_path FROM scope_runs ORDER BY ts DESC, rowid DESC LIMIT 20",
      ).all() as RunsRow[];
    } finally {
      db.close();
    }
  }
  const data = found.map((r) => ({
    runId: r.run_id, ts: r.ts, mode: r.mode, status: r.status, surfaces: r.surfaces, recall: r.recall, precision: r.precision,
    baselineRecall: r.baseline_recall, baselinePrecision: r.baseline_precision, leaky: r.leaky === 1, file: path.basename(r.out_path),
  }));
  if (data.length === 0) return success("No scope runs recorded.", data, json);
  const num = (v: number | null): string => (v === null ? "n/a" : v.toFixed(2));
  const text = data.map((r) => `${r.ts} ${r.mode} ${r.status} surfaces=${r.surfaces} recall=${num(r.recall)} precision=${num(r.precision)}${r.leaky ? " leaky" : ""} ${r.file}`);
  return success(text.join("\n"), data, json);
}

// Spec §7.5: scope the brief as it stood at the cut, then the brief alone, and let a model
// (never a person) judge both maps against the issues filed later. One budget covers it all.
async function runBacktest(
  deps: Deps,
  loaded: LoadedProfile,
  io: ScopeIo,
  subject: string,
  o: { out: string; only: Set<SourceName> | null; codeRepos: string[]; scrubber: Scrubber; window: string | undefined; withIndex: boolean; json: boolean },
): Promise<CommandResult> {
  if (!isLinearSubject(subject)) throw new SindriError("SND-CLI-002", "--backtest takes a Linear project (linear:<project-url>), not a file");
  const scrubber = o.scrubber;
  const windowMs = parseWindow(o.window);
  const project = await loadLinear(deps, loaded, io, subject, scrubber);
  const { cut, brief, early, later } = splitProject(project, windowMs);
  if (later.length === 0) throw new SindriError("SND-SCOPE-023", `no issues were filed after ${cut.toISOString()}; nothing to backtest`, { fix: "pick a project with later issues, or a shorter --window" });

  // One budget and one audit trail for the whole backtest: scoping, baseline and every adjudication.
  const runId = ulid(deps.now());
  const budget = new Budget(loaded.profile.scope.maxTokensPerBacktest);
  const audit: ModelAuditRow[] = [];
  const raw = io.runner(loaded, scrubber);
  const judgeWith = (tag: string) => ({ runner: meteredRunner(raw, { budget, audit, tag }), model: loaded.profile.models.adjudicator, progress: io.progress, scrubber });
  const problems: string[] = [];
  const notes: string[] = [];
  // The model's text never passed a scrubber: scrub a map before it is judged, rendered or saved.
  const scrubbed = (m: ScopeResult["map"]) => (m === null ? null : scrubber.scrubDeep(m));
  const why = (r: string[]): string => r.map((x) => scrubber.scrub(x).text).join("; ");

  // Spec amendment 4: as-of sources only; the code index is today's code, so it's opt-in and labeled leaky.
  const sources: Source[] = [
    ...(o.only === null || o.only.has("linear") ? [linearSource({ ...project, issues: early })] : []),
    ...localSources(deps, loaded, { withIndex: o.withIndex, only: o.only, codeRepos: o.codeRepos, scrubber }),
  ];
  io.progress("scoping the brief with every source…");
  const fullJudge = judgeWith("");
  const first = await scopeOnce(loaded, io, brief, sources, cut, fullJudge.runner, budget, scrubber);
  const firstMap = scrubbed(first.result.map);
  if (first.result.status === "incomplete") problems.push(`scoping incomplete: ${why(first.result.reasons)}`);

  let full: Measured | null = null;
  let baseline: Measured | null = null;
  if (firstMap === null) {
    problems.push("recall and precision not measured: no scope map passed the checks");
  } else {
    full = await measureMap(firstMap, { early, later }, fullJudge);
    problems.push(...measureProblems("", full));
    notes.push(...measureNotes("", full));
    // The same scoping with the brief alone: what the sources add is the difference.
    io.progress("scoping the brief alone (baseline)…");
    const baseJudge = judgeWith("baseline:");
    const base = await scopeOnce(loaded, io, brief, [], cut, baseJudge.runner, budget, scrubber);
    const baseMap = scrubbed(base.result.map);
    if (baseMap === null) {
      problems.push(`baseline: no scope map passed the checks (${why(base.result.reasons)})`);
    } else {
      baseline = await measureMap(baseMap, { early, later }, baseJudge);
      problems.push(...measureProblems("baseline: ", baseline));
      notes.push(...measureNotes("baseline: ", baseline));
    }
  }

  const status = problems.length === 0 ? "complete" : "incomplete";
  const label = subjectLabel(subject, scrubber);
  const report: BacktestReport = {
    name: project.name, cut: cut.toISOString(), generatedAt: deps.now().toISOString(), leaky: o.withIndex, status, reasons: [...problems, ...notes],
    scoping: { status: first.result.status, rounds: first.result.rounds }, tokens: budget.used, later, full, baseline,
  };
  const file = writeOut(o.out, `backtest-${slug(project.name)}-${deps.now().toISOString().slice(0, 10)}`, renderBacktest(report), {
    ...report, later: later.map((i) => i.identifier), subject: label, map: firstMap,
  }, scrubber);
  const recall = full?.recall ?? null;
  const precision = full?.precision ?? null;
  const baselineRecall = baseline?.recall ?? null;
  const baselinePrecision = baseline?.precision ?? null;
  let recorded: boolean;
  try {
    recorded = await recordRun(deps, {
      runId, subject: label, mode: "backtest", status, rounds: first.result.rounds, surfaces: firstMap === null ? 0 : firstMap.surfaces.length,
      recall, precision, baselineRecall, baselinePrecision, leaky: o.withIndex, tokens: budget.used, outPath: file,
    }, audit);
  } catch (e) {
    return unrecorded(e, file, scrubber, o.json);
  }
  const p = summarize(report);
  const text = [
    `Backtest of "${project.name}": ${p.recall}, ${p.precision}; ${p.baseline}. Pass bar: ${p.overall}.${o.withIndex ? " Leaky: used today's code index." : ""}`,
    `Wrote ${file}.`,
    ...(recorded ? [] : [LEDGER_MISS]),
  ];
  const res = success(text.join("\n"), { status, recall, precision, baselineRecall, baselinePrecision, bar: passBar(full, baseline), leaky: o.withIndex, file, runId, recorded, reasons: report.reasons }, o.json, status === "complete" ? 0 : 1);
  const miss = recorded ? "" : missLine(runId, audit, file);
  return { ...res, stderr: (o.json ? "" : problems.map((x) => `Why incomplete: ${x}\n`).join("")) + miss };
}

export function makeScopeCommand(io: ScopeIo): Command {
  return async (args, deps) => {
    const json = args.includes("--json");
    try {
      if (args[0] === "runs") return scopeRuns(args.slice(1), deps);
      const { values, positionals } = parseFlags(args, {
        section: { type: "string" }, out: { type: "string" }, json: { type: "boolean" }, sources: { type: "string" }, "dry-run": { type: "boolean" },
        backtest: { type: "boolean" }, window: { type: "string" }, "with-index": { type: "boolean" },
      });
      const subject = positionals[0];
      if (subject === undefined) return failure("SND-CLI-002", USAGE, json);
      const only = parseSources(values.sources);
      const dryRun = values["dry-run"] === true;
      // Read-only and at any schema version (readLedger, as `repo status` does): a dry run on an older
      // ledger finds the approval the real run finds. Only recordRun, at the end of a real run, migrates.
      const loaded = approvedOrThrow(deps);
      const scrubber = profileScrubber(loaded);
      const linear = isLinearSubject(subject);
      if (linear && values.section !== undefined) throw new SindriError("SND-CLI-002", "--section applies to brief files, not Linear projects");
      const backtest = values.backtest === true;
      if (!backtest && (values.window !== undefined || values["with-index"] === true)) throw new SindriError("SND-CLI-002", "--window and --with-index apply only to --backtest");
      if (backtest && values.section !== undefined) throw new SindriError("SND-CLI-002", "--section does not apply to --backtest");
      if (backtest && dryRun) throw new SindriError("SND-CLI-002", "--dry-run does not apply to --backtest");
      const out = dryRun ? null : outputDir(loaded, deps, values.out);
      const codeRepos = out === null ? Object.keys(loaded.repos).sort() : await guardOutput(deps, loaded, out, only, linear);
      if (backtest && out !== null) {
        return await runBacktest(deps, loaded, io, subject, { out, only, codeRepos, scrubber, window: values.window, withIndex: values["with-index"] === true, json });
      }

      let brief: SourceRecord;
      const sources = localSources(deps, loaded, { withIndex: true, only, codeRepos, scrubber });
      if (linear) {
        const project = await loadLinear(deps, loaded, io, subject, scrubber);
        brief = { ref: `linear-project:${projectSlug(subject)}`, kind: "brief", title: project.name, text: `${project.name}\n\n${project.description}`, author: null, createdAt: project.createdAt, trust: "untrusted" };
        if (only === null || only.has("linear")) sources.unshift(linearSource(project));
      } else {
        brief = unwrap(await fileSource(path.resolve(deps.cwd, subject), scrubber).find({ keywords: [], asOf: null, limit: 1 }))[0];
        if (values.section !== undefined) {
          const part = extractSection(brief.text, values.section);
          if (part === null) throw new SindriError("SND-SCOPE-022", `no section ${values.section} in ${path.basename(subject)}`, { fix: "check the heading number (## 13. …)" });
          brief = { ...brief, text: part, title: part.split("\n")[0].replace(/^##\s*/, "") };
        }
      }

      const s = loaded.profile.scope;
      if (out === null) {
        const evidence = await gather(brief, sources, { asOf: null, maxRecords: s.maxRecords, progress: io.progress, scrubber });
        const m = loaded.profile.models;
        const packChars = evidence.refs.pack(s.maxPackChars).length;
        const text = [
          `Dry run for "${brief.title}". No model was called and nothing was written.`,
          `Sources it would read: ${formatCounts(evidence.counts)}.`,
          `Pack: about ${packChars} characters (at most ${s.maxPackChars}). Models: draft ${m.scoping}, challenge ${m.challenger}; up to ${s.maxRounds} rounds each; budget ${s.maxTokensPerRun} tokens.`,
        ];
        return success(text.join("\n"), { dryRun: true, title: brief.title, counts: evidence.counts, notes: evidence.notes, packChars, models: { scoping: m.scoping, challenger: m.challenger }, maxRounds: s.maxRounds, maxTokensPerRun: s.maxTokensPerRun }, json);
      }

      const runId = ulid(deps.now());
      const budget = new Budget(s.maxTokensPerRun);
      const audit: ModelAuditRow[] = [];
      const prompts = { draft: loadPrompt(deps, "scope.draft"), challenger: loadPrompt(deps, "scope.challenger") };
      const runner = meteredRunner(io.runner(loaded, scrubber), { budget, audit });
      const { result, evidence } = await scopeOnce(loaded, io, brief, sources, null, runner, budget, scrubber, prompts);
      // The model's text never passed a scrubber: scrub it before it is rendered or saved.
      const map = result.map === null ? null : scrubber.scrubDeep(result.map);
      const reasons = result.reasons.map((r) => scrubber.scrub(r).text);
      const label = subjectLabel(subject, scrubber);
      const meta: RenderMeta = { status: result.status, rounds: result.rounds, tokens: result.tokens, generatedAt: deps.now().toISOString(), reasons, notes: evidence.notes, added: result.added };
      const md = map === null ? renderIncomplete(brief.title, meta) : renderMap(map, evidence.refs, meta);
      const file = writeOut(out, `scope-${slug(brief.title)}-${deps.now().toISOString().slice(0, 10)}`, md, { ...meta, subject: label, map, counts: evidence.counts }, scrubber);
      const n = map === null ? { surfaces: 0, workstreams: 0, questions: 0 } : { surfaces: map.surfaces.length, workstreams: map.workstreams.length, questions: map.questions.length };
      let recorded: boolean;
      try {
        recorded = await recordRun(deps, { runId, subject: label, mode: "scope", status: result.status, rounds: result.rounds, surfaces: n.surfaces, recall: null, precision: null, baselineRecall: null, baselinePrecision: null, leaky: false, tokens: result.tokens, outPath: file }, audit);
      } catch (e) {
        return unrecorded(e, file, scrubber, json);
      }
      // The replay item shares the run's id with the scope_runs row recordRun just wrote. evidence.brief is the
      // scrubbed brief (never the raw one). trySaveReplay never throws, so a full disk can't fail the run.
      trySaveReplay(deps, {
        id: runId, artifact: "scope.draft", createdAt: deps.now().toISOString(), brief: evidence.brief,
        records: evidence.refs.entries().slice(1).map(([, r]) => r),
        outcome: { status: result.status, surfaces: n.surfaces, recall: null },
      });
      const next =
        result.status === "incomplete"
          ? "Rerun after raising scope.maxRounds or scope.maxTokensPerRun in the profile (then sindri profile approve), or fix the reasons above."
          : n.questions > 0
            ? `${n.questions} open questions need answers before issues are created (see "Open questions" in ${path.basename(file)}).`
            : "No open questions.";
      const lines = [
        `Scope map for "${brief.title}": ${result.status}, ${n.surfaces} surfaces, ${n.workstreams} workstreams, ${n.questions} open questions (${result.rounds} rounds, ${result.tokens} tokens).`,
        `Sources: ${formatCounts(evidence.counts)}.`,
        `Wrote ${file}.`,
        next,
        ...(recorded ? [] : [LEDGER_MISS]),
      ];
      const why = [...evidence.notes.map((x) => `Note: ${x}`), ...(result.status === "incomplete" ? reasons.map((x) => `Why incomplete: ${x}`) : [])];
      const res = success(lines.join("\n"), { file, runId, ...meta, ...n, counts: evidence.counts, recorded }, json, result.status === "complete" ? 0 : 1);
      const miss = recorded ? "" : missLine(runId, audit, file);
      return { ...res, stderr: (json ? "" : why.map((x) => `${x}\n`).join("")) + miss };
    } catch (e) {
      return fromError(e, json);
    }
  };
}
