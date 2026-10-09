import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { corpusDir, holdoutTitles, isHoldout, loadCorpus, mentionsHoldout, readCorpus, saveReplay, split, trySaveReplay, type ReplayItem } from "../src/evolve/corpus.js";
import { stateDir } from "../src/deps.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { corpusSection } from "../src/evolve/cmd/status.js";
import { evolveFixture } from "./evolve-fixtures.js";
import { makeDeps } from "./helpers.js";

const sha = (b: string): string => createHash("sha256").update(b).digest("hex");
// The ledger row recordRun would have written for this run id.
function record(d: ReturnType<typeof makeDeps>, id: string): void {
  const db = openLedger(ledgerPath(stateDir(d)));
  db.prepare("INSERT OR IGNORE INTO scope_runs (run_id, subject, mode, ts, status, rounds, surfaces, tokens, out_path, epoch) VALUES (?, 's', 'scope', 't', 'complete', 1, 1, 1, '/o', 1)").run(id);
  db.close();
}
const save = (d: ReturnType<typeof makeDeps>, it: ReplayItem): boolean => {
  record(d, it.id);
  return saveReplay(d, it);
};

const idsWhere = (want: boolean, n: number): string[] => Array.from({ length: 4000 }, (_, i) => `run-${i}`).filter((id) => isHoldout(id) === want).slice(0, n);
const item = (id: string, title = "Shift times", text = "t"): ReplayItem => ({
  id, artifact: "scope.draft", createdAt: "2026-10-08T00:00:00Z",
  brief: { ref: "file:/b.md", kind: "brief", title, text, author: null, createdAt: null, trust: "trusted" },
  records: [], outcome: { status: "complete", surfaces: 3, recall: null },
});

describe("sealed holdout (spec §7.4)", () => {
  it("is deterministic, about 30%, and independent of content", () => {
    const ids = Array.from({ length: 2000 }, (_, i) => `run-${i}`);
    const share = ids.filter(isHoldout).length / ids.length;
    expect(share).toBeGreaterThan(0.26);
    expect(share).toBeLessThan(0.34);
    expect(isHoldout("run-7")).toBe(isHoldout("run-7"));
    const { train, holdout } = split(ids.map((id) => item(id)));
    expect(train.length + holdout.length).toBe(2000);
    expect(holdout.every((h) => isHoldout(h.id))).toBe(true);
  });
});

describe("replay corpus with a manifest (Review Focus 8)", () => {
  it("saves private, scrubbed files once, and records each in an append-only manifest", () => {
    const d = makeDeps();
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    expect(save(d, item("a", "Shift times", `key ${secret}`))).toBe(true);
    expect(save(d, item("b"))).toBe(true);
    expect(save(d, item("a", "Other title"))).toBe(false);
    const dir = path.join(corpusDir(d), "scope.draft");
    expect(fs.statSync(path.join(dir, "a.json")).mode & 0o777).toBe(0o600);
    expect(fs.readFileSync(path.join(dir, "a.json"), "utf8")).not.toContain(secret);
    const manifest = fs.readFileSync(path.join(dir, "manifest.jsonl"), "utf8").trim().split("\n").map((l) => JSON.parse(l) as { id: string; sha256: string; added_at: string });
    expect(manifest.map((m) => m.id)).toEqual(["a", "b"]);
    expect(manifest[0].sha256).toMatch(/^[0-9a-f]{64}$/);
    expect(manifest[0].added_at).toBe(d.now().toISOString());
    expect(loadCorpus(d, "scope.draft").map((i) => i.id)).toEqual(["a", "b"]);
    expect(() => save(d, item("Not Valid!"))).toThrow();
    expect(loadCorpus(makeDeps(), "scope.draft")).toEqual([]);
  });

  it("drops items that aren't in the manifest, were changed, or are malformed", () => {
    const d = makeDeps();
    save(d, item("good"));
    save(d, item("tampered"));
    const dir = path.join(corpusDir(d), "scope.draft");
    fs.writeFileSync(path.join(dir, "tampered.json"), fs.readFileSync(path.join(dir, "tampered.json"), "utf8").replace("Shift times", "Swapped!"));
    fs.writeFileSync(path.join(dir, "unlisted.json"), JSON.stringify(item("unlisted")));
    const bad = "{ not valid";
    record(d, "malformed"); // recorded and listed, but not a valid item
    fs.writeFileSync(path.join(dir, "malformed.json"), bad);
    fs.appendFileSync(path.join(dir, "manifest.jsonl"), `${JSON.stringify({ id: "malformed", sha256: sha(bad), added_at: "t" })}\nnot json\n${JSON.stringify({ id: 7 })}\n${JSON.stringify({ id: "good", sha256: "0".repeat(64), added_at: "later" })}\n`);
    const r = readCorpus(d, "scope.draft");
    expect(r.items.map((i) => i.id)).toEqual(["good"]);
    expect(r.dropped.sort()).toEqual(["malformed", "tampered", "unlisted"]);
  });

  it("drops every file when there is no manifest at all", () => {
    const d = makeDeps();
    const dir = path.join(corpusDir(d), "scope.draft");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, "dropped-in.json"), JSON.stringify(item("dropped-in")));
    expect(readCorpus(d, "scope.draft")).toMatchObject({ items: [], dropped: ["dropped-in"] });
  });

  it("rethrows a write failure that isn't a duplicate id", () => {
    const d = makeDeps();
    save(d, item("first"));
    const dir = path.join(corpusDir(d), "scope.draft");
    fs.chmodSync(dir, 0o500);
    try {
      expect(() => save(d, item("second"))).toThrow(/EACCES/);
    } finally {
      fs.chmodSync(dir, 0o700);
    }
  });

  it("never throws from trySaveReplay", () => {
    const d = makeDeps();
    fs.mkdirSync(path.dirname(corpusDir(d)), { recursive: true });
    fs.writeFileSync(corpusDir(d), "a file where the directory should be");
    expect(trySaveReplay(d, item("x"))).toBe(false);
    const ok = makeDeps();
    expect(trySaveReplay(ok, item("y"))).toBe(true);
  });

  it("finds holdout titles of 12+ characters, and matches them case-insensitively", () => {
    const d = makeDeps();
    const held = idsWhere(true, 2);
    const train = idsWhere(false, 1);
    save(d, item(held[0], "Quarterly staffing overhaul"));
    save(d, item(held[1], "Short title"));
    save(d, item(train[0], "Training-only project name"));
    expect(holdoutTitles(d)).toEqual(["Quarterly staffing overhaul"]);
    expect(mentionsHoldout("we discussed the QUARTERLY STAFFING OVERHAUL today", ["Quarterly staffing overhaul"])).toBe(true);
    expect(mentionsHoldout("nothing relevant", ["Quarterly staffing overhaul"])).toBe(false);
    expect(mentionsHoldout("anything", [])).toBe(false);
  });
});

describe("the corpus section of status", () => {
  it("says nothing for an empty corpus, then how many holdout items are still needed", async () => {
    const fx = await evolveFixture();
    expect((await corpusSection(fx.ctx)).lines).toEqual([]);
    for (const id of idsWhere(true, 5)) save(fx.deps, item(id));
    for (const id of idsWhere(false, 3)) save(fx.deps, item(id));
    const s = await corpusSection(fx.ctx);
    expect(s.lines).toEqual(["Corpus: 8 items (5 holdout); 15 more holdout items needed, about 50 more scope runs (30% of runs join the holdout)."]);
    expect(s.data).toMatchObject({ corpus: { items: 8, holdout: 5, needed: 15, dropped: 0 } });
    for (const id of idsWhere(true, 25).slice(5)) save(fx.deps, item(id));
    expect((await corpusSection(fx.ctx)).lines).toEqual(["Corpus: 28 items (25 holdout); enough for a comparison."]);
    fs.writeFileSync(path.join(corpusDir(fx.deps), "scope.draft", "stray.json"), "{}");
    const flagged = await corpusSection(fx.ctx);
    expect(flagged.attention).toBe(true);
    expect(flagged.lines[1]).toBe("Corpus check: 1 item(s) failed the manifest and ledger check and are ignored.");
    const other = makeDeps();
    save(other, item("lonely"));
    fs.writeFileSync(ledgerPath(stateDir(other)), "not a database");
    const unreadable = await corpusSection({ ...fx.ctx, deps: other });
    expect(unreadable.lines[1]).toContain("(the ledger couldn't be read");
    fx.close();
  });
});

describe("the corpus is bound to the ledger and to its own filenames", () => {
  it("drops a planted item that has a valid manifest line but no scope_runs row", () => {
    const d = makeDeps();
    save(d, item("real"));
    const dir = path.join(corpusDir(d), "scope.draft");
    const planted = JSON.stringify(item("planted"));
    fs.writeFileSync(path.join(dir, "planted.json"), planted);
    fs.appendFileSync(path.join(dir, "manifest.jsonl"), `${JSON.stringify({ id: "planted", sha256: sha(planted), added_at: "t" })}\n`);
    const r = readCorpus(d, "scope.draft");
    expect(r.items.map((i) => i.id)).toEqual(["real"]);
    expect(r.dropped).toEqual(["planted"]);
    expect(r.unverified).toBeNull();
  });

  it("drops every item, with a reason, when the ledger can't be read", () => {
    const d = makeDeps();
    save(d, item("one"));
    fs.writeFileSync(ledgerPath(stateDir(d)), "not a database");
    const r = readCorpus(d, "scope.draft");
    expect(r.items).toEqual([]);
    expect(r.dropped).toEqual(["one"]);
    expect(r.unverified).toMatch(/ledger/);
    const gone = makeDeps();
    saveReplay(gone, item("two"));
    expect(readCorpus(gone, "scope.draft").unverified).toMatch(/ledger/);
  });

  it("drops an item whose content id differs from its filename", () => {
    const d = makeDeps();
    save(d, item("a"));
    record(d, "b");
    const dir = path.join(corpusDir(d), "scope.draft");
    const copy = fs.readFileSync(path.join(dir, "a.json"), "utf8");
    fs.writeFileSync(path.join(dir, "b.json"), copy);
    fs.appendFileSync(path.join(dir, "manifest.jsonl"), `${JSON.stringify({ id: "b", sha256: sha(copy), added_at: "t" })}\n`);
    const r = readCorpus(d, "scope.draft");
    expect(r.items.map((i) => i.id)).toEqual(["a"]);
    expect(r.dropped).toEqual(["b"]);
  });

  it("removes the item file when the manifest append fails, so no orphan is left", () => {
    const d = makeDeps();
    const dir = path.join(corpusDir(d), "scope.draft");
    fs.mkdirSync(path.join(dir, "manifest.jsonl"), { recursive: true });
    expect(() => saveReplay(d, item("x"))).toThrow();
    expect(fs.existsSync(path.join(dir, "x.json"))).toBe(false);
  });

  it("starts each manifest line on a new line, so a torn last line can't corrupt it", () => {
    const d = makeDeps();
    save(d, item("first"));
    const dir = path.join(corpusDir(d), "scope.draft");
    fs.appendFileSync(path.join(dir, "manifest.jsonl"), '{"id":"torn","sha25');
    save(d, item("second"));
    expect(readCorpus(d, "scope.draft").items.map((i) => i.id)).toEqual(["first", "second"]);
  });
});
