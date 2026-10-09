import fs from "node:fs";
import path from "node:path";

import { ok } from "../../adapters/types.js";
import type { Scrubber } from "../../scrub/scrub.js";
import { clean, keywordHits, type Source, type SourceRecord } from "../source.js";

const CAP = 1500;
export const TRANSCRIPT_CAPS = { perFile: 2 * 1024 * 1024, perRun: 50 * 1024 * 1024 };

function jsonlFiles(dir: string, root: string, out: string[]): void {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) jsonlFiles(p, root, out);
    else if (e.isFile() && e.name.endsWith(".jsonl")) out.push(path.relative(root, p));
  }
}

function readCapped(file: string, max: number): string {
  const fd = fs.openSync(file, "r");
  try {
    const buf = Buffer.alloc(Math.min(fs.fstatSync(fd).size, max));
    const n = fs.readSync(fd, buf, 0, buf.length, 0);
    return buf.subarray(0, n).toString("utf8");
  } finally {
    fs.closeSync(fd);
  }
}

// Human turns only: type "user" with plain string content that isn't an injected
// command block. (The jsonl shape is Claude Code's, which is undocumented: a
// change there makes this source return nothing, never throw.) People paste
// other text into turns, so they're untrusted.
function humanTurn(line: string): { text: string; ts: string } | null {
  let v: unknown;
  try {
    v = JSON.parse(line);
  } catch {
    return null;
  }
  const o = v as { type?: unknown; timestamp?: unknown; message?: { role?: unknown; content?: unknown } };
  if (o.type !== "user" || o.message?.role !== "user" || typeof o.message.content !== "string") return null;
  if (o.message.content.trimStart().startsWith("<")) return null;
  return { text: o.message.content, ts: typeof o.timestamp === "string" ? o.timestamp : "" };
}

export function transcriptsSource(dir: string, caps: { perFile: number; perRun: number } = TRANSCRIPT_CAPS, scrubber?: Scrubber): Source {
  return {
    name: "transcripts",
    async find(q) {
      if (!fs.existsSync(dir)) return ok([]);
      const files: string[] = [];
      jsonlFiles(dir, dir, files);
      const found: (SourceRecord & { hits: number })[] = [];
      const seen = new Set<string>(); // (timestamp, text) of turns in earlier files: forks and resumes copy lines
      let total = 0;
      for (const rel of files.sort()) {
        if (total >= caps.perRun) break;
        const text = readCapped(path.join(dir, rel), caps.perFile);
        total += Buffer.byteLength(text);
        const name = path.basename(rel);
        const own: string[] = [];
        text.split("\n").forEach((line, i) => {
          const t = humanTurn(line);
          if (t === null) return;
          if (t.ts !== "") {
            const key = `${t.ts}\u0000${t.text.replace(/\s+/g, " ").trim()}`;
            if (seen.has(key)) return;
            own.push(key);
          }
          if (q.asOf !== null && !(Date.parse(t.ts) <= q.asOf.getTime())) return;
          const hits = keywordHits(t.text, q.keywords);
          if (hits < 2) return;
          found.push({ ref: `transcript:${name}#${i + 1}`, kind: "transcript", title: `${name} turn ${i + 1}`, text: clean(t.text.slice(0, CAP), scrubber), author: "human", createdAt: t.ts || null, trust: "untrusted", hits });
        });
        for (const k of own) seen.add(k);
      }
      return ok(found.sort((a, b) => b.hits - a.hits || a.ref.localeCompare(b.ref)).slice(0, q.limit).map(({ hits: _h, ...r }) => r));
    },
  };
}
