import fs from "node:fs";

import { z } from "zod";

// One row of the daily report's "Hook fail-open" section: how often a hook
// failed open for one cause inside the report window (config/lib/failopen.sh
// writes the events; spec 2026-10-09-agent-readiness-design.md §4c, B5).
export interface FailOpenRow {
  hook: string;
  cause: string;
  events: number;
  last: string;
}

const EventSchema = z.object({ ts: z.string(), event: z.literal("HOOK-FAILOPEN"), hook: z.string(), cause: z.string() });

// Reads hooks/failopen.log (JSONL), tolerating unparseable lines, and groups
// the events in [since, until] by hook and cause, most frequent first.
export function readFailOpenLog(file: string, since: Date, until: Date): FailOpenRow[] {
  if (!fs.existsSync(file)) return [];
  const rows = new Map<string, FailOpenRow>();
  for (const line of fs.readFileSync(file, "utf8").split("\n")) {
    let parsed;
    try {
      parsed = EventSchema.safeParse(JSON.parse(line));
    } catch {
      continue;
    }
    if (!parsed.success) continue;
    const e = parsed.data;
    const at = Date.parse(e.ts);
    if (Number.isNaN(at) || at < since.getTime() || at > until.getTime()) continue;
    const key = `${e.hook}\t${e.cause}`;
    const row = rows.get(key) ?? { hook: e.hook, cause: e.cause, events: 0, last: e.ts };
    row.events += 1;
    if (e.ts > row.last) row.last = e.ts;
    rows.set(key, row);
  }
  return [...rows.values()].sort((a, b) => b.events - a.events || a.hook.localeCompare(b.hook) || a.cause.localeCompare(b.cause));
}

export function renderFailOpenSection(rows: readonly FailOpenRow[]): string {
  const lines = ["## Hook fail-open", "", "A hook that fails open allows because of its own error (judge missing, input it can't read). The cutover needs zero of these.", ""];
  if (rows.length === 0) {
    lines.push("No hook failed open in this window.");
  } else {
    lines.push("| Hook | Cause | Events | Last |", "|---|---|---|---|", ...rows.map((r) => `| ${r.hook} | ${r.cause} | ${r.events} | ${r.last} |`));
  }
  return lines.join("\n");
}
