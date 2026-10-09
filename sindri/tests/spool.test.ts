import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { ingestSpool, spoolDir, writeShapeRun, type ShapeRun } from "../src/index/spool.js";
import type { Signal, SignalType } from "../src/index/signals.js";
import { bumpEpoch, openMemoryLedger } from "../src/ledger/db.js";
import { makeDeps } from "./helpers.js";

const sig: Signal = {
  type: "reinvented:exact", layer: "clones", value: 1, threshold: 1, at: "src/a.ts:1", existing: "src/b.ts:1",
  detail: "<untrusted>a</untrusted> has the same structure as <untrusted>b</untrusted>", name: "a", astHash: "a".repeat(64),
};
const run = (over: Partial<ShapeRun> = {}): ShapeRun => ({
  runId: "01k0000000000000000000000a", repo: "r", ts: "2026-10-08T12:00:00.000Z", head: "a".repeat(40), tree: "b".repeat(40), elapsedMs: 40,
  indexAgeMs: 3_600_000, providers: { embedder: null, graph: null }, deferred: ["embeddings"], signals: [sig], ...over,
});

describe("shape spool", () => {
  it("writes runs atomically and ingests them into the ledger once", () => {
    const d = makeDeps();
    const file = writeShapeRun(d, run());
    expect(path.basename(file)).toBe("shape-01k0000000000000000000000a.json");
    expect(fs.readdirSync(spoolDir(d)).filter((n) => n.includes(".tmp"))).toEqual([]);
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 1, signals: 1, quarantined: 0 });
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 0, signals: 0, quarantined: 0 });
    expect(db.prepare("SELECT type, layer, at, existing, name, ast_hash, outcome, epoch FROM shape_signals").all()).toEqual([
      { type: "reinvented:exact", layer: "clones", at: "src/a.ts:1", existing: "src/b.ts:1", name: "a", ast_hash: "a".repeat(64), outcome: null, epoch },
    ]);
    expect(db.prepare("SELECT repo, deferred, signal_count, tree, commit_sha, index_age_ms, providers FROM shape_runs").get()).toEqual({
      repo: "r", deferred: '["embeddings"]', signal_count: 1, tree: "b".repeat(40), commit_sha: null, index_age_ms: 3_600_000, providers: '{"embedder":null,"graph":null}',
    });
  });

  it("deletes a replayed file without inserting its signals a second time", () => {
    const d = makeDeps();
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    writeShapeRun(d, run());
    expect(ingestSpool(db, d, epoch).runs).toBe(1);
    writeShapeRun(d, run());
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 0, signals: 0, quarantined: 0 });
    expect(fs.readdirSync(spoolDir(d)).filter((n) => n.startsWith("shape-"))).toEqual([]);
    expect(db.prepare("SELECT COUNT(*) AS n FROM shape_signals").get()).toEqual({ n: 1 });
  });

  it("quarantines files that fail validation, forged enums, links, directories and oversized files", () => {
    const d = makeDeps();
    fs.mkdirSync(spoolDir(d), { recursive: true });
    fs.writeFileSync(path.join(spoolDir(d), "shape-bad.json"), JSON.stringify({ nope: 1 }));
    fs.writeFileSync(path.join(spoolDir(d), "shape-garbage.json"), "not json");
    fs.writeFileSync(path.join(spoolDir(d), "shape-huge.json"), "x".repeat(2 * 1024 * 1024));
    fs.symlinkSync("/etc/hosts", path.join(spoolDir(d), "shape-link.json"));
    fs.mkdirSync(path.join(spoolDir(d), "shape-dir.json"));
    writeShapeRun(d, run({ runId: "01k0000000000000000000000c", signals: [{ ...sig, type: "bogus" as unknown as SignalType }] }));
    writeShapeRun(d, run({ runId: "01k0000000000000000000000d", repo: "../x" }));
    writeShapeRun(d, run({ runId: "01k0000000000000000000000b" }));
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    expect(ingestSpool(db, d, epoch)).toEqual({ runs: 1, signals: 1, quarantined: 7 });
    expect(fs.readdirSync(path.join(spoolDir(d), "quarantine")).sort()).toEqual([
      "shape-01k0000000000000000000000c.json", "shape-01k0000000000000000000000d.json", "shape-bad.json", "shape-dir.json", "shape-garbage.json", "shape-huge.json", "shape-link.json",
    ]);
  });

  it("scrubs secrets and strips control characters from signal text before it reaches the ledger", () => {
    const d = makeDeps();
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    writeShapeRun(d, run({ signals: [{ ...sig, detail: `leak ${secret}\u001b[31m red`, existing: null, name: null, astHash: null }] }));
    const db = openMemoryLedger();
    expect(ingestSpool(db, d, bumpEpoch(db)).signals).toBe(1);
    const row = db.prepare("SELECT detail, existing FROM shape_signals").get() as { detail: string; existing: string | null };
    expect(row.detail).not.toContain(secret);
    expect(row.detail).not.toContain("\u001b");
    expect(row.detail).toContain("[31m red");
    expect(row.existing).toBeNull();
  });

  it("does nothing when there is no spool", () => {
    const db = openMemoryLedger();
    expect(ingestSpool(db, makeDeps(), bumpEpoch(db))).toEqual({ runs: 0, signals: 0, quarantined: 0 });
  });
});
