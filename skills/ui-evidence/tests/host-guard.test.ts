import { describe, expect, it } from "vitest";

import { DEFAULT_HOST, findWriteSteps, guardWrites, provenanceFor, readCredentials, redact, resolveHost } from "../src/host-guard.js";
import type { ScriptStep } from "../src/script-schema.js";

const click = (target: string): ScriptStep => ({ action: "click", target, expectedState: { kind: "testid-visible", testId: target } });

describe("resolveHost", () => {
  it("defaults to localhost:3000 with no flags", () => {
    expect(resolveHost({})).toEqual({ ok: true, host: DEFAULT_HOST, kind: "local" });
  });
  it("accepts a pr-<n>.vitalize.build preview only with --allow-preview-host", () => {
    expect(resolveHost({ host: "https://pr-17450.vitalize.build", allowPreviewHost: true })).toEqual({ ok: true, host: "https://pr-17450.vitalize.build", kind: "preview" });
    expect(resolveHost({ host: "https://pr-17450.vitalize.build/" , allowPreviewHost: true })).toMatchObject({ ok: true });
    expect(resolveHost({ host: "https://pr-17450.vitalize.build" })).toEqual({ ok: false, error: "--host needs --allow-preview-host" });
  });
  it.each([
    ["https://app.vitalize.build", "not a pr-"],
    ["https://dev.vitalize.build", "not a pr-"],
    ["https://vitalize.build", "not a pr-"],
    ["https://pr-1.vitalize.build.evil.com", "not a pr-"],
    ["https://pr-abc.vitalize.build", "not a pr-"],
    ["https://app.vitalize.care", "not a pr-"],
    ["http://pr-1.vitalize.build", "must be https"],
    ["https://user:pw@pr-1.vitalize.build", "credentials"],
    ["https://pr-1.vitalize.build:8443", "bare origin"],
    ["https://pr-1.vitalize.build/admin", "bare origin"],
    ["https://pr-1.vitalize.build/?x=1", "bare origin"],
    ["https://pr-1.vitalize.build/#x", "bare origin"],
    ["not a url", "not a URL"],
    ["localhost:3000", "must be https"],
  ])("refuses %s", (host, why) => {
    const r = resolveHost({ host, allowPreviewHost: true });
    expect(r.ok).toBe(false);
    expect(r.ok ? "" : r.error).toContain(why);
  });
});

describe("readCredentials", () => {
  it("returns both values from the env", () => {
    expect(readCredentials({ E: "a@b.c", P: "pw" }, "E", "P")).toEqual({ email: "a@b.c", password: "pw" });
  });
  it("names only the missing variables, never a value", () => {
    const r = readCredentials({ E: "secret@x.y" }, "E", "P");
    expect(r).toEqual({ error: "missing credentials in environment: P" });
    expect(readCredentials({}, "E", "P")).toEqual({ error: "missing credentials in environment: E, P" });
    expect(readCredentials({ E: "", P: "" }, "E", "P")).toEqual({ error: "missing credentials in environment: E, P" });
  });
});

describe("redact", () => {
  it("removes every occurrence of every secret and ignores empty ones", () => {
    expect(redact("fill a@b.c then pw, pw again", ["a@b.c", "pw", ""])).toBe("fill [redacted] then [redacted], [redacted] again");
  });
});

describe("write guard", () => {
  it("flags click steps that read like save/submit/update controls, including camelCase", () => {
    const steps = [click("hospital-day-starts-at"), click("update-hospital-button"), click("saveChanges"), click("coverage-pattern-option-8/4/4/8"), { ...click("form"), action: "fill" as const }, { ...click("save"), action: "fill" as const }];
    expect(findWriteSteps(steps)).toEqual([
      { index: 1, target: "update-hospital-button" },
      { index: 2, target: "saveChanges" },
    ]);
  });
  it("does not trip on words that merely contain a write word", () => {
    expect(findWriteSteps([click("preview-manage-staffing-toggle"), click("postal-code-help"), click("address-line")])).toEqual([]);
  });
  it("refuses with the offending steps unless writes are allowed", () => {
    const steps = [click("a"), click("submit-form")];
    expect(guardWrites(steps, false)).toEqual({ ok: false, error: "refusing write-looking steps without --allow-writes: #2 click submit-form" });
    expect(guardWrites(steps, true)).toEqual({ ok: true });
    expect(guardWrites([click("a")], false)).toEqual({ ok: true });
  });
});

describe("provenanceFor", () => {
  it("treats a preview as scrubbed and a local run as its DB check", () => {
    expect(provenanceFor("preview", "unknown")).toBe("scrubbed");
    expect(provenanceFor("local", "seeded")).toBe("seeded");
    expect(provenanceFor("local", "unknown")).toBe("unknown");
  });
});
