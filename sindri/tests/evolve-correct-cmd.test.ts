import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { correctCommand } from "../src/evolve/cmd/correct.js";
import { init } from "../src/evolve/cmd/registry.js";
import type { ModelCall } from "../src/scope/model.js";
import { evolveFixture, scriptedEvolveIo, type EvolveFixture } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "skills/review/SKILL.md": "x\n" };
const answer = { proposal: { artifact: "skill:review", kind: "skill-edit", title: "Lint for test-vs-hook edits", rationale: "level: lint. Would have caught the first correction.", evidence: ["transcript:5e55a1d0#1"], change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" } } };

const refsOf = (input: string): string[] => [...input.matchAll(/<untrusted id="([^"]+)" kind="human"/g)].map((m) => m[1]);
// Every label call gets the same labels for all its turns; the proposal calls get `proposal`.
const script = (labels: string[] = ["wrong_approach_process"], proposal: unknown = answer) => (call: ModelCall<unknown>): unknown =>
  call.role === "label" ? { results: refsOf(call.input).map((ref) => ({ ref, labels })) } : proposal;

function writeCorrections(fx: EvolveFixture): void {
  const mk = (name: string, day: string, text: string) =>
    fs.writeFileSync(path.join(fx.transcripts, name), `${JSON.stringify({ type: "user", cwd: fx.repo, timestamp: `2026-10-0${day}T10:00:00Z`, message: { content: text } })}\n`);
  mk("5e55a1d0-a.jsonl", "5", "you edited the test file instead of the hook file again");
  mk("6f66b2e1-b.jsonl", "6", "wrong file: edit the hook file, not the test file");
}

async function ready(fn: (call: ModelCall<unknown>) => unknown = script(), extraYaml = "") {
  const io = scriptedEvolveIo(fn);
  const fx = await evolveFixture({ files: FILES, io, extraYaml });
  await init([], fx.ctx);
  writeCorrections(fx);
  return { fx, io };
}

const LABELED = (n: number, d: number, p: number, errors = 0): string => `labeled ${n} turns: ${d} design, ${p} process, 0 restate, 0 scope (${errors} label errors)`;

describe("sindri evolve correct", () => {
  it("labels the turns, clusters the corrections, saves the proposals for the ISO week, and doesn't repeat the week", async () => {
    const { fx, io } = await ready();
    const r = await correctCommand([], fx.ctx);
    expect(r.exitCode).toBe(0);
    const row = fx.ctx.db.prepare("SELECT id, source, tier FROM proposals").get() as { id: string; source: string; tier: string };
    expect(row).toMatchObject({ source: "correct:2026-W41", tier: "code" });
    expect(r.stdout).toBe(`Correct: ${LABELED(2, 0, 2)}; 1 repeated-correction class, 1 proposal (1 code).\n  ${row.id}  code      Lint for test-vs-hook edits\nNext: sindri evolve show ${row.id}\n`);
    expect(io.calls.map((c) => [c.role, c.model])).toEqual([["label", "sonnet"], ["draft", "opus"]]);
    expect(io.calls[1].input).toContain("Class label: wrong_approach_process");
    expect(JSON.parse((await correctCommand(["--json"], fx.ctx)).stdout)).toMatchObject({ alreadyRan: true });
    const again = await correctCommand([], fx.ctx);
    expect(again.stdout).toBe(`Already ran for 2026-W41: ${row.id}.\nNext: sindri evolve proposals\n`);
    fx.close();
  });

  it("reports several classes with different labels, each with its own proposal", async () => {
    const second = { proposal: { ...answer.proposal, artifact: "hook:done-gate", kind: "code", title: "Run it in CI", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } } };
    const io = scriptedEvolveIo((call) => {
      if (call.role === "label") return { results: refsOf(call.input).map((ref) => ({ ref, labels: call.input.includes(`id="${ref}" kind="human" edits-before="no">run it`) ? ["wrong_approach_design"] : ["wrong_approach_process"] })) };
      return call.input.includes("Class label: wrong_approach_design") ? second : answer;
    });
    const fx = await evolveFixture({ files: FILES, io });
    await init([], fx.ctx);
    writeCorrections(fx);
    const mk = (name: string, day: string, text: string) =>
      fs.writeFileSync(path.join(fx.transcripts, name), `${JSON.stringify({ type: "user", cwd: fx.repo, timestamp: `2026-10-0${day}T10:00:00Z`, message: { content: text } })}\n`);
    mk("8b88d4a3-d.jsonl", "4", "run it in CI, never locally");
    mk("9c99e5b4-e.jsonl", "3", "run it in CI, never locally please");
    const r = await correctCommand([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain(`Correct: ${LABELED(4, 2, 2)}; 2 repeated-correction classes, 2 proposals (`);
    expect(r.stdout).toContain("Lint for test-vs-hook edits");
    expect(r.stdout).toContain("Run it in CI");
    expect(io.calls.map((c) => c.role)).toEqual(["label", "draft", "draft"]);
    fx.close();
  });

  it("explains the empty cases: nothing to label, everything labeled none, label errors and the turn cap", async () => {
    const emptyIo = scriptedEvolveIo(script());
    const empty = await evolveFixture({ files: FILES, io: emptyIo });
    await init([], empty.ctx);
    const none = await correctCommand([], empty.ctx);
    expect(none.stdout).toBe(`No repeated corrections in sessions of ${empty.repo} since 2026-10-01: ${LABELED(0, 0, 0)}.\nNext: sindri evolve correct --since 30d\n`);
    expect(emptyIo.calls).toEqual([]);
    empty.close();

    const nones = await ready(script(["none"]));
    expect((await correctCommand([], nones.fx.ctx)).stdout).toBe(`No repeated corrections in sessions of ${nones.fx.repo} since 2026-10-01: ${LABELED(2, 0, 0)}.\nNext: sindri evolve correct --since 30d\n`);
    expect(nones.io.calls).toHaveLength(1);
    // A finished pass that found nothing is remembered for the week and window: a rerun spends no tokens, a wider window does.
    expect((await correctCommand([], nones.fx.ctx)).stdout).toBe("Already ran for 2026-W41 (nothing was proposed).\nNext: sindri evolve proposals\n");
    expect(nones.io.calls).toHaveLength(1);
    expect((await correctCommand(["--since", "30d"], nones.fx.ctx)).stdout).toBe(`No repeated corrections in sessions of ${nones.fx.repo} since 2026-09-08: ${LABELED(2, 0, 0)}.\nNext: sindri evolve correct --since 30d\n`);
    expect(nones.io.calls).toHaveLength(2);
    nones.fx.close();

    const broken = await ready((call) => (call.role === "label" ? { nope: 1 } : answer));
    const errors = await correctCommand([], broken.fx.ctx);
    expect(errors.exitCode).toBe(0);
    expect(errors.stdout).toBe(`No repeated corrections in sessions of ${broken.fx.repo} since 2026-10-01: ${LABELED(0, 0, 0, 1)}.\nNext: sindri evolve correct --since 30d\n`);
    expect(broken.io.calls).toHaveLength(2); // the bad batch is retried once
    broken.fx.close();

    const capped = await ready(script(), "evolve:\n  maxCorrectTurns: 1\n");
    const one = await correctCommand([], capped.fx.ctx);
    expect(one.stdout).toBe(`No repeated corrections in sessions of ${capped.fx.repo} since 2026-10-01: ${LABELED(1, 0, 1)}.\nNext: sindri evolve correct --since 30d\n`);
    expect(refsOf(capped.io.calls[0].input)).toEqual(["transcript:6f66b2e1#1"]); // the newest turn
    capped.fx.close();
  });

  it("escapes control characters in a dropped proposal's title, which is not schema-validated", async () => {
    const { fx } = await ready(script(["wrong_approach_process"], { proposal: { title: "Bad \u001b[2Kone", artifact: "nope" } }));
    const r = await correctCommand([], fx.ctx);
    expect(r.stdout).not.toMatch(/[\u0000-\u0008\u000B-\u001F\u007F-\u009F]/);
    expect(r.stdout).toContain("dropped: Bad \\u{001B}[2Kone (invalid proposal");
    fx.close();
  });

  it("remembers a week that proposed nothing", async () => {
    const { fx } = await ready(script(["wrong_approach_process"], { proposal: { title: "Bad one", artifact: "nope" } }));
    const bad = await correctCommand([], fx.ctx);
    expect(bad.stdout).toBe(`Correct: ${LABELED(2, 0, 2)}; 1 repeated-correction class, 0 proposals (0 code).\n  dropped: Bad one (invalid proposal: artifact: Invalid)\nNext: sindri evolve proposals\n`);
    expect((await correctCommand([], fx.ctx)).stdout).toBe("Already ran for 2026-W41 (nothing was proposed).\nNext: sindri evolve proposals\n");
    fx.close();
  });

  it("reports a partial result when a proposal call fails or the labeling budget runs out, and refuses a bad window or an empty registry", async () => {
    const { fx } = await ready(script(["wrong_approach_process"], { nope: 1 }));
    const r = await correctCommand([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Partial result: class 1: the model's answer didn't match the schema");
    expect(r.stdout).toContain("Next: rerun sindri evolve correct once the cause above is fixed");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit").get()).toEqual({ c: 0 });
    await expect(correctCommand(["--since", "soon"], fx.ctx)).rejects.toThrow(/--since must look like 7d/);
    const bare = await evolveFixture({ files: FILES, io: scriptedEvolveIo(script()) });
    await expect(correctCommand([], bare.ctx)).rejects.toThrow(/registry is empty/);
    fx.close();
    bare.close();

    // 22 turns make two batches; evolve.maxTokensPerJob of 2 is spent by the first label call.
    const tight = await ready(script(["none"]), "evolve:\n  maxTokensPerJob: 2\n");
    const stamp = (i: number) => `2026-10-04T10:00:${String(i).padStart(2, "0")}Z`;
    fs.writeFileSync(path.join(tight.fx.transcripts, "7a77c3f2-c.jsonl"), `${Array.from({ length: 20 }, (_, i) => JSON.stringify({ type: "user", cwd: tight.fx.repo, timestamp: stamp(i), message: { content: `turn number ${i}` } })).join("\n")}\n`);
    const cut = await correctCommand([], tight.fx.ctx);
    expect(cut.exitCode).toBe(1);
    expect(cut.stdout).toBe(
      `No repeated corrections in sessions of ${tight.fx.repo} since 2026-10-01: ${LABELED(20, 0, 0)}.\nPartial result: labeling stopped before batch 2: token budget exhausted\nNext: raise evolve.maxTokensPerJob in the profile (then sindri profile approve), or rerun later\n`,
    );
    expect(tight.io.calls).toHaveLength(1);
    tight.fx.close();
  });

  // Two classes (process: the hook-file corrections; design: the CI ones) and the fillers that make the batches.
  const CLASSES = { proposal: { ...answer.proposal, artifact: "hook:done-gate", kind: "code", title: "Run it in CI", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } } };
  const filler = (fx: EvolveFixture, name: string, day: string, n: number) =>
    fs.writeFileSync(path.join(fx.transcripts, name), `${Array.from({ length: n }, (_, i) => JSON.stringify({ type: "user", cwd: fx.repo, timestamp: `2026-10-${day}T10:00:${String(i).padStart(2, "0")}Z`, message: { content: `turn number ${i}` } })).join("\n")}\n`);
  const labelsBy = (call: ModelCall<unknown>) => ({ results: refsOf(call.input).map((ref) => ({ ref, labels: call.input.includes(`id="${ref}" kind="human" edits-before="no">turn number`) ? ["none"] : call.input.includes(`id="${ref}" kind="human" edits-before="no">run it`) ? ["wrong_approach_design"] : ["wrong_approach_process"] })) });
  const twoClasses = (fx: EvolveFixture) => {
    writeCorrections(fx);
    for (const [name, day, text] of [["8b88d4a3-d.jsonl", "4", "run it in CI, never locally"], ["9c99e5b4-e.jsonl", "3", "run it in CI, never locally please"]]) {
      fs.writeFileSync(path.join(fx.transcripts, name), `${JSON.stringify({ type: "user", cwd: fx.repo, timestamp: `2026-10-0${day}T10:00:00Z`, message: { content: text } })}\n`);
    }
  };

  it("lets a partial week be rerun: only the complete-run marker says already ran, and a complete run writes it", async () => {
    let failDesign = true;
    const io = scriptedEvolveIo((call) => {
      if (call.role === "label") return labelsBy(call);
      if (call.input.includes("Class label: wrong_approach_design")) return failDesign ? { nope: 1 } : CLASSES;
      return answer;
    });
    const fx = await evolveFixture({ files: FILES, io });
    await init([], fx.ctx);
    twoClasses(fx);
    const first = await correctCommand([], fx.ctx);
    expect(first.exitCode).toBe(1);
    expect(first.stdout).toContain("Partial result: class 1: the model's answer didn't match the schema");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals").get()).toEqual({ c: 1 }); // the process class was saved
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit").get()).toEqual({ c: 0 });
    failDesign = false;
    const calls = io.calls.length;
    const second = await correctCommand([], fx.ctx);
    expect(io.calls.length).toBeGreaterThan(calls); // the rerun asked the model again
    expect(second.exitCode).toBe(0);
    expect(second.stdout).toContain("Run it in CI");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals").get()).toEqual({ c: 2 }); // the missing class was added, the first was a duplicate
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit").get()).toEqual({ c: 1 });
    const third = await correctCommand([], fx.ctx);
    expect(third.stdout).toMatch(/^Already ran for 2026-W41: \S+, \S+\.\nNext: sindri evolve proposals\n$/);
    expect(io.calls.length).toBe(calls + 3); // the third run spent nothing
    fx.close();
  });

  it("does not remember the week when a labeling batch failed, even though a class was found", async () => {
    const io = scriptedEvolveIo((call) => (call.role === "label" ? (call.input.includes("turn number") ? { nope: 1 } : labelsBy(call)) : answer));
    const fx = await evolveFixture({ files: FILES, io });
    await init([], fx.ctx);
    writeCorrections(fx);
    filler(fx, "7a77c3f2-c.jsonl", "07", 20); // the newest 20 turns are the first batch, which fails
    const r = await correctCommand([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain(`Correct: ${LABELED(2, 0, 2, 1)}; 1 repeated-correction class, 1 proposal`);
    expect(r.stdout).toContain("Partial result: labeling: 1 failed batch(es)");
    expect(r.stdout).toContain("Next: rerun sindri evolve correct once the cause above is fixed");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit").get()).toEqual({ c: 0 });
    fx.close();
  });

  it("says to raise the budget, not to rerun, when the labeling budget ran out after a class was found", async () => {
    const io = scriptedEvolveIo((call) => (call.role === "label" ? labelsBy(call) : answer));
    const fx = await evolveFixture({ files: FILES, io, extraYaml: "evolve:\n  maxTokensPerJob: 2\n" });
    await init([], fx.ctx);
    writeCorrections(fx);
    filler(fx, "7a77c3f2-c.jsonl", "03", 20); // the two corrections are newest: batch 1 finds the class, batch 2 is never labeled
    const r = await correctCommand([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Partial result: labeling stopped before batch 2: token budget exhausted");
    expect(r.stdout).toContain("Next: raise evolve.maxTokensPerJob in the profile (then sindri profile approve), or rerun later");
    expect(r.stdout).not.toContain("Next: rerun sindri evolve correct once");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit").get()).toEqual({ c: 0 });
    fx.close();
  });

  it("never sends harness-injected turns (Stop hook feedback, skill bodies marked isMeta) to the labeler (I3)", async () => {
    const { fx, io } = await ready();
    const meta = (name: string, day: string, text: string) =>
      fs.writeFileSync(path.join(fx.transcripts, name), `${JSON.stringify({ type: "user", isMeta: true, cwd: fx.repo, timestamp: `2026-10-0${day}T11:00:00Z`, message: { content: text } })}\n`);
    meta("7a77c3f2-c.jsonl", "5", "Stop hook feedback:\n[/r/config/hooks/done-gate.sh # aw:done-gate]: Claiming done without tests");
    meta("7b77c3f3-d.jsonl", "6", "Base directory for this skill: /skills/review\n\n# Review skill body that is not a human turn");
    await correctCommand([], fx.ctx);
    const sent = io.calls.filter((c) => c.role === "label").map((c) => c.input).join("\n");
    expect(sent).toContain("you edited the test file");
    expect(sent).not.toContain("Stop hook feedback");
    expect(sent).not.toContain("Review skill body");
    fx.close();
  });

  it("keeps the audit marker key exact when a profile scrub pattern would match it", async () => {
    const { fx } = await ready(script(["none"]), "scrub:\n  extraPatterns:\n    - kind: marker\n      regex: \"W[0-9]+ 7d|2026-W41\"\n");
    await correctCommand([], fx.ctx);
    expect(fx.ctx.db.prepare("SELECT detail FROM evolve_audit WHERE verb = 'correct'").all()).toEqual([{ detail: "2026-W41 7d" }]);
    expect(JSON.parse((await correctCommand(["--json"], fx.ctx)).stdout)).toMatchObject({ alreadyRan: true });
    fx.close();
  });
});
