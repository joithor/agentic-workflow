import path from "node:path";

import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import { isSourcePath } from "./files.js";
import { matchesAny } from "./globs.js";
import { signature } from "./minhash.js";
import { typescriptParser, type ParsedSymbol } from "./parse-ts.js";

export interface StagedChange {
  path: string;
  text: string | null;
}

export interface SkippedFile {
  path: string;
  reason: "denied" | "symlink" | "too-large" | "unreadable";
}

export interface OverlaySymbol extends ParsedSymbol {
  minhash: Uint32Array;
}

export interface Overlay {
  symbols: OverlaySymbol[];
  changedPaths: Set<string>;
  manifests: { path: string; text: string }[];
  addedLines: number;
}

// The commit as it will be, in the commit's own worktree: staged blobs (git show :path), never
// the working tree. Denied paths and blobs over the size cap are never read or parsed.
export async function stagedChanges(
  git: GitRunner,
  worktree: string,
  o: { denyPaths: readonly string[]; maxFileKB: number },
): Promise<{ changes: StagedChange[]; addedLines: number; skipped: SkippedFile[] }> {
  const names = await git.run(["-c", "core.quotePath=false", "diff", "--cached", "--raw", "--no-abbrev", "-M", "-z"], worktree);
  if (!names.ok) throw new SindriError("SND-INDEX-002", `${worktree} is not a git repo`);
  const parts = names.stdout.split("\0").filter((p) => p !== "");
  const changes: StagedChange[] = [];
  const skipped: SkippedFile[] = [];
  const add = async (p: string, mode: string): Promise<void> => {
    if (matchesAny(p, o.denyPaths)) {
      skipped.push({ path: p, reason: "denied" });
      return;
    }
    // A staged symlink's blob is its target path: never read it as source, same as the inventory.
    if (mode === "120000") {
      skipped.push({ path: p, reason: "symlink" });
      changes.push({ path: p, text: null });
      return;
    }
    const size = await git.run(["cat-file", "-s", `:${p}`], worktree);
    if (!size.ok) {
      skipped.push({ path: p, reason: "unreadable" });
      changes.push({ path: p, text: null });
      return;
    }
    if (Number(size.stdout) > o.maxFileKB * 1024) {
      skipped.push({ path: p, reason: "too-large" });
      changes.push({ path: p, text: null });
      return;
    }
    const shown = await git.run(["show", `:${p}`], worktree);
    changes.push({ path: p, text: shown.ok ? shown.stdout : null });
  };
  // Records are `:<src mode> <dst mode> <src sha> <dst sha> <status>` then one path (two for R and C).
  for (let i = 0; i < parts.length; ) {
    const [, dstMode, , , status] = parts[i].split(" ");
    if (status.startsWith("R")) {
      // The old path is gone, which keeps a moved file from matching itself.
      changes.push({ path: parts[i + 1], text: null });
      await add(parts[i + 2], dstMode);
      i += 3;
    } else {
      if (status === "D") changes.push({ path: parts[i + 1], text: null });
      else await add(parts[i + 1], dstMode);
      i += 2;
    }
  }
  const numstat = await git.run(["diff", "--cached", "--numstat", "-M"], worktree);
  const addedLines = (numstat.ok ? numstat.stdout : "")
    .split("\n")
    .map((l) => Number(l.split("\t")[0]))
    .filter((n) => Number.isFinite(n))
    .reduce((a, b) => a + b, 0);
  return { changes: changes.sort((a, b) => a.path.localeCompare(b.path)), addedLines, skipped };
}

export function buildOverlay(changes: StagedChange[], addedLines: number): Overlay {
  const symbols: OverlaySymbol[] = [];
  const manifests: { path: string; text: string }[] = [];
  for (const c of changes) {
    if (c.text === null) continue;
    if (isSourcePath(c.path)) {
      for (const s of typescriptParser.parse(c.path, c.text)) symbols.push({ ...s, minhash: signature(s.tokens) });
    } else if (path.basename(c.path) === "package.json") {
      manifests.push({ path: c.path, text: c.text });
    }
  }
  return { symbols, changedPaths: new Set(changes.map((c) => c.path)), manifests, addedLines };
}
