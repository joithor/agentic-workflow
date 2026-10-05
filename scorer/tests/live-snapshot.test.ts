import path from "node:path";

import { describe, expect, it } from "vitest";

import { openDb } from "../src/db.js";
import { ingestFile } from "../src/ingest.js";
import { newHealth } from "../src/format-health.js";
import { LiveSnapshotSchema, sessionUsage } from "../src/live-snapshot.js";
import { assistant, tmpDir, writeLines } from "./helpers.js";

function ingest(db: ReturnType<typeof openDb>, file: string, isMain: boolean, lines: string[]): void {
  writeLines(file, lines);
  ingestFile(db, { provider: "claude", path: file, project: "p", sessionId: "s1", agentId: isMain ? "main" : "a1", agentType: isMain ? "main" : "Explore", isMain }, newHealth());
}

describe("sessionUsage", () => {
  it("is all zeros and a null context for a session with no calls", () => {
    expect(sessionUsage(openDb(":memory:"), 200_000)).toEqual({
      context: { tokens: null, window: 200_000, percent: null },
      usage: { calls: 0, subagentCalls: 0, contextTokens: 0, outputTokens: 0, callsOver200k: 0 },
    });
  });

  it("sums every call, counts subagent calls, and reads the context from the newest MAIN call", () => {
    const db = openDb(":memory:");
    const dir = tmpDir();
    ingest(db, path.join(dir, "s1.jsonl"), true, [
      assistant({ id: "m1", ts: "2026-10-04T10:00:00.000Z", input: 100, cacheRead: 50_000, cacheCreation: 10_000, output: 20 }),
      assistant({ id: "m2", ts: "2026-10-04T10:05:00.000Z", input: 200, cacheRead: 80_000, cacheCreation: 5_000, output: 30 }),
    ]);
    ingest(db, path.join(dir, "agent-a1.jsonl"), false, [
      assistant({ id: "m3", ts: "2026-10-04T10:09:00.000Z", input: 5, cacheRead: 250_000, cacheCreation: 0, output: 7, sidechain: true }),
    ]);
    expect(sessionUsage(db, 200_000)).toEqual({
      context: { tokens: 85_200, window: 200_000, percent: 43 },
      usage: { calls: 3, subagentCalls: 1, contextTokens: 60_100 + 85_200 + 250_005, outputTokens: 57, callsOver200k: 1 },
    });
  });

  it("scales the percent by the window and clamps it at 100", () => {
    const db = openDb(":memory:");
    ingest(db, path.join(tmpDir(), "s1.jsonl"), true, [assistant({ id: "m1", ts: "2026-10-04T10:00:00.000Z", input: 300_000 })]);
    expect(sessionUsage(db, 1_000_000).context.percent).toBe(30);
    expect(sessionUsage(db, 200_000).context.percent).toBe(100);
  });
});

describe("LiveSnapshotSchema", () => {
  it("rejects a snapshot of another version and a percent outside 0..100", () => {
    const base = {
      v: 1, sessionId: "s1", at: "2026-10-04T10:00:00.000Z", transcript: { found: false, files: 0 }, ingest: { newLines: 0, ms: 1, readErrors: 0 },
      context: { tokens: null, window: 200_000, percent: null },
      usage: { calls: 0, subagentCalls: 0, contextTokens: 0, outputTokens: 0, callsOver200k: 0 },
      judge: { state: "none" }, contextGuard: { fires: 0 }, wakes: { queuedNow: 0 },
    };
    expect(LiveSnapshotSchema.safeParse(base).success).toBe(true);
    expect(LiveSnapshotSchema.safeParse({ ...base, v: 2 }).success).toBe(false);
    expect(LiveSnapshotSchema.safeParse({ ...base, context: { tokens: 1, window: 200_000, percent: 120 } }).success).toBe(false);
  });
});
