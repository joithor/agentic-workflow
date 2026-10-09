import { ok } from "../../adapters/types.js";
import type { Deps } from "../../deps.js";
import { allSymbols, indexPath, openIndexReadOnly, type SymbolRow } from "../../index/db.js";
import type { Scrubber } from "../../scrub/scrub.js";
import { clean, type Source, type SourceRecord } from "../source.js";

const words = (name: string): string[] => name.replace(/([a-z0-9])([A-Z])/g, "$1 $2").toLowerCase().split(/[^a-z0-9]+/).filter((w) => w !== "");

// Today's code: a backtest (asOf set) gets none unless allowAsOf (then it's "leaky").
// With no keywords nothing matches (the brief gave us nothing to look for).
// The index stores scrubbed bodies (Plan 3); clean() strips them again at fetch.
export function codeSource(deps: Deps, repos: string[], o: { allowAsOf?: boolean; scrubber?: Scrubber } = {}): Source {
  return {
    name: "code",
    async find(q) {
      if (q.keywords.length === 0 || (q.asOf !== null && o.allowAsOf !== true)) return ok([]);
      const out: SourceRecord[] = [];
      for (const repo of repos) {
        const db = openIndexReadOnly(indexPath(deps, repo));
        if (db === null) continue;
        let picked: SymbolRow[];
        let bodies: Map<number, string>;
        try {
          const syms = allSymbols(db);
          const score = (s: SymbolRow): number => words(s.name).filter((w) => q.keywords.some((k) => k.startsWith(w) || w.startsWith(k))).length;
          const matched = syms.filter((s) => s.kind !== "class" && score(s) >= Math.min(2, q.keywords.length)).sort((a, b) => score(b) - score(a));
          const called = new Set(matched.flatMap((s) => s.callees));
          picked = [...matched, ...syms.filter((s) => called.has(s.name) && !matched.includes(s))].slice(0, q.limit);
          // SymbolRow carries no body (Plan 3 keeps bodies for embeddings), so read only the picked ones here.
          const bodyOf = db.prepare("SELECT body FROM symbols WHERE id = ?");
          bodies = new Map(picked.map((s) => [s.id, (bodyOf.get(s.id) as { body: string }).body]));
        } finally {
          db.close();
        }
        for (const s of picked.sort((a, b) => a.file.localeCompare(b.file) || a.startLine - b.startLine)) {
          out.push({
            ref: `code:${repo}/${s.file}:${s.startLine}`, kind: "code", title: `${s.name}${s.signature}`,
            text: clean((bodies.get(s.id) as string).slice(0, 600), o.scrubber), author: null, createdAt: null, trust: "untrusted",
          });
        }
      }
      return ok(out);
    },
  };
}
