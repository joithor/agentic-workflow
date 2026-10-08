import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import { matchesAny } from "./globs.js";

export interface IndexedFile {
  path: string;
  hash: string;
  size: number;
  text: string;
}

export type SkipReason = "denied" | "symlink" | "not-a-file" | "too-large" | "unreadable";

const SOURCE = /\.(?:ts|tsx|mts|cts|js|jsx|mjs|cjs)$/;

export function isSourcePath(p: string): boolean {
  return SOURCE.test(p) && !p.endsWith(".d.ts");
}

const GRAPH_INPUT = /\.(?:ts|tsx|mts|cts|js|jsx|mjs|cjs|py|go|rs|java|kt|rb|php|c|h|cc|cpp|cs|swift|md)$/;

// What graphify may read: source and docs only (spec §6.2, offline guarantee).
export function isGraphInput(p: string): boolean {
  return GRAPH_INPUT.test(p);
}

// Spec §6.2 inputs: tracked files only, minus denyPaths; symlinks are never
// followed (lstat, then O_NOFOLLOW so a file swapped for a link mid-read fails).
export async function inventory(
  git: GitRunner,
  repoPath: string,
  o: { denyPaths: readonly string[]; maxFileKB: number; maxTotalMB: number; select: (p: string) => boolean },
): Promise<{ files: IndexedFile[]; skipped: { path: string; reason: SkipReason }[] }> {
  const ls = await git.run(["ls-files", "-z", "--cached"], repoPath);
  if (!ls.ok) throw new SindriError("SND-INDEX-002", `${repoPath} is not a git repo (git ls-files failed)`);
  const files: IndexedFile[] = [];
  const skipped: { path: string; reason: SkipReason }[] = [];
  let total = 0;
  for (const rel of [...new Set(ls.stdout.split("\0").filter((p) => p !== ""))].sort()) {
    if (!o.select(rel)) continue;
    if (matchesAny(rel, o.denyPaths)) {
      skipped.push({ path: rel, reason: "denied" });
      continue;
    }
    const full = path.join(repoPath, rel);
    const st = fs.lstatSync(full, { throwIfNoEntry: false });
    const reason: SkipReason | null =
      st === undefined ? "unreadable" : st.isSymbolicLink() ? "symlink" : !st.isFile() ? "not-a-file" : st.size > o.maxFileKB * 1024 ? "too-large" : null;
    if (reason !== null) {
      skipped.push({ path: rel, reason });
      continue;
    }
    let buf: Buffer;
    try {
      const fd = fs.openSync(full, fs.constants.O_RDONLY | fs.constants.O_NOFOLLOW);
      try {
        buf = fs.readFileSync(fd);
      } finally {
        fs.closeSync(fd);
      }
    } catch {
      skipped.push({ path: rel, reason: "unreadable" });
      continue;
    }
    total += buf.length;
    if (total > o.maxTotalMB * 1024 * 1024) {
      throw new SindriError("SND-INDEX-003", `index input is over index.maxTotalMB (${o.maxTotalMB} MB)`);
    }
    files.push({ path: rel, hash: createHash("sha256").update(buf).digest("hex"), size: buf.length, text: buf.toString("utf8") });
  }
  return { files, skipped };
}
