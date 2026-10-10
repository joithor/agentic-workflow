import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { excerptFor, parseSince, resolveRefs, readRepoSessions, sessionTexts, sessionOf, textBlocks, toolNames, transcriptsDir } from "../src/evolve/transcripts.js";
import { compileExtraPatterns, makeScrubber } from "../src/scrub/scrub.js";
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
        { type: "tool_result", is_error: true, content: "failed" },
        { type: "tool_result", content: [{ type: "text", text: "x" }, { type: "text", text: "y" }] },
        { type: "image" },
      ]),
    ).toEqual([
      { text: "a", toolResult: false },
      { text: "result", toolResult: true, isError: false },
      { text: "failed", toolResult: true, isError: true },
      { text: "x\ny", toolResult: true, isError: false },
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
    // A subagent file is <session prefix>.<agent id>, so agents never collide on a 3-character prefix.
    expect(sessionOf("/p/5e55a1d0-0000-4000-8000-000000000001/subagents/agent-abc111def.jsonl")).toBe("5e55a1d0.abc111def");
    expect(sessionOf("/p/5e55a1d0-0000-4000-8000-000000000001/subagents/agent-abc222def.jsonl")).toBe("5e55a1d0.abc222def");
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

  it("resolves both paths: dot-dot segments don't count as under the repo, and a trailing slash changes nothing", () => {
    const d = dir();
    fs.writeFileSync(
      path.join(d, "proj", "eeee0005.jsonl"),
      [
        line({ type: "user", cwd: "/x/repo/../other", timestamp: "2026-10-01T00:00:00Z", message: { content: "escaped" } }),
        line({ type: "user", cwd: "relative/repo", timestamp: "2026-10-01T00:00:01Z", message: { content: "relative" } }),
        line({ type: "user", cwd: "/x/repo/sub/", timestamp: "2026-10-01T00:00:02Z", message: { content: "inside with slash" } }),
      ].join("\n"),
    );
    expect(readRepoSessions(d, "/x/repo/", new Date(0)).lines.map((l) => l.blocks[0].text)).toEqual(["inside with slash"]);
    expect(readRepoSessions(d, "/x/repo", new Date(0)).lines).toHaveLength(1);
    expect(readRepoSessions(d, "/", new Date(0)).lines).toHaveLength(2);
  });

  it("orders files by first and last timestamp, not by name, so the original keeps a copied line", () => {
    const d = dir();
    const at = (ts: string, text: string) => line({ type: "user", cwd: "/repo", timestamp: ts, message: { content: text } });
    const shared = at("2026-10-01T00:00:00Z", "shared turn");
    // The resumed session sorts first by name, but it started with the original's lines and ran longer.
    fs.writeFileSync(path.join(d, "proj", "aaaa0001.jsonl"), [shared, at("2026-10-03T00:00:00Z", "later turn")].join("\n"));
    fs.writeFileSync(path.join(d, "proj", "zzzz0002.jsonl"), [shared, at("2026-10-01T00:00:05Z", "second turn")].join("\n"));
    fs.writeFileSync(path.join(d, "proj", "mmmm0003.jsonl"), [at("garbage-time", "no usable time")].join("\n"));
    for (const n of ["pppp0004", "nnnn0005", "oooo0006"]) fs.writeFileSync(path.join(d, "proj", `${n}.jsonl`), at("2026-10-04T00:00:00Z", n)); // same span: by name
    const r = readRepoSessions(d, "/repo", new Date(0));
    expect(r.lines.map((l) => l.ref)).toEqual(["transcript:zzzz0002#1", "transcript:zzzz0002#2", "transcript:aaaa0001#2", "transcript:nnnn0005#1", "transcript:oooo0006#1", "transcript:pppp0004#1", "transcript:mmmm0003#1"]);
  });

  it("reads a session with far more lines than a spread call can take", () => {
    const d = dir();
    const one = line({ type: "user", cwd: "/repo", timestamp: "2026-10-01T00:00:00Z", message: { content: "same" } });
    fs.writeFileSync(path.join(d, "proj", "gggg0007.jsonl"), Array.from({ length: 200_000 }, () => one).join("\n"));
    expect(readRepoSessions(d, "/repo", new Date(0)).lines).toHaveLength(200_000);
  });

  it("records the isMeta flag", () => {
    const d = dir();
    fs.writeFileSync(path.join(d, "proj", "ffff0006.jsonl"), [line({ type: "user", cwd: "/repo", isMeta: true, message: { content: "a" } }), line({ type: "user", cwd: "/repo", message: { content: "b" } })].join("\n"));
    expect(readRepoSessions(d, "/repo", new Date(0)).lines.map((l) => l.meta)).toEqual([true, false]);
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
      [line({ type: "assistant", cwd: "/repo", message: { content: [{ type: "text", text: `first\nline ${secret}` }] } }), line({ type: "user", message: { content: "" } }), "garbage", line({ type: "user", message: { content: "x".repeat(400) } })].join("\n"),
    );
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#1")).toBe("first line [REDACTED:aws-access-key]");
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#4")).toBe("x".repeat(300));
    const profile = makeScrubber(compileExtraPatterns([{ kind: "ticket", regex: "first" }]));
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#1", 300, profile)).toBe("[REDACTED:ticket] line [REDACTED:aws-access-key]");
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#2")).toBeNull();
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#3")).toBeNull();
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#99")).toBeNull();
    expect(excerptFor(d, "/repo", "transcript:deadbeef#1")).toBeNull();
    expect(excerptFor(d, "/repo", "pr:12")).toBeNull();
    expect(excerptFor(path.join(d, "missing"), "/repo", "transcript:5e55a1d0#1")).toBeNull();
    // Refs resolve only through this repo's sessions: another repo's line with a matching prefix is never printed.
    fs.writeFileSync(path.join(d, "proj", "5e55a1d0-2222.jsonl"), line({ type: "user", cwd: "/elsewhere", message: { content: "private line" } }));
    expect(excerptFor(d, "/elsewhere", "transcript:5e55a1d0#1")).toBe("private line");
    expect(excerptFor(d, "/repo", "transcript:5e55a1d0#1")).toBe("first line [REDACTED:aws-access-key]");
    const other = tempDir();
    fs.mkdirSync(path.join(other, "proj"));
    fs.writeFileSync(path.join(other, "proj", "5e55a1d0-3333.jsonl"), line({ type: "user", cwd: "/elsewhere", message: { content: "private line" } }));
    expect(excerptFor(other, "/repo", "transcript:5e55a1d0#1")).toBeNull();
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

describe("resolveRefs (privacy B1)", () => {
  it("returns the full scrubbed text of every resolvable transcript ref in one scan, tool results included", () => {
    const d = tempDir();
    fs.mkdirSync(path.join(d, "proj"));
    fs.writeFileSync(
      path.join(d, "proj", "5e55a1d0-1111.jsonl"),
      [
        line({ type: "user", cwd: "/repo", message: { content: "first line\twith a tab" } }),
        line({ type: "user", cwd: "/repo", message: { content: [{ type: "tool_result", content: "x".repeat(500) }] } }),
      ].join("\n"),
    );
    const session = "5e55a1d0";
    const m = resolveRefs(d, "/repo", [`transcript:${session}#1`, `transcript:${session}#2`, "pr:12", "transcript:zzzzzzzz#9"]);
    expect(m.get(`transcript:${session}#1`)).toBe("first line with a tab");
    expect(m.get(`transcript:${session}#2`)).toHaveLength(500); // not cut to the 300-character excerpt
    expect(m.has("pr:12")).toBe(false);
    expect(m.size).toBe(2);
  });

  it("returns an empty map for no refs without reading anything", () => {
    expect(resolveRefs("/nonexistent", "/nonexistent", []).size).toBe(0);
  });
});

describe("sessionTexts (privacy B1, verbatim gate)", () => {
  it("returns every line of the asked sessions of this repo, scrubbed, by line number, in one scan", () => {
    const d = tempDir();
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    fs.mkdirSync(path.join(d, "proj"));
    fs.writeFileSync(
      path.join(d, "proj", "5e55a1d0-1111.jsonl"),
      [
        line({ type: "user", cwd: "/repo", message: { content: "first line\twith a tab" } }),
        line({ type: "assistant", cwd: "/repo", message: { content: [{ type: "tool_use", name: "Edit" }] } }),
        line({ type: "user", cwd: "/repo", message: { content: [{ type: "tool_result", content: `key ${secret}` }] } }),
      ].join("\n"),
    );
    fs.writeFileSync(path.join(d, "proj", "0ddba11e-2222.jsonl"), line({ type: "user", cwd: "/repo", message: { content: "a session nobody asked for" } }));
    fs.writeFileSync(path.join(d, "proj", "5e55a1d0-3333.jsonl"), line({ type: "user", cwd: "/elsewhere", message: { content: "another repo's session" } }));
    const m = sessionTexts(d, "/repo", ["5e55a1d0", "zzzzzzzz"]);
    const file = path.join(d, "proj", "5e55a1d0-1111.jsonl");
    expect([...m.keys()]).toEqual([file]); // unknown prefixes are absent; other sessions aren't read in
    expect(m.get(file)!.session).toBe("5e55a1d0");
    const lines = m.get(file)!.lines;
    expect([...lines.keys()]).toEqual([1, 2, 3]); // a line with no text is still a line of the session
    expect(lines.get(1)).toEqual({ text: "first line with a tab", turn: "first line with a tab" });
    expect(lines.get(2)).toEqual({ text: "", turn: "" });
    expect(lines.get(3)!.text).toMatch(/^key /);
    expect(lines.get(3)!.text).not.toContain(secret);
    expect(lines.get(3)!.turn).toBe(""); // a tool result is never a conversation turn
    expect(JSON.stringify([...lines.values()])).not.toMatch(/another repo|nobody asked/);
  });

  it("keys by the session file, so two files sharing an 8-character prefix keep their own lines (kieran-typescript-reviewer-3)", () => {
    const d = tempDir();
    fs.mkdirSync(path.join(d, "proj"));
    fs.writeFileSync(path.join(d, "proj", "abcd1234-0001.jsonl"), line({ type: "user", cwd: "/repo", message: { content: "line one of the first file" } }));
    fs.writeFileSync(path.join(d, "proj", "abcd1234-0002.jsonl"), line({ type: "user", cwd: "/repo", message: { content: "line one of the second file" } }));
    const m = sessionTexts(d, "/repo", ["abcd1234"]);
    expect([...m.values()].map((f) => f.session)).toEqual(["abcd1234", "abcd1234"]);
    expect([...m.values()].map((f) => f.lines.get(1)!.turn).sort()).toEqual(["line one of the first file", "line one of the second file"]);
  });

  it("keeps a resumed session's copied lines: nothing is deduped against an earlier file (security-sentinel-2)", () => {
    const d = tempDir();
    fs.mkdirSync(path.join(d, "proj"));
    const copied = line({ type: "user", timestamp: "2026-10-08T10:00:00Z", cwd: "/repo", message: { content: "migrate the zorb ledger sync" } });
    fs.writeFileSync(path.join(d, "proj", "aaaa1111-0001.jsonl"), copied);
    fs.writeFileSync(path.join(d, "proj", "bbbb2222-0002.jsonl"), [copied, line({ type: "user", timestamp: "2026-10-09T10:00:00Z", cwd: "/repo", message: { content: "and then the next step" } })].join("\n"));
    const m = sessionTexts(d, "/repo", ["bbbb2222"]);
    expect([...m.get(path.join(d, "proj", "bbbb2222-0002.jsonl"))!.lines.values()].map((l) => l.turn)).toEqual(["migrate the zorb ledger sync", "and then the next step"]);
    // reflect and correct still read each turn once
    expect(readRepoSessions(d, "/repo", new Date(0)).lines.filter((l) => l.session === "bbbb2222").map((l) => l.n)).toEqual([2]);
  });

  it("gives a turn only for human and assistant text: no tool results, isMeta lines, tag-led harness blocks or interruption markers (kieran-typescript-reviewer-1)", () => {
    const d = tempDir();
    fs.mkdirSync(path.join(d, "proj"));
    fs.writeFileSync(
      path.join(d, "proj", "c0ffee00-1111.jsonl"),
      [
        line({ type: "user", cwd: "/repo", message: { content: [{ type: "text", text: "[Request interrupted by user]" }] } }),
        line({ type: "user", cwd: "/repo", message: { content: [{ type: "text", text: "[Request interrupted by user for tool use]" }] } }),
        line({ type: "user", cwd: "/repo", isMeta: true, message: { content: "Caveat: The messages below were generated by the user while running local commands." } }),
        line({ type: "user", cwd: "/repo", message: { content: [{ type: "text", text: "<system-reminder>be brief</system-reminder>" }, { type: "text", text: "fix the zorb reconcile cron" }] } }),
        line({ type: "user", cwd: "/repo", message: { content: "<command-name>/clear</command-name>" } }),
        line({ type: "assistant", cwd: "/repo", message: { content: [{ type: "text", text: "I will retry the check" }, { type: "tool_use", name: "Bash", input: { command: "npm test" } }] } }),
        line({ type: "user", cwd: "/repo", message: { content: [{ type: "tool_result", content: "Tool ran without output or errors" }] } }),
        line({ type: "summary", cwd: "/repo", content: "a summary entry of five words" }),
      ].join("\n"),
    );
    const lines = [...[...sessionTexts(d, "/repo", ["c0ffee00"]).values()][0].lines.values()];
    expect(lines.map((l) => l.turn)).toEqual(["", "", "", "fix the zorb reconcile cron", "", "I will retry the check", "", ""]);
    expect(lines.map((l) => l.text)).toEqual([
      "[Request interrupted by user]", "[Request interrupted by user for tool use]", "Caveat: The messages below were generated by the user while running local commands.",
      "<system-reminder>be brief</system-reminder> fix the zorb reconcile cron", "<command-name>/clear</command-name>", "I will retry the check", "Tool ran without output or errors", "a summary entry of five words",
    ]);
  });

  it("returns an empty map for no sessions without reading anything", () => {
    expect(sessionTexts("/nonexistent", "/nonexistent", []).size).toBe(0);
  });
});
