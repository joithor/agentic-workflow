import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { parsePlan } from "../src/adapters/plan-file/parse.js";
import { stateDir } from "../src/deps.js";
import { init } from "../src/evolve/cmd/registry.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { stage } from "../src/evolve/cmd/stage.js";
import type { EvolveIo } from "../src/evolve/ctx.js";
import { GENERALIZE_ROLES } from "../src/evolve/generalize.js";
import { getProposal, getPublic, ProposalSchema, saveProposal, stagedFile, type ProposalStatus, type Tier } from "../src/evolve/proposals.js";
import { PROMPTS } from "../src/evolve/prompts.js";
import { stageProposals } from "../src/evolve/stage.js";
import { Budget } from "../src/scope/model.js";
import { evolveFixture, generalizingScript, git, scriptedEvolveIo, setStatus, unescapeFence, withDeps } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "skills/review/SKILL.md": "x\n", "sindri/package.json": "{}", "sindri/src/observe/observe.ts": "export const a = 1;\n" };

async function ready(cap = 2, io?: EvolveIo) {
  const fx = await evolveFixture({ files: FILES, extraYaml: `evolve:\n  maxOpenProposals: ${cap}\n`, prompts: () => PROMPTS, io });
  await init([], fx.ctx);
  let n = 0;
  const save = (title: string, over: Record<string, unknown>, tier: Tier, status?: ProposalStatus) => {
    const id = fx.ctx.write((epoch) =>
      saveProposal(fx.ctx.db, ProposalSchema.parse({
        artifact: "skill:review", kind: "skill-edit", title, rationale: "r", evidence: ["pr:1"],
        change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" }, ...over,
      }), "reflect:pr-1", tier, epoch, new Date(Date.parse("2026-10-08T12:00:00Z") + n++ * 1000)).id);
    if (status !== undefined) fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, status, epoch, fx.deps.now()));
    return id;
  };
  return { fx, save };
}

describe("sindri evolve stage (the unattended half; Review Focus 6)", () => {
  it("stages the best-evidenced proposals up to the cap, previews them under the state dir, and leaves prompt variants for compare", async () => {
    const { fx, save } = await ready();
    const a = save("Alpha change here", {}, "code");
    const b = save("Beta change here", { evidence: ["pr:1", "pr:2", "pr:3"] }, "code");
    const c = save("Gamma hook fix here", { artifact: "hook:done-gate", kind: "hook-fix", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } }, "code");
    const d = save("Delta prompt text", { artifact: "prompt:scope.draft", kind: "prompt-edit", change: { type: "replace-prompt", text: "new" } }, "self-adopt");
    const r = await stage([], fx.ctx);
    const dir = path.dirname(stagedFile(fx.deps, a));
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(`Staged 2 proposal(s) (0 approval tier) in ${dir}; 1 more waiting (cap 2, 2 in flight); 1 prompt variant(s) wait for compare.\nNext: sindri evolve publish\n`);
    expect([a, b, c, d].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["staged", "staged", "proposed", "proposed"]);
    const preview = fs.readFileSync(stagedFile(fx.deps, b), "utf8");
    expect(preview).toContain("### Task 1: Beta change here");
    expect(fs.readFileSync(stagedFile(fx.deps, a), "utf8")).toContain("### Task 2: Alpha change here");
    expect(parsePlan(`# P\n\n${preview}`).tasks).toHaveLength(1);
    expect(fs.statSync(dir).mode & 0o777).toBe(0o700);
    expect(fs.statSync(stagedFile(fx.deps, b)).mode & 0o777).toBe(0o600);

    const capped = await stage([], fx.ctx);
    expect(capped.stdout).toBe("Cap reached: 2 proposals are staged or published and not merged yet (evolve.maxOpenProposals is 2); 1 waiting.\nNext: merge or reject some (sindri evolve proposals), then rerun sindri evolve stage\n");
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, a, "rejected", epoch, fx.deps.now()));
    const after = await stage(["--json"], fx.ctx);
    const out = JSON.parse(after.stdout) as { staged: { id: string; tier: string }[]; reclassified: number; waiting: number };
    // The hook proposal was saved as code, but the registry says the hook is protected: it is re-tiered, not trusted.
    expect(out).toMatchObject({ staged: [{ id: c, tier: "approval" }], reclassified: 1, waiting: 0 });
    expect(getProposal(fx.ctx.db, c)).toMatchObject({ status: "staged", tier: "approval" });
    fx.close();
  });

  it("says when there's nothing to stage", async () => {
    const { fx, save } = await ready();
    expect((await stage([], fx.ctx)).stdout).toBe("Nothing to stage.\nNext: sindri evolve proposals\n");
    save("Delta prompt text", { artifact: "prompt:scope.draft", kind: "prompt-edit", change: { type: "replace-prompt", text: "new" } }, "self-adopt");
    expect((await stage([], fx.ctx)).stdout).toBe("Nothing to stage; 1 prompt variant(s) wait for compare.\nNext: sindri evolve proposals\n");
    fx.close();
  });
});

describe("sindri evolve stage (guarded transitions)", () => {
  // The weekly stage waits on the tick lock; while it sleeps, something else commits.
  const heldLock = (fx: Awaited<ReturnType<typeof ready>>["fx"], whileWaiting: () => void) => {
    const held = acquireTickLock({ dir: stateDir(fx.deps), db: fx.ctx.db, sys: fx.deps.system, now: fx.deps.now });
    expect(held.ok).toBe(true);
    return withDeps(fx.ctx, { sleep: async () => { if (held.ok) held.release(); whileWaiting(); } });
  };

  it("does not resurrect a proposal rejected while stage waited for the lock", async () => {
    const { fx, save } = await ready();
    const id = save("Alpha change here", {}, "code");
    const ctx = heldLock(fx, () => void fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, "rejected", epoch, fx.deps.now())));
    const r = await stage([], ctx);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("rejected");
    expect(fs.existsSync(stagedFile(fx.deps, id))).toBe(false);
    expect(r.stdout).toBe("Nothing to stage.\nNext: sindri evolve proposals\n");
    fx.close();
  });

  it("counts the cap under the lock: a proposal staged by an overlapping run uses the slot", async () => {
    const { fx, save } = await ready(1);
    const a = save("Alpha change here", {}, "code");
    const b = save("Beta change here", {}, "code");
    const ctx = heldLock(fx, () => void fx.ctx.write((epoch) => setStatus(fx.ctx.db, b, "staged", epoch, fx.deps.now())));
    const r = await stage([], ctx);
    expect(getProposal(fx.ctx.db, a)?.status).toBe("proposed");
    expect(r.stdout).toContain("Cap reached: 1 proposals are staged or published");
    fx.close();
  });
});

describe("sindri evolve stage (Task 10 rulings)", () => {
  it("writes through the retrying writer, so a held tick lock can't lose the unattended weekly run (m2)", async () => {
    const { fx, save } = await ready();
    const id = save("Alpha change here", {}, "code");
    const ctx = { ...fx.ctx, write: () => { throw new Error("stage must use writeRetry"); } };
    expect((await stage([], ctx)).exitCode).toBe(0);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("staged");
    fx.close();
  });

  it("rejects a proposal that duplicates merged or adopted work, with an audit entry, instead of staging it (T8 m1)", async () => {
    const { fx, save } = await ready(4);
    const merged = save("Alpha change here", {}, "code", "merged");
    const adopted = save("Gamma hook fix here", { artifact: "hook:done-gate", kind: "hook-fix", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } }, "approval", "adopted");
    const again = save("alpha CHANGE here!", {}, "code");
    const hook = save("Gamma hook fix here", { artifact: "hook:done-gate", kind: "hook-fix", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } }, "approval");
    const other = save("Alpha change here", { artifact: "skill:other" }, "code");
    const fresh = save("A fresh idea about it", {}, "code");
    const r = await stage([], fx.ctx);
    expect([again, hook, other, fresh].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["rejected", "rejected", "staged", "staged"]);
    expect(r.stdout).toContain("2 duplicate(s) of merged or adopted work rejected");
    expect(fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit ORDER BY seq").all()).toEqual([
      { verb: "reject", detail: `${again}: duplicate of ${merged}` },
      { verb: "reject", detail: `${hook}: duplicate of ${adopted}` },
    ]);
    expect(fs.existsSync(stagedFile(fx.deps, again))).toBe(false);
    expect(JSON.parse((await stage(["--json"], fx.ctx)).stdout)).toMatchObject({ duplicates: 0 });
    fx.close();
  });
});

describe("stage preview hardening (Task 10 fix round 1, m1)", () => {
  it("never writes a live key rebuilt from a control-character split into the staged preview", async () => {
    const { fx, save } = await ready(4);
    const id = save("Clean title here", { rationale: `leaked ${"AKIA" + "\u0001" + "ABCDEFGHIJKLMNOP"} here` }, "code");
    await stage([], fx.ctx);
    const preview = fs.readFileSync(stagedFile(fx.deps, id), "utf8");
    expect(preview).not.toMatch(/AKIA[A-Z0-9]{16}/);
    expect(preview).toContain("REDACTED");
    fx.close();
  });

  it("marks published proposals whose commits are on the default branch as merged before it counts the cap (I2)", async () => {
    const { fx, save } = await ready(10);
    const published = Array.from({ length: 10 }, (_, k) => save(`Published change number ${k}`, {}, "code", "published"));
    for (const id of published) git(fx.repo, "commit", "-q", "--allow-empty", "-m", `feat: ship it\n\nProposal \`${id}\``);
    const fresh = save("Fresh change to stage", {}, "code");
    const r = await stage([], fx.ctx);
    expect(r.stdout).toContain("Staged 1 proposal(s)");
    expect(r.stdout).not.toContain("Cap reached");
    expect(published.map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(Array(10).fill("merged"));
    expect(getProposal(fx.ctx.db, fresh)?.status).toBe("staged");
    fx.close();
  });
});

describe("stage generalizes first (privacy B1)", () => {
  it("stages the generalized version and writes the preview from it", async () => {
    const io = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.rewrite
      ? { ...(JSON.parse(unescapeFence(/<untrusted id="proposal">([\s\S]*?)<\/untrusted>/.exec(c.input)![1])) as object), title: "Generic title for publishing" }
      : { leaks: false, category: "none" }));
    const { fx, save } = await ready(2, io);
    const id = save("Globex quarbleScheduler retries", {}, "code");
    const o = await stageProposals(fx.ctx);
    expect(o.staged.map((s) => s.id)).toEqual([id]);
    expect(getPublic(fx.ctx.db, id)?.title).toBe("Generic title for publishing");
    expect(fs.readFileSync(stagedFile(fx.deps, id), "utf8")).toContain("Generic title for publishing");
    expect(fs.readFileSync(stagedFile(fx.deps, id), "utf8")).not.toContain("quarbleScheduler");
    fx.close();
  });

  it("holds a proposal the judge flags, keeps no public row, and retries it on the next stage", async () => {
    const strict = scriptedEvolveIo(generalizingScript((c) => { throw new Error(`unexpected ${c.role}`); }));
    const leaky = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge ? { leaks: true, category: "tickets" } : generalizingScript()(c)));
    const { fx, save } = await ready(2, leaky);
    const id = save("Alpha change here", {}, "code");
    const first = await stageProposals(fx.ctx);
    expect(first.held).toEqual([{ id, why: "generalize: the judge found tickets" }]);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("held");
    expect(getPublic(fx.ctx.db, id)).toBeNull();
    const second = await stageProposals({ ...fx.ctx, io: strict });
    expect(second.staged.map((s) => s.id)).toEqual([id]);
    fx.close();
  });

  it("leaves proposals the budget ran out on, even between the rewrite and the judge, as they were (Review Focus 4, M1)", async () => {
    const { fx, save } = await ready(3);
    const a = save("Alpha change here", { evidence: ["pr:1", "pr:2"] }, "code");
    const b = save("Beta change here", {}, "code");
    const budget = new Budget(2); // the pass-through runner spends 2 tokens per call: one rewrite fits, nothing after it
    const o = await stageProposals(fx.ctx, budget);
    expect(getProposal(fx.ctx.db, a)?.status).toBe("proposed"); // its judge call found the budget gone: not a judge failure
    expect(getProposal(fx.ctx.db, b)?.status).toBe("proposed");
    expect(o).toMatchObject({ staged: [], held: [], waiting: 2 });
    // A held retry the budget doesn't reach stays held, and is not reported as held again.
    const leaky = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge && c.input.includes("Gamma") ? { leaks: true, category: "people" } : generalizingScript()(c)));
    const g = save("Gamma change here", {}, "code");
    fx.ctx.write((epoch) => { setStatus(fx.ctx.db, a, "rejected", epoch, fx.deps.now()); setStatus(fx.ctx.db, b, "rejected", epoch, fx.deps.now()); });
    expect((await stageProposals({ ...fx.ctx, io: leaky })).held.map((h) => h.id)).toEqual([g]);
    const again = await stageProposals({ ...fx.ctx, io: leaky }, new Budget(2));
    expect(again).toMatchObject({ held: [], waiting: 1 });
    expect(getProposal(fx.ctx.db, g)?.status).toBe("held");
    fx.close();
  });

  it("spends no model call on a duplicate of shipped work; the write still rejects it", async () => {
    const io = scriptedEvolveIo(generalizingScript());
    const { fx, save } = await ready(4, io);
    const merged = save("Alpha change here", {}, "code", "merged");
    const again = save("alpha CHANGE here!", { evidence: ["pr:1", "pr:2"] }, "code");
    const fresh = save("A fresh idea about it", {}, "code");
    const o = await stageProposals(fx.ctx);
    expect(io.calls).toHaveLength(2);
    expect(io.calls.every((c) => c.input.includes("A fresh idea about it"))).toBe(true);
    expect(o).toMatchObject({ staged: [{ id: fresh }], duplicates: 1 });
    expect(getProposal(fx.ctx.db, again)?.status).toBe("rejected");
    expect(fx.ctx.db.prepare("SELECT detail FROM evolve_audit").all()).toEqual([{ detail: `${again}: duplicate of ${merged}` }]);
    fx.close();
  });

  it("sends each proposal's own resolved transcript lines to both generalize calls, never another proposal's", async () => {
    const io = scriptedEvolveIo(generalizingScript());
    const { fx, save } = await ready(2, io);
    const line = (text: string) => JSON.stringify({ type: "user", cwd: fx.repo, timestamp: "2026-10-05T10:00:00Z", message: { content: text } });
    fs.writeFileSync(path.join(fx.transcripts, "5e55a1d0-a.jsonl"), `${line("the Initech billing retry broke")}\n${line("second line, for the other proposal")}\n`);
    save("Alpha change here", { evidence: ["transcript:5e55a1d0#1", "pr:1"] }, "code");
    save("Beta change here", { evidence: ["transcript:5e55a1d0#2"] }, "code");
    await stageProposals(fx.ctx);
    const inputs = io.calls.map((c) => ({ role: c.role, input: c.input }));
    const alpha = inputs.filter((c) => c.input.includes("Alpha change here"));
    expect(alpha.map((c) => c.role)).toEqual([GENERALIZE_ROLES.rewrite, GENERALIZE_ROLES.judge]);
    for (const c of alpha) {
      expect(c.input).toContain('<untrusted id="transcript:5e55a1d0#1">the Initech billing retry broke</untrusted>');
      expect(c.input).not.toContain("second line");
    }
    fx.close();
  });

  it("prints held proposals under the summary line in every branch of the stage output", async () => {
    const leakyFor = (title: string) => scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge && c.input.includes(title) ? { leaks: true, category: "people" } : generalizingScript()(c)));
    const { fx, save } = await ready(2, leakyFor("Alpha"));
    const a = save("Alpha change here", { evidence: ["pr:1", "pr:2"] }, "code");
    const b = save("Beta change here", {}, "code");
    const held = `held ${a}: generalize: the judge found people`;
    const dir = path.dirname(stagedFile(fx.deps, a));
    expect((await stage([], fx.ctx)).stdout).toBe(`Staged 1 proposal(s) (0 approval tier) in ${dir}.\n${held}\nNext: sindri evolve publish\n`);
    // Held at stage: retried (and held again) on the next run, which has nothing else to stage.
    expect((await stage([], fx.ctx)).stdout).toBe(`Nothing to stage; 1 held by the generalization pass.\n${held}\nNext: sindri evolve proposals\n`);
    // One free slot: the new proposal takes it ahead of the held retry, which has more evidence but waits (I1).
    const e = save("Epsilon change here", {}, "code");
    expect((await stage([], fx.ctx)).stdout).toBe(`Staged 1 proposal(s) (0 approval tier) in ${dir}; 1 more waiting (cap 2, 2 in flight).\nNext: sindri evolve publish\n`);
    expect([a, b, e].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["held", "staged", "staged"]);
    fx.close();
  });

  it("fills slots from proposed proposals first, then retries held ones with what is left (I1)", async () => {
    const io = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge && !c.input.includes("Fresh") ? { leaks: true, category: "domain" } : generalizingScript()(c)));
    const { fx, save } = await ready(2, io);
    const many = { evidence: ["pr:1", "pr:2", "pr:3", "pr:4"] };
    const h1 = save("Held one here", many, "code");
    const h2 = save("Held two here", many, "code");
    expect((await stageProposals(fx.ctx)).held.map((h) => h.id)).toEqual([h1, h2]);
    // Re-proposals merged more evidence into the held ones; the fresh one still goes first.
    const fresh = save("Fresh idea here", {}, "code");
    io.calls.length = 0;
    const o = await stageProposals(fx.ctx);
    expect(o.staged.map((s) => s.id)).toEqual([fresh]);
    expect(io.calls.filter((c) => c.role === GENERALIZE_ROLES.rewrite).map((c) => c.input.includes("Fresh idea here"))).toEqual([true, false]);
    expect(o.held).toHaveLength(1);
    expect(o.waiting).toBe(1);
    expect(getProposal(fx.ctx.db, fresh)?.status).toBe("staged");
    // With no new proposals left, the one free slot goes to a held retry (held again); the other retry waits.
    const out = (await stage([], fx.ctx)).stdout;
    expect(out).toMatch(/^Nothing staged; 1 held by the generalization pass; 1 waiting for the next run\.\nheld \w+: generalize: the judge found domain\nNext: rerun sindri evolve stage\n$/);
    fx.close();
  });
});
