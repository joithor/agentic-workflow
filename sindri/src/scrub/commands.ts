import fs from "node:fs";
import path from "node:path";

import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { ledgerPath, readLedger } from "../ledger/db.js";
import { approvedProfile } from "../profile/approve.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "../profile/load.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "./scrub.js";

export const PRE_COMMIT_MARKER = "# sindri-pre-commit v2";
const LEGACY_MARKERS: readonly string[] = ["# sindri-scrub-pre-commit v1"];

// A whole-line match, never a substring: a foreign hook that merely mentions a marker isn't ours.
export function isSindriHook(text: string): boolean {
  return text.split("\n").some((l) => l === PRE_COMMIT_MARKER || LEGACY_MARKERS.includes(l));
}

export const TEMPLATE_MARKER = "# sindri-template: a no-op until this repo is in the approved profile";

// The hook calls the CLI by the absolute path it was installed from (GUI git
// clients don't load ~/.local/bin into PATH) and fails closed when it's missing.
// `git commit --no-verify` still bypasses it: a deliberate, visible human choice.
// The template copy (git init and clone copy it from init.templateDir) stays a no-op until
// `sindri repo onboard` replaces it with the full hook, so it never blocks a repo nobody
// onboarded. `repo status` exits 0 (onboarded), 1 (not) or 2 (it failed): a failure is one
// stderr line and still never blocks the commit.
export function preCommitHook(bin: string, o: { template?: boolean } = {}): string {
  const gate = o.template === true
    ? `${TEMPLATE_MARKER}
if [ ! -x "$SINDRI" ] && ! command -v "$SINDRI" >/dev/null 2>&1; then exit 0; fi
"$SINDRI" repo status >/dev/null 2>&1
case $? in
  0) ;;
  1) exit 0 ;;
  *) echo "sindri: repo status failed; secret scan skipped" >&2; exit 0 ;;
esac
`
    : `if [ ! -x "$SINDRI" ] && ! command -v "$SINDRI" >/dev/null 2>&1; then
  echo "sindri-scrub: $SINDRI not found, so the secret scan can't run; refusing the commit." >&2
  echo "  fix: scripts/install-sindri.sh (or commit with --no-verify and say why)" >&2
  exit 1
fi
`;
  return `#!/bin/sh
${PRE_COMMIT_MARKER}
# Refuses commits that add secret-shaped strings (spec §8.4), then records shape signals (spec §6.2).
# Installed by \`${o.template === true ? "sindri repo onboard --template" : "sindri scrub --install-pre-commit"}\`.
SINDRI='${bin.replace(/'/g, "'\\''")}'
${gate}"$SINDRI" scrub --staged || exit 1
# Record-only shape signals (spec §6.2): never blocks the commit.
"$SINDRI" shape --record --staged || true
`;
}

export function isTemplateHook(text: string): boolean {
  return text.split("\n").includes(TEMPLATE_MARKER);
}

export function hookBinary(hookText: string): string | null {
  return /^SINDRI='((?:[^']|'\\'')*)'$/m.exec(hookText)?.[1].replace(/'\\''/g, "'") ?? null;
}

// git resolves core.hooksPath itself (relative values against the top level, ~ expansion),
// and does so from any subdirectory; --path-format=absolute needs git 2.31+.
export async function preCommitPath(git: GitRunner, repoPath: string): Promise<string | null> {
  const gitPath = await git.run(["rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit"], repoPath);
  return gitPath.ok ? gitPath.stdout.trim() : null;
}

export interface AddedFile {
  file: string;
  lines: { line: number; text: string }[];
}

// Reads `git diff --unified=0`. Inside a hunk, lines are counted from the hunk
// header, so an added line whose text starts with "++ " is still an addition,
// never mistaken for a "+++ " file header.
export function parseAddedLines(diff: string): AddedFile[] {
  const out: AddedFile[] = [];
  let cur: AddedFile | null = null;
  let newLine = 0;
  let oldLeft = 0;
  let newLeft = 0;
  for (const l of diff.split("\n")) {
    if (oldLeft > 0 || newLeft > 0) {
      if (l.startsWith("+")) {
        cur?.lines.push({ line: newLine, text: l.slice(1) });
        newLine++;
        newLeft--;
      } else if (l.startsWith("-")) {
        oldLeft--;
      } else if (!l.startsWith("\\")) {
        newLine++;
        newLeft--;
        oldLeft--;
      }
      continue;
    }
    if (l.startsWith("+++ ")) {
      const f = l.slice(4);
      cur = f === "/dev/null" ? null : { file: f.replace(/^b\//, ""), lines: [] };
      if (cur !== null) out.push(cur);
      continue;
    }
    const h = /^@@ -\d+(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/.exec(l);
    if (h !== null) {
      oldLeft = h[1] === undefined ? 1 : Number(h[1]);
      newLine = Number(h[2]);
      newLeft = h[3] === undefined ? 1 : Number(h[3]);
    }
  }
  return out.filter((f) => f.lines.length > 0);
}

// One file's additions are scanned as one text, so a secret split over lines
// (a PEM private key) is still found; each hit maps back to its first line.
export function hitsIn(f: AddedFile, scrubber: Scrubber): { file: string; line: number; kind: string }[] {
  const starts: number[] = [];
  let offset = 0;
  for (const l of f.lines) {
    starts.push(offset);
    offset += l.text.length + 1;
  }
  return scrubber.find(f.lines.map((l) => l.text).join("\n")).map((h) => {
    let i = starts.length - 1;
    while (starts[i] > h.start) i--;
    return { file: f.file, line: f.lines[i].line, kind: h.kind };
  });
}

// The latest approved snapshot, or null when there is none (or no readable ledger).
function approvedSnapshot(deps: Deps): LoadedProfile | null {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return null;
  try {
    return readLedger(file, (db) => approvedProfile(deps, db));
  } catch {
    return null;
  }
}

// Spec §8.7: the patterns come from the latest approved snapshot, so an unapproved
// edit can't change them. A profile never approved yet uses its live patterns. A
// profile that neither loads nor has a snapshot refuses: never built-ins only.
function scrubberFor(deps: Deps): { scrubber: Scrubber; warning: string } {
  const root = resolveProfileRoot(deps);
  if (root === null) return { scrubber: makeScrubber(), warning: "" };
  const live = loadProfile(root);
  const approved = approvedSnapshot(deps);
  const use = approved ?? (live.ok ? live.value : null);
  if (use === null) {
    throw new SindriError("SND-PROFILE-001", `the profile at ${root} is invalid and has no approved snapshot, so its scrub patterns can't be used`, {
      fix: "sindri profile validate, fix each listed key, then sindri profile approve",
    });
  }
  const drift = approved !== null && (!live.ok || live.value.hash !== approved.hash);
  const warning = drift ? `note: using approved profile ${approved.hash.slice(0, 12)}; the live profile has unapproved changes (sindri profile approve)\n` : "";
  return { scrubber: makeScrubber(compileExtraPatterns(use.profile.scrub.extraPatterns)), warning };
}

// A foreign hook reached through core.hooksPath is never edited, and core.hooksPath is never set.
// When that hook is husky's (under .husky/, or its shim), the local-only route is husky's
// ${XDG_CONFIG_HOME:-~/.config}/husky/init.sh, which husky sources before every hook: the two lines
// there, guarded to this repo and calling the installed binary (GUI git clients have no ~/.local/bin).
const HUSKY = /husky/;
async function foreignHookFix(deps: Deps, repoPath: string, hook: string, text: string): Promise<string | undefined> {
  if (!hook.includes(`${path.sep}.husky${path.sep}`) && !HUSKY.test(text)) return undefined;
  const hooksPath = await deps.git.run(["config", "--get", "core.hooksPath"], repoPath);
  const common = await deps.git.run(["rev-parse", "--path-format=absolute", "--git-common-dir"], repoPath);
  if (!hooksPath.ok || !common.ok) return undefined;
  const q = (s: string): string => `'${s.replace(/'/g, "'\\''")}'`;
  const bin = q(deps.env.SINDRI_BIN ?? "sindri");
  return `core.hooksPath (${hooksPath.stdout.trim()}) makes ${hook} this repo's hook; sindri never edits it and never sets core.hooksPath. ` +
    `Local-only route (husky): add to \${XDG_CONFIG_HOME:-~/.config}/husky/init.sh: ` +
    `if [ "$(basename "$0")" = pre-commit ] && [ "$(git rev-parse --path-format=absolute --git-common-dir)" = ${q(common.stdout.trim())} ]; then ${bin} scrub --staged || exit 1; ${bin} shape --record --staged || true; fi`;
}

// Replaces any sindri hook (v1, v2 or the template copy); refuses a foreign one.
export async function installPreCommit(deps: Deps, repoPath: string): Promise<{ hook: string; changed: boolean }> {
  const hook = await preCommitPath(deps.git, repoPath);
  if (hook === null) throw new SindriError("SND-SCRUB-004", `${repoPath} is not inside a git repo`);
  const text = preCommitHook(deps.env.SINDRI_BIN ?? "sindri");
  const old = fs.existsSync(hook) ? fs.readFileSync(hook, "utf8") : null;
  if (old !== null && !isSindriHook(old)) {
    throw new SindriError("SND-SCRUB-003", `${hook} already exists and is not sindri's`, { fix: await foreignHookFix(deps, repoPath, hook, old) });
  }
  if (old === text) return { hook, changed: false };
  fs.mkdirSync(path.dirname(hook), { recursive: true });
  fs.writeFileSync(hook, text);
  fs.chmodSync(hook, 0o755);
  return { hook, changed: true };
}

async function install(deps: Deps, repo: string | undefined, json: boolean): Promise<CommandResult> {
  const { hook } = await installPreCommit(deps, path.resolve(deps.cwd, repo ?? "."));
  return success(`Installed the secret-scan pre-commit hook at ${hook}.`, { hook }, json);
}

async function staged(deps: Deps, json: boolean): Promise<CommandResult> {
  // -M and --diff-filter=d: renamed, copied and type-changed files are scanned too.
  // --text: binary-looking files and `-diff` attributes would otherwise print
  // "Binary files differ" with no lines, hiding a secret from the scan.
  const args = ["-c", "core.quotePath=false", "diff", "--cached", "--unified=0", "--no-color", "--no-ext-diff", "--text", "--no-textconv", "-M", "--diff-filter=d"];
  const diff = await deps.git.run(args, deps.cwd);
  if (!diff.ok) {
    // Outside a repo, git diff falls back to --no-index and its error says nothing useful.
    const inRepo = await deps.git.run(["rev-parse", "--git-dir"], deps.cwd);
    if (!inRepo.ok) throw new SindriError("SND-SCRUB-004", `${deps.cwd} is not inside a git repo`);
    throw new SindriError("SND-SCRUB-005", "git diff --cached failed, so the staged changes were not scanned", { details: diff.stderr.trim().split("\n") });
  }
  const { scrubber, warning } = scrubberFor(deps);
  const hits = parseAddedLines(diff.stdout).flatMap((f) => hitsIn(f, scrubber));
  if (hits.length === 0) return { ...success("No secrets in staged changes.", { hits }, json), stderr: warning };
  const r = failure("SND-SCRUB-002", `refused: ${hits.length} likely secret(s) in staged changes:`, json, {
    details: hits.map((h) => `${h.file}:${h.line} ${h.kind}`),
    exitCode: 1,
  });
  return { ...r, stderr: warning + r.stderr };
}

export const scrubCommand: Command = async (args, deps) => {
  const json = args.includes("--json");
  try {
    const { values } = parseFlags(args, { staged: { type: "boolean" }, "install-pre-commit": { type: "boolean" }, repo: { type: "string" }, json: { type: "boolean" } });
    if (values["install-pre-commit"] === true) return await install(deps, values.repo, json);
    if (values.staged === true) return await staged(deps, json);
    const { scrubber, warning } = scrubberFor(deps);
    const out = scrubber.scrub(await deps.stdin());
    const stderr = warning + (out.hits.length > 0 ? `scrubbed ${out.hits.length} hit(s)\n` : "");
    // A filter: the text passes through as-is; --json wraps it with the hits.
    if (json) return { ...success("", { text: out.text, hits: out.hits }, true), stderr };
    return { exitCode: 0, stdout: out.text, stderr };
  } catch (e) {
    return fromError(e, json);
  }
};
