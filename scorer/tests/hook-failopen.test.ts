import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { readFailOpenLog, renderFailOpenSection } from "../src/hook-failopen.js";

const SINCE = new Date("2026-10-09T00:00:00.000Z");
const UNTIL = new Date("2026-10-10T00:00:00.000Z");

function writeLog(lines: string[]): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "failopen-"));
  const file = path.join(dir, "failopen.log");
  fs.writeFileSync(file, lines.join("\n"));
  return file;
}

const event = (ts: string, hook: string, cause: string): string =>
  JSON.stringify({ ts, event: "HOOK-FAILOPEN", hook, cause, provider: "claude" });

describe("readFailOpenLog", () => {
  it("returns no rows when the log doesn't exist", () => {
    expect(readFailOpenLog(`/tmp/does-not-exist-${Date.now()}/failopen.log`, SINCE, UNTIL)).toEqual([]);
  });

  it("counts events per hook and cause inside the window, most frequent first", () => {
    const file = writeLog([
      event("2026-10-09T01:00:00Z", "send-gate", "judge-exit-127"),
      event("2026-10-09T03:00:00Z", "send-gate", "judge-exit-127"),
      event("2026-10-09T02:00:00Z", "done-gate", "transcript-unreadable"),
      event("2026-10-08T23:59:59Z", "send-gate", "judge-exit-127"),
      event("2026-10-10T00:00:01Z", "send-gate", "judge-exit-127"),
      "not json",
      JSON.stringify({ ts: "2026-10-09T05:00:00Z", event: "OTHER", hook: "x", cause: "y" }),
      JSON.stringify({ ts: "2026-10-09T05:00:00Z", event: "HOOK-FAILOPEN" }),
      "",
    ]);
    expect(readFailOpenLog(file, SINCE, UNTIL)).toEqual([
      { hook: "send-gate", cause: "judge-exit-127", events: 2, last: "2026-10-09T03:00:00Z" },
      { hook: "done-gate", cause: "transcript-unreadable", events: 1, last: "2026-10-09T02:00:00Z" },
    ]);
  });

  it("orders equal counts by hook, then cause", () => {
    const file = writeLog([
      event("2026-10-09T01:00:00Z", "scope-gate", "b"),
      event("2026-10-09T01:00:00Z", "scope-gate", "a"),
      event("2026-10-09T01:00:00Z", "done-gate", "z"),
    ]);
    expect(readFailOpenLog(file, SINCE, UNTIL).map((r) => `${r.hook} ${r.cause}`)).toEqual([
      "done-gate z", "scope-gate a", "scope-gate b",
    ]);
  });

  it("skips an event whose timestamp can't be read", () => {
    const file = writeLog([event("yesterday", "send-gate", "c")]);
    expect(readFailOpenLog(file, SINCE, UNTIL)).toEqual([]);
  });
});

describe("renderFailOpenSection", () => {
  it("says so when nothing failed open", () => {
    expect(renderFailOpenSection([])).toContain("No hook failed open in this window.");
  });

  it("lists each hook and cause with its count", () => {
    const md = renderFailOpenSection([{ hook: "send-gate", cause: "judge-exit-127", events: 2, last: "2026-10-09T03:00:00Z" }]);
    expect(md).toContain("## Hook fail-open");
    expect(md).toContain("| send-gate | judge-exit-127 | 2 | 2026-10-09T03:00:00Z |");
  });
});
