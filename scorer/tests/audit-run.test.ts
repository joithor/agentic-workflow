import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { runAudit } from "../src/audit/run-audit.js";

const u = (text: string, timestamp?: string) => ({ type: "user", ...(timestamp === undefined ? {} : { timestamp }), message: { content: text } });

function corpus(files: Record<string, unknown[]>): { projects: string; out: string } {
  const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
  const out = fs.mkdtempSync(path.join(os.tmpdir(), "out-"));
  const dir = path.join(projects, "repo");
  fs.mkdirSync(dir);
  for (const [name, lines] of Object.entries(files)) fs.writeFileSync(path.join(dir, name), lines.map((l) => JSON.stringify(l)).join("\n"));
  return { projects, out };
}

const run = (c: { projects: string; out: string }) =>
  runAudit({ projectsDir: c.projects, since: new Date("2026-10-01T00:00:00Z"), outDir: c.out, itemPattern: /X-\d+/, itemsFile: null, maxSize: "XS" });

describe("runAudit", () => {
  it("writes human-turns.jsonl, summary.json and baseline.md for sessions after --since", async () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "out-"));
    const dir = path.join(projects, "repo");
    fs.mkdirSync(dir);
    const lines = [
      { type: "assistant", message: { id: "m1", usage: { input_tokens: 50, output_tokens: 5 }, content: [{ type: "text", text: "Want me to push?" }] } },
      { type: "user", timestamp: "2026-10-05T00:00:00Z", message: { content: "push" } },
      { type: "user", timestamp: "2026-10-05T00:01:00Z", message: { content: "now fix ABC-7" } },
    ];
    fs.writeFileSync(path.join(dir, "s1.jsonl"), lines.map((l) => JSON.stringify(l)).join("\n"));
    const items = path.join(out, "items.json");
    fs.writeFileSync(items, JSON.stringify([{ id: "ABC-7", size: "XS", ambiguous: false, authorsTrusted: true }, { id: "ABC-8", size: "M" }]));

    const summary = await runAudit({ projectsDir: projects, since: new Date("2026-10-01T00:00:00Z"), outDir: out, itemPattern: /[A-Z][A-Z0-9]{1,9}-\d+/, itemsFile: items, maxSize: "XS" });

    expect(summary).toMatchObject({ sessions: 1, turns: 2, duplicates: 0, patterns: { push_only: { turns: 1, sessions: 1 } }, usage: { items: 1, medianTokens: 55 }, autoStart: { eligible: 1, total: 2, share: 0.5 } });
    expect(fs.readFileSync(path.join(out, "human-turns.jsonl"), "utf8").trim().split("\n")).toHaveLength(2);
    expect(JSON.parse(fs.readFileSync(path.join(out, "summary.json"), "utf8")).turns).toBe(2);
    expect(fs.readFileSync(path.join(out, "baseline.md"), "utf8")).toContain("| push_only | 1 | 1 |");
  });

  it("counts a turn shared by two session files once (resumed or forked copy) and reports duplicates", async () => {
    // The copy lands in whichever file discoverFiles yields second; the count is the same either way.
    const c = corpus({
      "s1.jsonl": [u("fix the layout", "2026-10-05T00:00:00Z"), u("then push", "2026-10-05T00:01:00Z")],
      "s2.jsonl": [u("fix the layout", "2026-10-05T00:00:00Z"), u("add a test", "2026-10-05T00:05:00Z")],
    });
    const s = await run(c);
    expect(s).toMatchObject({ sessions: 2, turns: 3, duplicates: 1 });
    expect(fs.readFileSync(path.join(c.out, "human-turns.jsonl"), "utf8").trim().split("\n")).toHaveLength(3);
    expect(fs.readFileSync(path.join(c.out, "baseline.md"), "utf8")).toContain("1 copied turns skipped");
  });

  it("counts the same text at a different timestamp twice", async () => {
    const c = corpus({ "s1.jsonl": [u("push", "2026-10-05T00:00:00Z")], "s2.jsonl": [u("push", "2026-10-05T00:01:00Z")] });
    expect(await run(c)).toMatchObject({ turns: 2, duplicates: 0 });
  });

  it("never dedupes turns with an empty timestamp", async () => {
    const c = corpus({ "s1.jsonl": [u("push")], "s2.jsonl": [u("push")] });
    expect(await run(c)).toMatchObject({ turns: 2, duplicates: 0 });
  });

  it("writes the verbatim turn file readable by the owner only", async () => {
    const c = corpus({ "s1.jsonl": [u("push", "2026-10-05T00:00:00Z")] });
    await run(c);
    expect(fs.statSync(path.join(c.out, "human-turns.jsonl")).mode & 0o077).toBe(0);
  });

  it("rejects a malformed items file with a clear error", async () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "out-"));
    const items = path.join(out, "items.json");
    fs.writeFileSync(items, JSON.stringify([{ size: "XS" }]));
    await expect(runAudit({ projectsDir: projects, since: new Date(0), outDir: out, itemPattern: /X-\d+/, itemsFile: items, maxSize: "XS" })).rejects.toThrow(/items file/);
  });

  it("skips old files, old turns, subagent files and sessions with no turns left", async () => {
    const c = corpus({
      "old.jsonl": [u("push", "2026-10-05T00:00:00Z")],
      "mixed.jsonl": [u("early", "2026-09-01T00:00:00Z"), u("late", "2026-10-05T00:00:00Z")],
      "empty.jsonl": [u("early", "2026-09-01T00:00:00Z")],
    });
    fs.utimesSync(path.join(c.projects, "repo", "old.jsonl"), new Date("2026-09-01"), new Date("2026-09-01"));
    const sub = path.join(c.projects, "repo", "mixed", "subagents");
    fs.mkdirSync(sub, { recursive: true });
    fs.writeFileSync(path.join(sub, "agent-a1.jsonl"), JSON.stringify(u("sub", "2026-10-05T00:00:00Z")));
    expect(await run(c)).toMatchObject({ sessions: 1, turns: 1 });
  });

  it("renders zero shares for an empty corpus", async () => {
    const c = corpus({});
    expect(await run(c)).toMatchObject({ sessions: 0, turns: 0, autoStart: null });
    expect(fs.readFileSync(path.join(c.out, "baseline.md"), "utf8")).toContain("push_only 0.0%");
  });

  it("reports fresh tokens as the primary per-item figure and cache reads separately", async () => {
    const c = corpus({
      "s1.jsonl": [
        { type: "assistant", message: { id: "m1", usage: { input_tokens: 10, cache_creation_input_tokens: 5, output_tokens: 5, cache_read_input_tokens: 1_000_000 } } },
        u("work on X-1", "2026-10-05T00:00:00Z"),
      ],
    });
    const s = await run(c);
    expect(s.usage).toMatchObject({ items: 1, medianTokens: 20, p75Tokens: 20, medianCacheRead: 1_000_000, p75CacheRead: 1_000_000 });
    const md = fs.readFileSync(path.join(c.out, "baseline.md"), "utf8");
    expect(md).toContain("Fresh tokens per item (input + cache writes + output)");
    expect(md).toContain("Cache-read tokens per item");
    expect(md).toContain("proxy for subscription quota");
  });

  it("rejects when the turn file cannot be written", async () => {
    const c = corpus({ "s1.jsonl": [u("push", "2026-10-05T00:00:00Z")] });
    // human-turns.jsonl is a directory, so the write stream errors.
    fs.mkdirSync(path.join(c.out, "human-turns.jsonl"));
    await expect(run(c)).rejects.toThrow();
  });

  it("closes the turn file and rethrows when reading a transcript fails", async () => {
    const c = corpus({ "s1.jsonl": [u("push", "2026-10-05T00:00:00Z")] });
    // An unreadable transcript: statSync passes, reading it throws.
    const bad = path.join(c.projects, "repo", "bad.jsonl");
    fs.writeFileSync(bad, "{}");
    fs.chmodSync(bad, 0o000);
    await expect(run(c)).rejects.toThrow();
  });
});
