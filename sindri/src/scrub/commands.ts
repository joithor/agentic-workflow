import fs from "node:fs";
import path from "node:path";

import { parseFlags } from "../args.js";
import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { loadProfile, resolveProfileRoot } from "../profile/load.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "./scrub.js";

export const PRE_COMMIT_MARKER = "# sindri-scrub-pre-commit v1";

// The hook calls the CLI by the absolute path it was installed from (GUI git
// clients don't load ~/.local/bin into PATH) and fails closed when it's missing.
// `git commit --no-verify` still bypasses it: a deliberate, visible human choice.
export function preCommitHook(bin: string): string {
  return `#!/bin/sh
${PRE_COMMIT_MARKER}
# Refuses commits that add secret-shaped strings (spec §8.4).
# Installed by \`sindri scrub --install-pre-commit\`.
SINDRI='${bin.replace(/'/g, "'\\''")}'
if [ ! -x "$SINDRI" ] && ! command -v "$SINDRI" >/dev/null 2>&1; then
  echo "sindri-scrub: $SINDRI not found, so the secret scan can't run; refusing the commit." >&2
  echo "  fix: scripts/install-sindri.sh (or commit with --no-verify and say why)" >&2
  exit 1
fi
exec "$SINDRI" scrub --staged
`;
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

function scrubberFor(deps: Deps): { scrubber: Scrubber; warning: string } {
  const root = resolveProfileRoot(deps);
  const loaded = root === null ? null : loadProfile(root);
  if (loaded !== null && !loaded.ok) {
    return { scrubber: makeScrubber(), warning: "warning: the profile is invalid, so only built-in patterns are used (sindri profile validate)\n" };
  }
  return { scrubber: makeScrubber(loaded === null ? [] : compileExtraPatterns(loaded.value.profile.scrub.extraPatterns)), warning: "" };
}

async function install(deps: Deps, repo: string | undefined, json: boolean): Promise<CommandResult> {
  const repoPath = path.resolve(deps.cwd, repo ?? ".");
  const hook = await preCommitPath(deps.git, repoPath);
  if (hook === null) throw new SindriError("SND-PROFILE-009", `${repoPath} is not inside a git repo`);
  if (fs.existsSync(hook) && !fs.readFileSync(hook, "utf8").includes(PRE_COMMIT_MARKER)) {
    throw new SindriError("SND-SCRUB-003", `${hook} already exists and is not sindri's`);
  }
  fs.mkdirSync(path.dirname(hook), { recursive: true });
  fs.writeFileSync(hook, preCommitHook(deps.env.SINDRI_BIN ?? "sindri"));
  fs.chmodSync(hook, 0o755);
  return success(`Installed the secret-scan pre-commit hook at ${hook}.`, { hook }, json);
}

async function staged(deps: Deps, json: boolean): Promise<CommandResult> {
  // -M and --diff-filter=d: renamed, copied and type-changed files are scanned too.
  // --text: binary-looking files and `-diff` attributes would otherwise print
  // "Binary files differ" with no lines, hiding a secret from the scan.
  const args = ["-c", "core.quotePath=false", "diff", "--cached", "--unified=0", "--no-color", "--no-ext-diff", "--text", "--no-textconv", "-M", "--diff-filter=d"];
  const diff = await deps.git.run(args, deps.cwd);
  if (!diff.ok) throw new SindriError("SND-PROFILE-009", `${deps.cwd} is not inside a git repo`);
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
    return { exitCode: 0, stdout: out.text, stderr: warning + (out.hits.length > 0 ? `scrubbed ${out.hits.length} hit(s)\n` : "") };
  } catch (e) {
    return fromError(e, json);
  }
};
