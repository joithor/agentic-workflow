import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { firesForSession, readFiresLog } from "../src/context-guard-fires.js";

describe("readFiresLog", () => {
  it("returns an empty array when the directory doesn't exist", () => {
    expect(readFiresLog("/tmp/does-not-exist-" + Date.now())).toEqual([]);
  });

  it("returns an empty array when fires.jsonl doesn't exist", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "fires-"));
    expect(readFiresLog(dir)).toEqual([]);
  });

  it("parses fire lines, tolerating unparseable ones", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "fires-"));
    fs.writeFileSync(path.join(dir, "fires.jsonl"), [
      JSON.stringify({ ts: "2026-09-27T00:00:00Z", agentId: "a1", tokens: 210_000 }),
      "not json",
      JSON.stringify({ ts: "2026-09-27T00:01:00Z", agentId: null, tokens: 205_000 }),
      "",
    ].join("\n"));
    expect(readFiresLog(dir)).toEqual([
      { ts: "2026-09-27T00:00:00Z", agentId: "a1", tokens: 210_000 },
      { ts: "2026-09-27T00:01:00Z", agentId: null, tokens: 205_000 },
    ]);
  });

  it("skips a line missing required fields", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "fires-"));
    fs.writeFileSync(path.join(dir, "fires.jsonl"), JSON.stringify({ ts: "t" }));
    expect(readFiresLog(dir)).toEqual([]);
  });
});

describe("session ids on fires", () => {
  it("reads an optional sessionId, and firesForSession keeps only matching fires (old lines match nothing)", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "fires-"));
    fs.writeFileSync(path.join(dir, "fires.jsonl"), [
      JSON.stringify({ ts: "2026-09-27T00:00:00Z", agentId: null, tokens: 210_000, sessionId: "s1" }),
      JSON.stringify({ ts: "2026-09-27T00:01:00Z", agentId: "a1", tokens: 220_000, sessionId: "s2" }),
      JSON.stringify({ ts: "2026-09-27T00:02:00Z", agentId: null, tokens: 230_000 }),
    ].join("\n"));
    const fires = readFiresLog(dir);
    expect(fires).toHaveLength(3);
    expect(firesForSession(fires, "s1")).toEqual([{ ts: "2026-09-27T00:00:00Z", agentId: null, tokens: 210_000, sessionId: "s1" }]);
    expect(firesForSession(fires, "s3")).toEqual([]);
  });
});
