import fs from "node:fs";
import path from "node:path";

import { ok } from "../../adapters/types.js";
import type { Scrubber } from "../../scrub/scrub.js";
import { clean, keywordHits, type Source, type SourceRecord } from "../source.js";

const CAP = 4000;
// Maps this tool wrote earlier must not come back in as notes.
const GENERATED = /^(scope|backtest)-.*\.md$/;

function walk(dir: string, root: string, out: string[]): void {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name.startsWith(".")) continue;
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, root, out);
    else if (e.isFile() && e.name.endsWith(".md") && !GENERATED.test(e.name)) out.push(path.relative(root, p));
  }
}

// At most CAP + 1 bytes of a file are read, never the whole file.
function readHead(file: string): string {
  const fd = fs.openSync(file, "r");
  try {
    const buf = Buffer.alloc(CAP + 1);
    const n = fs.readSync(fd, buf, 0, buf.length, 0);
    return buf.subarray(0, n).toString("utf8");
  } finally {
    fs.closeSync(fd);
  }
}

// Spec amendment 4: file times are unreliable, so a backtest (asOf set) reads no notes.
export function notesSource(dir: string, scrubber?: Scrubber): Source {
  return {
    name: "notes",
    async find(q) {
      if (q.asOf !== null || !fs.existsSync(dir)) return ok([]);
      const files: string[] = [];
      walk(dir, dir, files);
      const scored = files
        .map((rel) => ({ rel, text: readHead(path.join(dir, rel)).slice(0, CAP) }))
        .map((f) => ({ ...f, hits: keywordHits(f.text, q.keywords) }))
        .filter((f) => f.hits >= 2)
        .sort((a, b) => b.hits - a.hits || a.rel.localeCompare(b.rel))
        .slice(0, q.limit);
      return ok(scored.map((f): SourceRecord => ({
        ref: `notes:${f.rel}`, kind: "note", title: path.basename(f.rel), text: clean(f.text, scrubber), author: null, createdAt: null, trust: "trusted",
      })));
    },
  };
}
