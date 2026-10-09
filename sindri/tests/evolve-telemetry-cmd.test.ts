import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { init } from "../src/evolve/cmd/registry.js";
import { telemetry } from "../src/evolve/cmd/telemetry.js";
import { SindriError } from "../src/errors.js";
import { saveProposal, setStatus, type Proposal } from "../src/evolve/proposals.js";
import { scriptedEvolveIo, evolveFixture, type EvolveFixture } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "config/lib/tests/done-gate.test.sh": "#!/bin/sh\n" };
const pad = (n: number): string => String(n).padStart(2, "0");

function writeFires(fx: EvolveFixture, count: number, name = "5e55a1d0-1111-4000-8000-000000000003"): void {
  const lines = Array.from({ length: count }, (_, n) => [
    { type: "assistant", timestamp: `2026-10-05T10:${pad(n)}:00Z`, cwd: fx.repo, message: { role: "assistant", content: [{ type: "text", text: `Turn ${n}: all done.` }] } },
    { type: "user", isMeta: true, timestamp: `2026-10-05T10:${pad(n)}:01Z`, cwd: fx.repo, message: { role: "user", content: `Stop hook feedback:\n[${fx.repo}/config/hooks/done-gate.sh # aw:done-gate]: Claiming done with no evidence mentioned. Show the proof.` } },
  ]).flat();
  fs.writeFileSync(path.join(fx.transcripts, `${name}.jsonl`), `${lines.map((l) => JSON.stringify(l)).join("\n")}\n`);
}

// The adjudicator calls every fire unwarranted.
const allUnwarranted = (call: { input: string }) => ({
  results: [...call.input.matchAll(/<untrusted id="([^"]+)"/g)].map((m) => ({ ref: m[1], warranted: false, reason: "nothing was claimed" })),
});

async function ready(extraYaml = "") {
  const fx = await evolveFixture({ files: FILES, extraYaml, io: scriptedEvolveIo(allUnwarranted) });
  await init([], fx.ctx);
  return fx;
}

describe("sindri evolve telemetry", () => {
  it("adjudicates new fires, reports a rate per hook, and opens one hook-fix proposal", async () => {
    const fx = await ready();
    writeFires(fx, 12);
    const r = await telemetry([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toMatch(/^done-gate: 12 fire\(s\) since 2026-10-01; 12 labelled sample\(s\), 12 unwarranted; FP rate 1\.00 \(lower bound 0\.76\)\n {2}opened proposal [0-9a-z]{26} \(approval\)\nNext: sindri evolve show [0-9a-z]{26}\n$/);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM hook_samples").get()).toEqual({ c: 12 });
    expect(fx.ctx.db.prepare("SELECT kind, tier, status, artifact_id FROM proposals").all()).toEqual([{ kind: "hook-fix", tier: "approval", status: "proposed", artifact_id: "hook:done-gate" }]);
    // A rerun re-adjudicates nothing and doesn't propose the same fix twice.
    const calls = (fx.io as ReturnType<typeof scriptedEvolveIo>).calls.length;
    const again = await telemetry([], fx.ctx);
    expect((fx.io as ReturnType<typeof scriptedEvolveIo>).calls.length).toBe(calls);
    expect(again.stdout).toMatch(/already proposed \([0-9a-z]{26}\)/);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals").get()).toEqual({ c: 1 });
    const json = JSON.parse((await telemetry(["--json"], fx.ctx)).stdout) as { hooks: { hook: string; fires: number }[] };
    expect(json.hooks).toEqual([expect.objectContaining({ hook: "done-gate", fires: 12, labelled: 12, unwarranted: 12 })]);
    fx.close();
  });

  it("says there aren't enough samples yet, and opens nothing", async () => {
    const fx = await ready();
    writeFires(fx, 3);
    const r = await telemetry(["--per-hook", "2"], fx.ctx);
    expect(r.stdout).toBe("done-gate: 3 fire(s) since 2026-10-01; 2 labelled sample(s), 2 unwarranted; not enough samples yet (2/10)\nNext: sindri evolve proposals\n");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals").get()).toEqual({ c: 0 });
    fx.close();
  });

  it("opens nothing when the adjudicator mostly finds the fires warranted", async () => {
    const fx = await evolveFixture({
      files: FILES,
      io: scriptedEvolveIo((call) => ({ results: [...call.input.matchAll(/<untrusted id="([^"]+)"/g)].map((m, i) => ({ ref: m[1], warranted: i % 5 !== 0, reason: "r" })) })),
    });
    await init([], fx.ctx);
    writeFires(fx, 12);
    const r = await telemetry([], fx.ctx);
    expect(r.stdout).toMatch(/FP rate 0\.25 \(lower bound 0\.0\d\)/);
    expect(r.stdout).not.toContain("opened proposal");
    fx.close();
  });

  it("reports a hook that isn't registered, instead of proposing against nothing", async () => {
    const fx = await evolveFixture({ files: {}, io: scriptedEvolveIo(allUnwarranted) });
    writeFires(fx, 12);
    const r = await telemetry([], fx.ctx);
    expect(r.stdout).toContain("no registered artifact hook:done-gate; run sindri evolve init");
    fx.close();
  });

  it("stops with a partial result when the token budget runs out", async () => {
    const fx = await ready("evolve:\n  maxTokensPerJob: 1\n");
    writeFires(fx, 12);
    const r = await telemetry([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Stopped early: token budget exhausted; 2 fire(s) were not adjudicated.");
    expect(r.stdout).toContain("Next: raise evolve.maxTokensPerJob in the profile (then sindri profile approve), or rerun later");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM hook_samples").get()).toEqual({ c: 10 });
    fx.close();
  });

  it("explains the empty and misconfigured cases", async () => {
    const fx = await ready();
    expect((await telemetry([], fx.ctx)).stdout).toBe(`No hook fires found in sessions of ${fx.repo} since 2026-10-01.\nNext: sindri evolve telemetry --since 30d\n`);
    fs.rmSync(fx.transcripts, { recursive: true });
    const gone = await telemetry([], fx.ctx);
    expect(gone.exitCode).toBe(1);
    expect(gone.stdout).toBe(`No transcripts directory at ${fx.transcripts}.\nNext: set sources.transcripts.dir in the profile, then sindri profile approve\n`);
    await expect(telemetry(["--since", "never"], fx.ctx)).rejects.toThrow(/--since must look like 7d/);
    await expect(telemetry(["--per-hook", "0"], fx.ctx)).rejects.toThrow(/--per-hook must be a positive whole number/);
    fx.close();
  });

  it("stores a fire the adjudicator labelled nothing for with no label, and counts it in no rate", async () => {
    const fx = await evolveFixture({ files: FILES, io: scriptedEvolveIo(() => ({ results: [] })) });
    await init([], fx.ctx);
    writeFires(fx, 12);
    const r = await telemetry([], fx.ctx);
    expect(r.stdout).toContain("done-gate: 12 fire(s) since 2026-10-01; 0 labelled sample(s), 0 unwarranted; not enough samples yet (0/10)");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM hook_samples WHERE warranted IS NULL").get()).toEqual({ c: 12 });
    fx.close();
  });

  it("reports a hook whose fires were never sampled because the budget ran out first", async () => {
    const fx = await ready("evolve:\n  maxTokensPerJob: 1\n");
    writeFires(fx, 10);
    const other = { type: "user", isMeta: true, timestamp: "2026-10-06T10:00:00Z", cwd: fx.repo, message: { role: "user", content: `Stop hook feedback:\n[${fx.repo}/config/hooks/other-gate.sh # aw:other-gate]: Blocked.` } };
    fs.writeFileSync(path.join(fx.transcripts, "bbbb2222-1111-4000-8000-000000000004.jsonl"), `${JSON.stringify(other)}\n`);
    const r = await telemetry([], fx.ctx);
    expect(r.stdout).toContain("other-gate: 1 fire(s) since 2026-10-01; 0 labelled sample(s), 0 unwarranted; not enough samples yet (0/10)");
    expect(r.stdout).toContain("Stopped early: token budget exhausted; 1 fire(s) were not adjudicated.");
    fx.close();
  });

  it("samples a fire once even when a resumed copy in an earlier-sorting file gives it a new ref", async () => {
    const fx = await ready();
    writeFires(fx, 12, "ffff0000-1111-4000-8000-000000000003");
    await telemetry([], fx.ctx);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM hook_samples").get()).toEqual({ c: 12 });
    const calls = (fx.io as ReturnType<typeof scriptedEvolveIo>).calls.length;
    // A fork holding only the first turn sorts first (shorter span, earlier name), so its fire is a new ref.
    const first = fs.readFileSync(path.join(fx.transcripts, "ffff0000-1111-4000-8000-000000000003.jsonl"), "utf8").split("\n").slice(0, 2).join("\n");
    fs.writeFileSync(path.join(fx.transcripts, "00000000-1111-4000-8000-000000000009.jsonl"), `${first}\n`);
    await telemetry([], fx.ctx);
    expect((fx.io as ReturnType<typeof scriptedEvolveIo>).calls.length).toBe(calls);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM hook_samples").get()).toEqual({ c: 12 });
    fx.close();
  });

  it("counts only samples after the latest merged hook-fix, and says so", async () => {
    const fx = await ready();
    writeFires(fx, 12);
    await telemetry([], fx.ctx);
    const art = "hook:done-gate";
    const fix: Proposal = { artifact: art, kind: "hook-fix", title: "Reduce false positives in the done-gate hook", rationale: "r", evidence: [], change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } };
    const saved = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, { ...fix, title: "An older merged fix" }, "t", "approval", epoch, new Date("2026-10-07T00:00:00Z")));
    if (saved.kind !== "saved") throw new Error("expected a saved proposal");
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, saved.id, "merged", epoch, new Date("2026-10-07T00:00:00Z")));
    const before = fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals").get();
    const out = (await telemetry([], fx.ctx)).stdout;
    expect(out).toContain("done-gate: 12 fire(s) since 2026-10-01; 0 labelled sample(s) after the merged fix of 2026-10-07, 0 unwarranted; not enough samples yet (0/10)");
    expect(out).not.toContain("opened proposal");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals").get()).toEqual(before);
    fx.close();
  });

  it("reports malformed adjudicator items as dropped without stopping the run", async () => {
    const fx = await evolveFixture({
      files: FILES,
      io: scriptedEvolveIo((call) => ({ results: [...call.input.matchAll(/<untrusted id="([^"]+)"/g)].map((m, i) => ({ ref: m[1], warranted: false, reason: i === 0 ? "x".repeat(600) : "ok" })) })),
    });
    await init([], fx.ctx);
    writeFires(fx, 3);
    const r = await telemetry([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("1 adjudicator answer(s) were malformed and dropped; those fires have no label.");
    fx.close();
  });

  it("suggests a rerun, not a bigger budget, after a model failure", async () => {
    const fx = await evolveFixture({ files: FILES, io: scriptedEvolveIo(() => { throw new SindriError("SND-SCOPE-002", "model job timed out"); }) });
    await init([], fx.ctx);
    writeFires(fx, 3);
    const r = await telemetry([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Stopped early: model job timed out; 3 fire(s) were not adjudicated.");
    expect(r.stdout).toContain("Next: rerun sindri evolve telemetry");
    expect(r.stdout).not.toContain("maxTokensPerJob");
    fx.close();
  });
});
