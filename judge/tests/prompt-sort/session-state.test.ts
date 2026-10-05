// judge/tests/prompt-sort/session-state.test.ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { SESSION_ID_RE, emptyState, readSortState, sortStatePath, writeSortState } from "../../src/prompt-sort/session-state.js";

const tmp = () => fs.mkdtempSync(path.join(os.tmpdir(), "sortstate-"));

describe("session state (RF-4)", () => {
  it("builds the path under judge/sessions and rejects ids that could escape it", () => {
    const dir = tmp();
    expect(sortStatePath(dir, "abc-123_X.y")).toBe(path.join(dir, "judge", "sessions", "abc-123_X.y.sort.json"));
    for (const bad of ["", "../x", "a/b", "a b", "x".repeat(81), ".."]) expect(sortStatePath(dir, bad)).toBeNull();
    expect(SESSION_ID_RE.test("s1")).toBe(true);
  });

  it("round-trips state, creating the directory, and leaves no temp file behind", () => {
    const dir = tmp();
    const file = sortStatePath(dir, "s1") as string;
    writeSortState(file, { prompts: 2, lastFired: { brief: 2 }, requirements: { uiEvidence: true } });
    expect(readSortState(file)).toEqual({ prompts: 2, lastFired: { brief: 2 }, requirements: { uiEvidence: true } });
    expect(fs.readdirSync(path.dirname(file))).toEqual(["s1.sort.json"]);
  });

  it("returns an empty state for a missing, corrupt or wrong-shaped file", () => {
    const dir = tmp();
    const file = path.join(dir, "s.sort.json");
    expect(readSortState(file)).toEqual(emptyState());
    fs.writeFileSync(file, "not json");
    expect(readSortState(file)).toEqual(emptyState());
    fs.writeFileSync(file, JSON.stringify({ prompts: "many", lastFired: [], requirements: 3 }));
    expect(readSortState(file)).toEqual(emptyState());
    fs.writeFileSync(file, JSON.stringify({ prompts: 4, lastFired: { brief: 2, bogus: 1, bugfix: "x" }, requirements: { uiEvidence: true } }));
    expect(readSortState(file)).toEqual({ prompts: 4, lastFired: { brief: 2 }, requirements: { uiEvidence: true } });
    fs.writeFileSync(file, JSON.stringify({ prompts: 1, lastFired: { bugfix: 1 }, requirements: { uiEvidence: false } }));
    expect(readSortState(file)).toEqual({ prompts: 1, lastFired: { bugfix: 1 }, requirements: {} });
  });
});
