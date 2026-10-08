import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { failure, fromError, success } from "../src/output.js";

describe("output contract", () => {
  it("success prints text with a trailing newline, or JSON", () => {
    expect(success("hello", { a: 1 }, false)).toEqual({ exitCode: 0, stdout: "hello\n", stderr: "" });
    expect(success("hello\n", { a: 1 }, false).stdout).toBe("hello\n");
    expect(success("hello", { a: 1 }, true).stdout).toBe('{\n  "a": 1\n}\n');
    expect(success("x", null, false, 1).exitCode).toBe(1);
  });

  it("failure prints code, message and fix to stderr, or a JSON error to stdout", () => {
    const text = failure("SND-CLI-001", "unknown command: frob", false);
    expect(text.exitCode).toBe(2);
    expect(text.stdout).toBe("");
    expect(text.stderr).toBe("SND-CLI-001 unknown command: frob\n  fix: sindri help\n");
    const json = failure("SND-CLI-001", "unknown command: frob", true);
    expect(JSON.parse(json.stdout)).toEqual({
      ok: false,
      error: { code: "SND-CLI-001", message: "unknown command: frob", fix: "sindri help", details: [] },
    });
    expect(json.stderr).toBe("");
  });

  it("failure takes details, a per-instance fix and an exit code", () => {
    const r = failure("SND-CLI-001", "two problems", false, { details: ["a.yaml: x", "b.yaml: y"], fix: "edit a.yaml", exitCode: 1 });
    expect(r).toEqual({ exitCode: 1, stdout: "", stderr: "SND-CLI-001 two problems\n  a.yaml: x\n  b.yaml: y\n  fix: edit a.yaml\n" });
  });

  it("fromError renders a SindriError and rethrows anything else", () => {
    expect(fromError(new SindriError("SND-CLI-001", "bad"), false).stderr).toContain("SND-CLI-001 bad");
    const withMore = fromError(new SindriError("SND-CLI-001", "bad", { fix: "do x", details: ["d1"] }), false);
    expect(withMore.stderr).toBe("SND-CLI-001 bad\n  d1\n  fix: do x\n");
    expect(() => fromError(new Error("boom"), false)).toThrow("boom");
  });
});
