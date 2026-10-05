import { describe, expect, it } from "vitest";

import { parseDesignManifest } from "../src/parity-schema.js";

const frame = (name: string) => ({ name, designPng: "d.png", designRegion: { x: 1, y: 2, w: 3, h: 4 }, anchor: { testId: "card" } });
const base = { route: "/x", ready: { text: "Hours" }, frames: [frame("A")] };

describe("parseDesignManifest", () => {
  it("fills Figma-frame defaults", () => {
    const m = parseDesignManifest(base);
    expect(m).toMatchObject({ viewport: { w: 1512, h: 982 }, settleMs: 600, threshold: 0.1, includeAA: false });
  });
  it("accepts steps, form values, box expectations, known differences and login", () => {
    const m = parseDesignManifest({
      ...base,
      login: { emailEnv: "ADMIN_EMAIL", passwordEnv: "ADMIN_PASSWORD", emailSelector: "input[name=contact]", passwordSelector: "input[name=password]", submitSelector: "button" },
      frames: [{ ...frame("A"), formValues: [{ testId: "n", value: "v" }, { css: "input[name=x]", value: "y" }], expectBoxes: [{ target: { text: "t" }, expected: { w: 1 } }], knownDifferences: ["copy"], steps: [{ action: "click", target: "t", expectedState: { kind: "testid-visible", testId: "t" } }] }],
    });
    expect("error" in m).toBe(false);
  });
  it.each([
    ["no frames", { ...base, frames: [] }],
    ["route without slash", { ...base, route: "x" }],
    ["no ready element", { route: "/x", frames: [frame("A")] }],
    ["unsafe frame name", { ...base, frames: [frame("../A")] }],
    ["empty expected box", { ...base, frames: [{ ...frame("A"), expectBoxes: [{ target: { testId: "t" }, expected: {} }] }] }],
    ["selector with both keys", { ...base, ready: { testId: "a", text: "b" } }],
    ["env var name that is a literal secret", { ...base, login: { emailEnv: "me@x.com", passwordEnv: "P", emailSelector: "a", passwordSelector: "b", submitSelector: "c" } }],
  ])("rejects %s", (_n, raw) => {
    expect(parseDesignManifest(raw)).toHaveProperty("error");
  });
  it.each([
    ["a misspelt frame key (expectBox)", { ...base, frames: [{ ...frame("A"), expectBox: [{ target: { testId: "t" }, expected: { w: 1 } }] }] }],
    ["a misspelt manifest key", { ...base, settleMS: 5 }],
    ["a misspelt box expectation key", { ...base, frames: [{ ...frame("A"), expectBoxes: [{ target: { testId: "t" }, expected: { w: 1 }, tolerence: 2 }] }] }],
    ["a misspelt expected axis", { ...base, frames: [{ ...frame("A"), expectBoxes: [{ target: { testId: "t" }, expected: { width: 1 } }] }] }],
    ["a misspelt design region key", { ...base, frames: [{ ...frame("A"), designRegion: { x: 1, y: 2, w: 3, h: 4, z: 1 } }] }],
    ["a misspelt anchorOffset key", { ...base, frames: [{ ...frame("A"), anchorOffset: { x: 1, y: 2, z: 3 } }] }],
    ["a misspelt viewport key", { ...base, viewport: { w: 1, h: 1, dpr: 2 } }],
    ["a misspelt login key", { ...base, login: { emailEnv: "E", passwordEnv: "P", emailSelector: "a", passwordSelector: "b", submitSelector: "c", loginpath: "/x" } }],
  ])("rejects %s instead of silently dropping the check", (_n, raw) => {
    expect(parseDesignManifest(raw)).toHaveProperty("error");
  });
  it("rejects duplicate frame names, which would overwrite each other's PNGs", () => {
    expect(parseDesignManifest({ ...base, frames: [frame("A"), frame("A")] })).toEqual({ error: 'duplicate frame name "A"' });
  });
});
