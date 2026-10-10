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

// The log is untrusted input (anything running as the user can append to it),
// so the schema mirrors the writer's contract exactly: a word from
// [A-Za-z0-9._:-] for hook and cause, and an ISO-8601 UTC timestamp. Anything
// else is dropped, so a line can't break or forge the Markdown table.
const WORD = /^[A-Za-z0-9._:-]{1,80}$/;
const ISO_Z = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;
const EventSchema = z.object({
  ts: z.string().regex(ISO_Z),
  event: z.literal("HOOK-FAILOPEN"),
  hook: z.string().regex(WORD),
  cause: z.string().regex(WORD),
});
type FailOpenEvent = z.infer<typeof EventSchema>;

function parseEvent(line: string): FailOpenEvent | null {
  try {
    const parsed = EventSchema.safeParse(JSON.parse(line));
    return parsed.success ? parsed.data : null;
  } catch {
    return null;
  }
}

// An unreadable log (a directory, no permission) yields no lines rather than
// failing the whole daily report, and only a regular file is opened: a FIFO
// would block the launchd report job forever.
function readLines(file: string): string[] {
  try {
    if (!fs.statSync(file).isFile()) return [];
    return fs.readFileSync(file, "utf8").split("\n");
  } catch {
    return [];
  }
}

// Reads logs/failopen.log and its rotated predecessor failopen.log.1 (JSONL),
// tolerating unparseable lines, and groups the events in [since, until] by
// hook and cause, most frequent first.
export function readFailOpenLog(file: string, since: Date, until: Date): FailOpenRow[] {
  const rows = new Map<string, FailOpenRow>();
  for (const line of [...readLines(`${file}.1`), ...readLines(file)]) {
    const e = parseEvent(line);
    if (e === null) continue;
    const at = Date.parse(e.ts);
    if (Number.isNaN(at) || at < since.getTime() || at > until.getTime()) continue;
    const key = `${e.hook}\t${e.cause}`;
    const row = rows.get(key) ?? { hook: e.hook, cause: e.cause, events: 0, last: e.ts };
    row.events += 1;
    // ts strings share one fixed ISO-Z format (checked above), so they order lexically.
    if (e.ts > row.last) row.last = e.ts;
    rows.set(key, row);
  }
  return [...rows.values()].sort((a, b) => b.events - a.events || a.hook.localeCompare(b.hook) || a.cause.localeCompare(b.cause));
}

export function renderFailOpenSection(rows: readonly FailOpenRow[]): string {
  const lines = ["## Hook fail-open", "", "A hook that fails open allows because of its own error (judge missing, input it can't read). Each one is a check that didn't run.", ""];
  if (rows.length === 0) {
    lines.push("No hook failed open in this window.");
  } else {
    lines.push("| Hook | Cause | Events | Last |", "|---|---|---|---|", ...rows.map((r) => `| ${r.hook} | ${r.cause} | ${r.events} | ${r.last} |`));
  }
  return lines.join("\n");
}
