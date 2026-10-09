import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { codeSource } from "../src/scope/sources/code.js";
import { fileSource } from "../src/scope/sources/file.js";
import { notesSource } from "../src/scope/sources/notes.js";
import { transcriptsSource } from "../src/scope/sources/transcripts.js";
import { compileExtraPatterns, makeScrubber } from "../src/scrub/scrub.js";
import { makeDeps, tempDir } from "./helpers.js";
import { buildIndexForTest } from "./scope-fixtures.js";

const q = (keywords: string[], asOf: Date | null = null) => ({ keywords, asOf, limit: 10 });
const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
const turn = (content: unknown, ts?: string, over: object = {}) => JSON.stringify({ type: "user", ...(ts === undefined ? {} : { timestamp: ts }), message: { role: "user", content }, ...over });
const extra = makeScrubber(compileExtraPatterns([{ kind: "proj-tag", regex: "PRJ-[0-9]{6}" }]));

describe("local sources (Review Focus 5: stripped and scrubbed at fetch)", () => {
  it("file: one trusted brief, stripped and scrubbed, referenced by basename", async () => {
    const f = path.join(tempDir(), "brief.md");
    fs.writeFileSync(f, `# Shift times\nkey ${secret}\n<!-- hidden -->\n`);
    const r = await fileSource(f).find(q([]));
    expect(r.ok && r.value).toEqual([{ ref: "file:brief.md", kind: "brief", title: "Shift times", text: "# Shift times\nkey [REDACTED:aws-access-key]\n\n", author: null, createdAt: null, trust: "trusted" }]);
    const plain = path.join(tempDir(), "plain.md");
    fs.writeFileSync(plain, "no heading here");
    const p = await fileSource(plain).find(q([]));
    expect(p.ok && p.value[0].title).toBe("plain.md");
    const missing = await fileSource("/no/such.md").find(q([]));
    expect(!missing.ok && missing.error.code).toBe("SND-SCOPE-020");
  });

  it("every source redacts an extra pattern supplied by the caller", async () => {
    const dir = tempDir();
    fs.writeFileSync(path.join(dir, "a.md"), "# T\nshift times PRJ-123456");
    const f = await fileSource(path.join(dir, "a.md"), extra).find(q([]));
    expect(f.ok && f.value[0].text).toContain("[REDACTED:proj-tag]");
    const n = await notesSource(dir, extra).find(q(["shift", "times"]));
    expect(n.ok && n.value[0].text).toContain("[REDACTED:proj-tag]");
    fs.writeFileSync(path.join(dir, "s.jsonl"), `${turn("shift times PRJ-123456", "2026-01-01T00:00:00Z")}\n`);
    const t = await transcriptsSource(dir, undefined, extra).find(q(["shift", "times"]));
    expect(t.ok && t.value[0].text).toContain("[REDACTED:proj-tag]");
    const d = makeDeps();
    await buildIndexForTest(d, "r", { "src/shift.ts": "export function saveShiftTimes() { return 'PRJ-123456'; }\n" });
    const c = await codeSource(d, ["r"], { scrubber: extra }).find(q(["shift", "times"]));
    expect(c.ok && c.value[0].text).toContain("[REDACTED:proj-tag]");
  });

  it("notes: keyword-matching markdown, best first, titled by basename; none in a backtest; generated maps skipped", async () => {
    const dir = tempDir();
    fs.mkdirSync(path.join(dir, "sub"));
    fs.writeFileSync(path.join(dir, "a.md"), `shift times and the scheduling editor ${secret}`);
    fs.writeFileSync(path.join(dir, "sub/b.md"), "shift times");
    fs.writeFileSync(path.join(dir, "c.md"), "unrelated");
    fs.writeFileSync(path.join(dir, "notes.txt"), "shift times scheduling");
    fs.writeFileSync(path.join(dir, ".hidden.md"), "shift times scheduling");
    fs.writeFileSync(path.join(dir, "scope-old-2026-01-01.md"), "shift times scheduling generated");
    fs.writeFileSync(path.join(dir, "backtest-old-2026-01-01.md"), "shift times scheduling generated");
    fs.symlinkSync(path.join(dir, "a.md"), path.join(dir, "link.md"));
    const r = await notesSource(dir).find(q(["shift", "times", "scheduling"]));
    expect(r.ok && r.value.map((x) => [x.ref, x.title])).toEqual([["notes:a.md", "a.md"], ["notes:sub/b.md", "b.md"]]);
    expect(r.ok && r.value[0].text).toContain("[REDACTED:aws-access-key]");
    const asOf = await notesSource(dir).find(q(["shift", "times"], new Date()));
    expect(asOf.ok && asOf.value).toEqual([]);
    const none = await notesSource(path.join(dir, "missing")).find(q(["shift", "times"]));
    expect(none.ok && none.value).toEqual([]);
  });

  it("notes: reads only the per-file cap, so an over-cap file is truncated and text past it never matches", async () => {
    const dir = tempDir();
    fs.writeFileSync(path.join(dir, "big.md"), `shift times ${"x ".repeat(5000)}`);
    fs.writeFileSync(path.join(dir, "late.md"), `${"x ".repeat(5000)} shift times`);
    fs.writeFileSync(path.join(dir, "tie.md"), "shift times");
    const r = await notesSource(dir).find(q(["shift", "times"]));
    expect(r.ok && r.value.map((x) => x.ref)).toEqual(["notes:big.md", "notes:tie.md"]);
    expect(r.ok && r.value[0].text.length).toBeLessThanOrEqual(4000);
  });

  it("notes and transcripts score the cleaned text: a keyword hidden in a comment does not select a record (I3)", async () => {
    const dir = tempDir();
    fs.writeFileSync(path.join(dir, "h.md"), "shift <!-- times --> only");
    fs.writeFileSync(path.join(dir, "ok.md"), "shift times");
    const n = await notesSource(dir).find(q(["shift", "times"]));
    expect(n.ok && n.value.map((x) => x.ref)).toEqual(["notes:ok.md"]);
    fs.writeFileSync(path.join(dir, "s.jsonl"), `${turn("shift <!-- times -->", "2026-01-01T00:00:00Z")}\n${turn("shift times", "2026-01-03T00:00:00Z")}\n`);
    const t = await transcriptsSource(dir).find(q(["shift", "times"]));
    expect(t.ok && t.value.map((x) => x.ref)).toEqual(["transcript:s.jsonl#2"]);
  });

  it("notes and transcripts skip an unreadable file or directory instead of throwing", async () => {
    const dir = tempDir();
    fs.writeFileSync(path.join(dir, "ok.md"), "shift times");
    fs.writeFileSync(path.join(dir, "locked.md"), "shift times");
    fs.mkdirSync(path.join(dir, "locked-dir"));
    fs.writeFileSync(path.join(dir, "locked-dir", "x.md"), "shift times");
    fs.writeFileSync(path.join(dir, "ok.jsonl"), `${turn("shift times", "2026-01-01T00:00:00Z")}\n`);
    fs.writeFileSync(path.join(dir, "locked.jsonl"), `${turn("shift times", "2026-01-02T00:00:00Z")}\n`);
    fs.mkdirSync(path.join(dir, "locked-dir2"));
    for (const p of ["locked.md", "locked-dir", "locked.jsonl", "locked-dir2"]) fs.chmodSync(path.join(dir, p), 0o000);
    try {
      const n = await notesSource(dir).find(q(["shift", "times"]));
      expect(n.ok && n.value.map((x) => x.ref)).toEqual(["notes:ok.md"]);
      const t = await transcriptsSource(dir).find(q(["shift", "times"]));
      expect(t.ok && t.value.map((x) => x.ref)).toEqual(["transcript:ok.jsonl#1"]);
    } finally {
      for (const p of ["locked.md", "locked-dir", "locked.jsonl", "locked-dir2"]) fs.chmodSync(path.join(dir, p), 0o755);
    }
  });

  it("transcripts: human turns only, up to asOf, untrusted, referenced by basename", async () => {
    const dir = tempDir();
    fs.mkdirSync(path.join(dir, "proj"));
    const lines = [
      turn("the shift times editor needs a picker", "2026-01-01T00:00:00Z"),
      turn("shift times again, later", "2026-03-01T00:00:00Z"),
      turn([{ type: "tool_result", content: "shift times" }], "2026-01-02T00:00:00Z"),
      JSON.stringify({ type: "assistant", timestamp: "2026-01-02T00:00:00Z", message: { role: "assistant", content: "shift times" } }),
      turn("<command-name>/x</command-name> shift times", "2026-01-03T00:00:00Z"),
      turn("shift times undated"),
      JSON.stringify({ type: "user", timestamp: "2026-01-04T00:00:00Z" }),
      turn("only shift here", "2026-01-05T00:00:00Z"),
    ];
    fs.writeFileSync(path.join(dir, "proj/s1.jsonl"), `${lines.join("\n")}\n{broken\n`);
    fs.writeFileSync(path.join(dir, "proj/readme.txt"), "shift times");
    const all = await transcriptsSource(dir).find(q(["shift", "times"]));
    expect(all.ok && all.value.map((r) => [r.ref, r.trust, r.createdAt])).toEqual([
      ["transcript:s1.jsonl#1", "untrusted", "2026-01-01T00:00:00Z"],
      ["transcript:s1.jsonl#2", "untrusted", "2026-03-01T00:00:00Z"],
      ["transcript:s1.jsonl#6", "untrusted", null],
    ]);
    expect(all.ok && all.value[0].title).toBe("s1.jsonl turn 1");
    const early = await transcriptsSource(dir).find(q(["shift", "times"], new Date("2026-02-01T00:00:00Z")));
    expect(early.ok && early.value.map((r) => r.ref)).toEqual(["transcript:s1.jsonl#1"]);
    const none = await transcriptsSource(path.join(dir, "missing")).find(q(["shift", "times"]));
    expect(none.ok && none.value).toEqual([]);
  });

  it("transcripts: a turn copied into a later (forked or resumed) file is returned once; the same text at another time is a new turn", async () => {
    const dir = tempDir();
    const copied = turn("shift times copied into the fork", "2026-01-01T00:00:00Z");
    fs.writeFileSync(path.join(dir, "a.jsonl"), `${copied}\n${turn("shift times undated copy")}\n`);
    fs.writeFileSync(path.join(dir, "b.jsonl"), `${copied}\n${turn("shift  times\ncopied into the fork", "2026-01-02T00:00:00Z")}\n${turn("shift times undated copy")}\n${turn("shift times copied into the fork", "2026-01-01T00:00:00Z")}\n`);
    const r = await transcriptsSource(dir).find(q(["shift", "times"]));
    expect(r.ok && r.value.map((x) => [x.ref, x.createdAt])).toEqual([
      ["transcript:a.jsonl#1", "2026-01-01T00:00:00Z"],
      ["transcript:a.jsonl#2", null],
      ["transcript:b.jsonl#2", "2026-01-02T00:00:00Z"],
      ["transcript:b.jsonl#3", null],
    ]);
  });

  it("transcripts: caps the bytes read per file and per run", async () => {
    const dir = tempDir();
    const first = turn("shift times first", "2026-01-01T00:00:00Z");
    const second = turn("shift times second", "2026-01-02T00:00:00Z");
    fs.writeFileSync(path.join(dir, "capped.jsonl"), `${first}\n${second}\n`);
    const perFile = await transcriptsSource(dir, { perFile: Buffer.byteLength(first) + 5, perRun: 1_000_000 }).find(q(["shift", "times"]));
    expect(perFile.ok && perFile.value.map((r) => r.ref)).toEqual(["transcript:capped.jsonl#1"]);
    const multi = tempDir();
    fs.writeFileSync(path.join(multi, "a.jsonl"), `${first}\n`);
    fs.writeFileSync(path.join(multi, "b.jsonl"), `${second}\n`);
    const perRun = await transcriptsSource(multi, { perFile: 1_000_000, perRun: 1 }).find(q(["shift", "times"]));
    expect(perRun.ok && perRun.value.map((r) => r.ref)).toEqual(["transcript:a.jsonl#1"]);
  });

  it("code: symbols whose names match keywords, plus what they call; none without keywords or in a backtest by default", async () => {
    const d = makeDeps();
    await buildIndexForTest(d, "r", {
      "src/shift.ts": "export function saveShiftTimes(t: string[]) { return validateTimes(t); }\nexport function validateTimes(t: string[]) { return t.length > 0; }\n",
      "src/other.ts": "export function unrelated() { return 1; }\n",
    });
    const r = await codeSource(d, ["r"]).find(q(["shift", "times"]));
    expect(r.ok && r.value.map((x) => x.ref)).toEqual(["code:r/src/shift.ts:1", "code:r/src/shift.ts:2"]);
    expect(r.ok && r.value[0].trust).toBe("untrusted");
    expect(r.ok && r.value[0].title).toContain("saveShiftTimes");
    expect(r.ok && r.value[0].text).toContain("return validateTimes(t)");
    const noWords = await codeSource(d, ["r"]).find(q([]));
    expect(noWords.ok && noWords.value).toEqual([]);
    const backtest = await codeSource(d, ["r"]).find(q(["shift"], new Date()));
    expect(backtest.ok && backtest.value).toEqual([]);
    const leaky = await codeSource(d, ["r"], { allowAsOf: true }).find(q(["shift", "times"], new Date()));
    expect(leaky.ok && leaky.value).toHaveLength(2);
    const noIndex = await codeSource(d, ["missing"]).find(q(["shift"]));
    expect(noIndex.ok && noIndex.value).toEqual([]);
  });
});
