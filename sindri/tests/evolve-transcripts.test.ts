import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { excerptFor, parseSince, readRepoSessions, sessionOf, textBlocks, toolNames, transcriptsDir } from "../src/evolve/transcripts.js";
import { evolveFixture } from "./evolve-fixtures.js";
import { tempDir } from "./helpers.js";

const line = (o: unknown): string => JSON.stringify(o);

describe("textBlocks", () => {
  it("flattens string, text and tool_result content, and ignores everything else", () => {
    expect(textBlocks("hi")).toEqual([{ text: "hi", toolResult: false }]);
    expect(textBlocks(42)).toEqual([]);
    expect(
      textBlocks([
        null,
        "loose string",
        { type: "text", text: "a" },
        { type: "text", text: 7 },
        { type: "tool_result", content: "result" },
        { type: "tool_result", content: [{ type: "text", text: "x" }, { type: "text", text: "y" }] },
        { type: "image" },
      ]),
    ).toEqual([
      { text: "a", toolResult: false },
      { text: "result", toolResult: true },
      { text: "x\ny", toolResult: true },
    ]);
  });
});

describe("toolNames", () => {
  it("lists the names of tool_use blocks and ignores everything else", () => {
    expect(toolNames("plain")).toEqual([]);
    expect(toolNames([null, "x", { type: "text", text: "t" }, { type: "tool_use", name: 7 }, { type: "tool_use", name: "Edit" }, { type: "tool_use", name: "Read" }])).toEqual(["Edit", "Read"]);
  });
});

describe("sessionOf and parseSince", () => {
  it("takes the first eight characters of the session file name", () => {
    expect(sessionOf("/p/5e55a1d0-0000-4000-8000-000000000001.jsonl")).toBe("5e55a1d0");
    expect(sessionOf("s.jsonl")).toBe("s");
  });

  it("parses N d, defaulting to 7 days", () => {
    const now = new Date("2026-10-08T12:00:00Z");
    expect(parseSince(undefined, now).toISOString()).toBe("2026-10-01T12:00:00.000Z");
    expect(parseSince("30d", now).toISOString()).toBe("2026-09-08T12:00:00.000Z");
    for (const bad of ["0d", "7", "x", "7h", "1234d"]) expect(() => parseSince(bad, now)).toThrow(/--since must look like 7d/);
  });
});

describe("readRepoSessions (Review Focus 5)", () => {
  const dir = () => {
    const d = tempDir();
    fs.mkdirSync(path.join(d, "proj"));
    return d;
  };

  it("keeps only lines whose latest cwd is the repo or under it, with stable refs", () => {
    const d = dir();
    fs.writeFileSync(
      path.join(d, "proj", "abcd1234-ffff.jsonl"),
      [
        line({ type: "user", cwd: "/other", timestamp: "2026-10-01T00:00:00Z", message: { content: "elsewhere" } }),
        line({ type: "user", cwd: "/repo/sub", timestamp: "2026-10-01T00:00:01Z", message: { content: "inside" } }),
        line({ type: "user", timestamp: "2026-10-01T00:00:02Z", message: { content: "inherits the cwd" } }),
        line({ type: "user", cwd: "/repository", timestamp: "2026-10-01T00:00:03Z", message: { content: "prefix only" } }),
        "{broken",
        "null",
        line({ type: "system", cwd: "/repo", gitBranch: "feat/x", content: "top-level content" }),
      ].join("\n"),
    );
    const r = readRepoSessions(d, "/repo", new Date(0));
    expect(r.lines.map((l) => [l.ref, l.type, l.blocks.map((b) => b.text)])).toEqual([
      ["transcript:abcd1234#2", "user", ["inside"]],
      ["transcript:abcd1234#3", "user", ["inherits the cwd"]],
      ["transcript:abcd1234#7", "system", ["top-level content"]],
    ]);
    expect(r.lines[2]).toMatchObject({ branch: "feat/x", session: "abcd1234", ts: "", tools: [] });
    expect(r.files).toBe(1);
  });

  it("gives an entry with no type an empty type", () => {
    const d = dir();
    fs.writeFileSync(path.join(d, "proj", "dddd0004.jsonl"), line({ cwd: "/repo", message: { content: "typeless" } }));
    expect(readRepoSessions(d, "/repo", new Date(0)).lines.map((l) => [l.type, l.blocks[0].text])).toEqual([["", "typeless"]]);
  });

  it("counts a line copied into a later (forked or resumed) file once, but the same text at another time twice", () => {
    const d = dir();
    const at = (ts: string, text: string, type = "user") => line({ type, cwd: "/repo", ...(ts === "" ? {} : { timestamp: ts }), message: { content: type === "assistant" ? [{ type: "text", text }] : text } });
    const copied = [
      at("2026-10-01T00:00:00Z", "Fix the retry flag"),
      at("2026-10-01T00:00:01Z", "Working on it", "assistant"),
      at("2026-10-01T00:00:02Z", "Stop hook feedback:\n[/repo/config/hooks/done-gate.sh # aw:done-gate]: Claiming done"),
      at("", "undated turn"),
      at("2026-10-01T00:00:03Z", "  "),
    ];
    fs.writeFileSync(path.join(d, "proj", "aaaa0001.jsonl"), copied.join("\n"));
    fs.writeFileSync(
      path.join(d, "proj", "bbbb0002.jsonl"),
      [...copied, at("2026-10-02T00:00:00Z", "Fix  the retry\nflag"), at("2026-10-02T00:00:01Z", "Fix the retry flag"), at("2026-10-02T00:00:02Z", "A new turn")].join("\n"),
    );
    const r = readRepoSessions(d, "/repo", new Date(0));
    expect(r.lines.map((l) => [l.ref, l.type, l.ts])).toEqual([
      ["transcript:aaaa0001#1", "user", "2026-10-01T00:00:00Z"],
      ["transcript:aaaa0001#2", "assistant", "2026-10-01T00:00:01Z"],
      ["transcript:aaaa0001#3", "user", "2026-10-01T00:00:02Z"],
      ["transcript:aaaa0001#4", "user", ""],
      ["transcript:aaaa0001#5", "user", "2026-10-01T00:00:03Z"],
      ["transcript:bbbb0002#2", "assistant", "2026-10-01T00:00:01Z"],
      ["transcript:bbbb0002#4", "user", ""],
      ["transcript:bbbb0002#5", "user", "2026-10-01T00:00:03Z"],
      ["transcript:bbbb0002#6", "user", "2026-10-02T00:00:00Z"],
      ["transcript:bbbb0002#7", "user", "2026-10-02T00:00:01Z"],
      ["transcript:bbbb0002#8", "user", "2026-10-02T00:00:02Z"],
    ]);
    // Within one file nothing is deduped: only an earlier file's lines count as copies.
    const solo = dir();
    fs.writeFileSync(path.join(solo, "proj", "cccc0003.jsonl"), [copied[0], copied[0]].join("\n"));
    expect(readRepoSessions(solo, "/repo", new Date(0)).lines).toHaveLength(2);
  });

  it("skips files older than since without reading them, and caps the number of files", () => {
    const d = dir();
    const old = path.join(d, "proj", "old00000.jsonl");
    fs.writeFileSync(old, line({ type: "user", cwd: "/repo", timestamp: "2020-01-01T00:00:00Z", message: { content: "old" } }));
    fs.utimesSync(old, new Date("2020-01-01"), new Date("2020-01-01"));
    for (const n of ["aaaaaaaa", "bbbbbbbb", "cccccccc"]) {
      fs.writeFileSync(path.join(d, "proj", `${n}.jsonl`), line({ type: "user", cwd: "/repo", timestamp: "2026-10-01T00:00:00Z", message: { content: n } }));
    }
    fs.writeFileSync(path.join(d, "proj", "notes.txt"), "ignored");
    const r = readRepoSessions(d, "/repo", new Date("2026-01-01"), 2);
    expect(r.files).toBe(2);
    expect(r.skipped).toBe(2);
    expect(readRepoSessions(path.join(d, "missing"), "/repo", new Date(0))).toEqual({ lines: [], files: 0, skipped: 0 });
  });
});

describe("excerptFor and transcriptsDir", () => {
  it("returns a scrubbed, single-line excerpt for a stable ref, and null for anything it can't resolve", async () => {
    const d = tempDir();
    fs.mkdirSync(path.join(d, "proj"));
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    fs.writeFileSync(
      path.join(d, "proj", "5e55a1d0-1111.jsonl"),
      [line({ type: "assistant", message: { content: [{ type: "text", text: `first\nline ${secret}` }] } }), line({ type: "user", message: { content: "" } }), "garbage", line({ type: "user", message: { content: "x".repeat(400) } })].join("\n"),
    );
    expect(excerptFor(d, "transcript:5e55a1d0#1")).toBe("first line [REDACTED:aws-access-key]");
    expect(excerptFor(d, "transcript:5e55a1d0#4")).toBe("x".repeat(300));
    expect(excerptFor(d, "transcript:5e55a1d0#2")).toBeNull();
    expect(excerptFor(d, "transcript:5e55a1d0#3")).toBeNull();
    expect(excerptFor(d, "transcript:5e55a1d0#99")).toBeNull();
    expect(excerptFor(d, "transcript:deadbeef#1")).toBeNull();
    expect(excerptFor(d, "pr:12")).toBeNull();
    expect(excerptFor(path.join(d, "missing"), "transcript:5e55a1d0#1")).toBeNull();
    const fx = await evolveFixture();
    expect(transcriptsDir(fx.ctx)).toBe(fx.transcripts);
    fx.close();
  });

  it("expands ~ in the configured transcripts dir", async () => {
    const fx = await evolveFixture({ extraYaml: "" });
    const ctx = { ...fx.ctx, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, sources: { ...fx.ctx.loaded.profile.sources, transcripts: { enabled: true, dir: "~/sessions" } } } } };
    expect(transcriptsDir(ctx)).toBe(path.join(fx.deps.home, "sessions"));
    fx.close();
  });
});
