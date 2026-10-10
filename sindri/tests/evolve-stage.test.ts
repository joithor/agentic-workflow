import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { parsePlan } from "../src/adapters/plan-file/parse.js";
import { stateDir } from "../src/deps.js";
import { init } from "../src/evolve/cmd/registry.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { stage } from "../src/evolve/cmd/stage.js";
import { retry } from "../src/evolve/cmd/proposals.js";
import type { EvolveIo } from "../src/evolve/ctx.js";
import { GENERALIZE_ROLES } from "../src/evolve/generalize.js";
import { getProposal, getPublic, ProposalSchema, savePublic, saveProposal, stagedFile, type ProposalStatus, type Tier } from "../src/evolve/proposals.js";
import { PROMPTS } from "../src/evolve/prompts.js";
import { quote } from "../src/evolve/render.js";
import { stageProposals } from "../src/evolve/stage.js";
import { SindriError } from "../src/errors.js";
import { Budget } from "../src/scope/model.js";
import { evolveFixture, generalizingScript, git, scriptedEvolveIo, setStatus, unescapeFence, withDeps } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "skills/review/SKILL.md": "x\n", "sindri/package.json": "{}", "sindri/src/observe/observe.ts": "export const a = 1;\n" };

async function ready(cap = 2, io?: EvolveIo) {
  const fx = await evolveFixture({ files: FILES, extraYaml: `evolve:\n  maxOpenProposals: ${cap}\n`, prompts: () => PROMPTS, io });
  await init([], fx.ctx);
  let n = 0;
  const save = (title: string, over: Record<string, unknown>, tier: Tier, status?: ProposalStatus, sessions: string[] = []) => {
    const id = fx.ctx.write((epoch) =>
      saveProposal(fx.ctx.db, ProposalSchema.parse({
        artifact: "skill:review", kind: "skill-edit", title, rationale: "r", evidence: ["pr:1"],
        change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" }, ...over,
      }), "reflect:pr-1", tier, epoch, new Date(Date.parse("2026-10-08T12:00:00Z") + n++ * 1000), sessions).id);
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
    // The overlapping run stages b the way stage does: with its public row.
    const ctx = heldLock(fx, () => void fx.ctx.write((epoch) => {
      setStatus(fx.ctx.db, b, "staged", epoch, fx.deps.now());
      savePublic(fx.ctx.db, b, getProposal(fx.ctx.db, b)!.proposal, { category: "none", model: "m" }, epoch, fx.deps.now());
    }));
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

  it("renders the preview from the stored (scrubbed) public row, the one source publish renders too (data-integrity-guardian-3)", async () => {
    const key = "AKIA" + "ABCDEFGHIJKLMNOP";
    const rewriteWith = (over: object) => scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.rewrite
      ? { ...(JSON.parse(unescapeFence(/<untrusted id="proposal">([\s\S]*?)<\/untrusted>/.exec(c.input)![1])) as object), ...over }
      : { leaks: false, category: "none" }));
    const { fx, save } = await ready(2, rewriteWith({ rationale: `rotate ${key} first` }));
    const id = save("Alpha change here", {}, "code");
    await stageProposals(fx.ctx);
    const pub = getPublic(fx.ctx.db, id)!;
    expect(pub.rationale).not.toContain(key);
    expect(fs.readFileSync(stagedFile(fx.deps, id), "utf8")).toContain(quote(pub.rationale).join("\n"));
    // A file path the scrubber redacts no longer parses as a path: nothing is stored or previewed, and it holds.
    const pathy = save("Beta change here", {}, "code");
    const o = await stageProposals({ ...fx.ctx, io: rewriteWith({ change: { type: "describe", files: [`skills/review/${key}.md`], description: "d" } }) });
    expect(o.held).toEqual([{ id: pathy, why: "generalize: the rewrite contains text the scrubber redacts" }]);
    expect(getPublic(fx.ctx.db, pathy)).toBeNull();
    expect(getProposal(fx.ctx.db, pathy)?.status).toBe("held");
    expect(fs.existsSync(stagedFile(fx.deps, pathy))).toBe(false);
    fx.close();
  });

  it("treats a public row that no longer parses like a missing one: a malformed row can't stop stage, and stage regenerates it (kieran-typescript-reviewer-1)", async () => {
    const { fx, save } = await ready(2, scriptedEvolveIo(generalizingScript()));
    const held = save("Held with a drifted row", {}, "code", "held");
    fx.ctx.db.prepare("INSERT INTO proposal_public (proposal_id, body, judge_category, model, created_at, epoch) VALUES (?, '{\"drifted\":true}', 'none', 'm', '2026-10-08T00:00:00Z', 1)").run(held);
    fx.ctx.db.prepare("INSERT INTO proposal_public (proposal_id, body, judge_category, model, created_at, epoch) VALUES (?, 'not json', 'none', 'm', '2026-10-08T00:00:00Z', 1)").run(save("Held with a broken row", {}, "code", "held"));
    const fresh = save("Fresh change here", {}, "code");
    const o = await stageProposals(fx.ctx);
    expect(o.staged.map((s) => s.id)).toEqual([fresh, held]); // new proposals first, then the retries the slots allow
    expect(getProposal(fx.ctx.db, held)?.status).toBe("staged");
    expect(getPublic(fx.ctx.db, held)?.title).toBe("Held with a drifted row"); // regenerated: readable again
    fx.close();
  });

  it("keeps the stricter tier: a rewrite that drops the protected file still stages as approval", async () => {
    const io = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.rewrite
      ? { ...(JSON.parse(unescapeFence(/<untrusted id="proposal">([\s\S]*?)<\/untrusted>/.exec(c.input)![1])) as object), change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" } }
      : { leaks: false, category: "none" }));
    const { fx, save } = await ready(2, io);
    const id = save("Alpha change here", { change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "d" } }, "code");
    const o = await stageProposals(fx.ctx);
    expect(o.staged).toEqual([{ id, tier: "approval" }]);
    expect(getProposal(fx.ctx.db, id)?.tier).toBe("approval");
    expect(getPublic(fx.ctx.db, id)?.change).toMatchObject({ files: ["skills/review/SKILL.md"] });
    expect(fs.readFileSync(stagedFile(fx.deps, id), "utf8")).toContain("touches a protected path");
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

  it("rejects a held-at-stage retry that duplicates shipped work, with no model call, instead of retrying it", async () => {
    const io = scriptedEvolveIo(generalizingScript());
    const { fx, save } = await ready(4, io);
    const merged = save("Alpha change here", {}, "code", "merged");
    const held = save("alpha CHANGE here!", {}, "code", "held"); // no public row: a held-at-stage retry
    const o = await stageProposals(fx.ctx);
    expect(io.calls).toEqual([]);
    expect(o).toMatchObject({ staged: [], held: [], duplicates: 1 });
    expect(getProposal(fx.ctx.db, held)?.status).toBe("rejected");
    expect(fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit").all()).toEqual([{ verb: "reject", detail: `${held}: duplicate of ${merged}` }]);
    fx.close();
  });

  it("regenerates a staged proposal with no public row (a v4-era stage), which doesn't hold a cap slot meanwhile", async () => {
    const io = scriptedEvolveIo(generalizingScript());
    const { fx, save } = await ready(1, io);
    const legacy = save("Legacy staged change", { evidence: ["pr:1", "pr:2"] }, "code", "staged");
    const fresh = save("Fresh change here", {}, "code");
    // One slot: the fresh proposal goes first; the legacy row waits without counting as in flight.
    const first = await stageProposals(fx.ctx);
    expect(first).toMatchObject({ staged: [{ id: fresh }], waiting: 1, inFlight: 0 });
    expect(getPublic(fx.ctx.db, legacy)).toBeNull();
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, fresh, "rejected", epoch, fx.deps.now()));
    io.calls.length = 0;
    const second = await stageProposals(fx.ctx);
    expect(second).toMatchObject({ staged: [{ id: legacy }], waiting: 0 });
    expect(io.calls.map((c) => c.role)).toEqual([GENERALIZE_ROLES.rewrite, GENERALIZE_ROLES.judge]);
    expect(getProposal(fx.ctx.db, legacy)?.status).toBe("staged");
    expect(getPublic(fx.ctx.db, legacy)?.title).toBe("Legacy staged change");
    expect(fs.readFileSync(stagedFile(fx.deps, legacy), "utf8")).toContain("Legacy staged change");
    fx.close();
  });

  it("holds a regenerated v4-era staged proposal the judge flags", async () => {
    const io = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge ? { leaks: true, category: "domain" } : generalizingScript()(c)));
    const { fx, save } = await ready(2, io);
    const legacy = save("Legacy staged change", {}, "code", "staged");
    expect((await stageProposals(fx.ctx)).held).toEqual([{ id: legacy, why: "generalize: the judge found domain" }]);
    expect(getProposal(fx.ctx.db, legacy)?.status).toBe("held");
    fx.close();
  });

  it("removes every staged preview whose proposal has no public row, so no v4-era preview keeps the original text", async () => {
    const { fx, save } = await ready(2);
    const legacy = save("Legacy staged change", {}, "code", "staged");
    const held = save("Legacy held change", {}, "code", "held");
    const gone = save("Legacy rejected change", {}, "code", "rejected");
    const kept = save("Kept change here", {}, "code");
    await stageProposals(fx.ctx, new Budget(0)); // nothing is generalized: only the cleanup runs
    expect(getPublic(fx.ctx.db, kept)).toBeNull();
    for (const id of [legacy, held, gone]) fs.writeFileSync(stagedFile(fx.deps, id), "### Task 1: original private text\n");
    fs.writeFileSync(path.join(path.dirname(stagedFile(fx.deps, kept)), "notes.txt"), "not a preview\n");
    await stageProposals(fx.ctx);
    expect(fs.existsSync(stagedFile(fx.deps, kept))).toBe(true); // staged now, rendered from its public row
    expect([legacy, held, gone].filter((id) => fs.existsSync(stagedFile(fx.deps, id)))).toEqual([legacy]); // the legacy row was regenerated and rewritten
    expect(fs.readFileSync(stagedFile(fx.deps, legacy), "utf8")).not.toContain("original private text");
    expect(fs.existsSync(path.join(path.dirname(stagedFile(fx.deps, kept)), "notes.txt"))).toBe(true);
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

  it("also sends the recorded source sessions' turns to both generalize calls, cited lines first and bounded (security-sentinel-0)", async () => {
    const io = scriptedEvolveIo(generalizingScript());
    const { fx, save } = await ready(2, io);
    const line = (text: string) => JSON.stringify({ type: "user", cwd: fx.repo, timestamp: "2026-10-05T10:00:00Z", message: { content: text } });
    fs.writeFileSync(path.join(fx.transcripts, "5e55a1d0-a.jsonl"), `${line("the Initech billing retry broke")}\n${line("a tool result follows")}\n`);
    // A long source session: far more text than the evidence bound, so only its first lines fit.
    fs.writeFileSync(path.join(fx.transcripts, "6f66b2e1-b.jsonl"), Array.from({ length: 40 }, (_, i) => line(`turn ${i} ${"word ".repeat(400)}`)).join("\n"));
    fs.writeFileSync(path.join(fx.transcripts, "0ddba11e-c.jsonl"), `${line("cited from a session that isn't a source")}\n`);
    save("Alpha change here", { evidence: ["pr:1", "transcript:0ddba11e#1"] }, "code", undefined, ["6f66b2e1", "5e55a1d0"]);
    await stageProposals(fx.ctx);
    const alpha = io.calls.filter((c) => c.input.includes("Alpha change here"));
    expect(alpha.map((c) => c.role)).toEqual([GENERALIZE_ROLES.rewrite, GENERALIZE_ROLES.judge]);
    for (const c of alpha) {
      const cited = c.input.indexOf('<untrusted id="transcript:0ddba11e#1">');
      expect(cited).toBeGreaterThan(-1);
      expect(c.input).toContain('<untrusted id="transcript:5e55a1d0#1">the Initech billing retry broke</untrusted>');
      expect(c.input).toContain('<untrusted id="transcript:6f66b2e1#1">turn 0 ');
      expect(c.input.indexOf('<untrusted id="transcript:6f66b2e1#1">')).toBeGreaterThan(cited);
      expect(c.input).not.toContain('id="transcript:6f66b2e1#40"'); // bounded like the evidence
      expect(c.input.length).toBeLessThan(60_000);
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
    expect((await stage([], fx.ctx)).stdout).toBe(`Staged 1 proposal(s) (0 approval tier) in ${dir}; 1 held by the generalization pass.\n${held}\nNext: sindri evolve publish\n`);
    // Held at stage: retried (and held again) on the next run, which has nothing else to stage.
    expect((await stage([], fx.ctx)).stdout).toBe(`Nothing to stage; 1 held by the generalization pass.\n${held}\nNext: sindri evolve proposals\n`);
    // One free slot: the new proposal takes it ahead of the held retry, which has more evidence but waits (I1).
    const e = save("Epsilon change here", {}, "code");
    expect((await stage([], fx.ctx)).stdout).toBe(`Staged 1 proposal(s) (0 approval tier) in ${dir}; 1 more waiting (cap 2, 2 in flight).\nNext: sindri evolve publish\n`);
    expect([a, b, e].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["held", "staged", "staged"]);
    fx.close();
  });

  it("gives up on a proposal after evolve.maxGeneralizeAttempts holds: no more model calls until it is retried or rejected", async () => {
    const io = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge ? { leaks: true, category: "domain" } : generalizingScript()(c)));
    const fx = await evolveFixture({ files: FILES, extraYaml: "evolve:\n  maxOpenProposals: 2\n  maxGeneralizeAttempts: 2\n", prompts: () => PROMPTS, io });
    await init([], fx.ctx);
    const id = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "Alpha change here", rationale: "r", evidence: ["pr:1"],
      change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "reflect:pr-1", "code", epoch, fx.deps.now()).id);
    const judged = `held ${id}: generalize: the judge found domain`;
    const gaveUp = `held ${id}: generalize: gave up after 2 attempts; sindri evolve retry ${id} to try again, or reject it`;
    expect((await stage([], fx.ctx)).stdout).toContain(judged);
    expect((await stage([], fx.ctx)).stdout).toContain(judged);
    expect(io.calls).toHaveLength(4);
    const out = await stage([], fx.ctx);
    expect(out.stdout).toBe(`Nothing to stage; 1 held by the generalization pass.\n${gaveUp}\nNext: sindri evolve proposals\n`);
    expect(io.calls).toHaveLength(4); // no model call once it gave up
    expect(getProposal(fx.ctx.db, id)?.status).toBe("held");
    expect(fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit ORDER BY seq").all()).toEqual([
      { verb: "generalize-hold", detail: `${id}: generalize: the judge found domain` },
      { verb: "generalize-hold", detail: `${id}: generalize: the judge found domain` },
    ]);
    // A given-up proposal doesn't take a slot: a new one still stages.
    const fresh = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "Fresh change here", rationale: "r", evidence: ["pr:1"],
      change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "reflect:pr-1", "code", epoch, fx.deps.now()).id);
    const ok = scriptedEvolveIo(generalizingScript());
    const o = await stageProposals({ ...fx.ctx, io: ok });
    expect(o).toMatchObject({ staged: [{ id: fresh }], held: [{ id, why: `generalize: gave up after 2 attempts; sindri evolve retry ${id} to try again, or reject it` }], gaveUp: [id], waiting: 0 });
    expect(ok.calls.every((c) => c.input.includes("Fresh change here"))).toBe(true);
    fx.close();
  });

  it("counts only content holds toward the cap: a failed rewrite or judge call holds without spending an attempt (architecture-strategist-0)", async () => {
    const fail = (): never => { throw new SindriError("SND-SCOPE-002", "model job failed (claude exited 1)"); };
    const down = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge ? fail() : generalizingScript()(c)));
    const fx = await evolveFixture({ files: FILES, extraYaml: "evolve:\n  maxOpenProposals: 2\n  maxGeneralizeAttempts: 1\n", prompts: () => PROMPTS, io: down });
    await init([], fx.ctx);
    const id = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "Alpha change here", rationale: "r", evidence: ["pr:1"],
      change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "reflect:pr-1", "code", epoch, fx.deps.now()).id);
    for (let run = 0; run < 3; run++) {
      const o = await stageProposals(fx.ctx);
      expect(o.held).toEqual([{ id, why: "generalize: the judge failed" }]);
      expect(o.gaveUp).toEqual([]);
    }
    const rewriteDown = scriptedEvolveIo(fail);
    expect((await stageProposals({ ...fx.ctx, io: rewriteDown })).held).toEqual([{ id, why: "generalize: the rewrite failed" }]);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("held");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit WHERE verb = 'generalize-hold'").get()).toEqual({ c: 0 });
    const up = await stageProposals({ ...fx.ctx, io: scriptedEvolveIo(generalizingScript()) });
    expect(up.staged.map((x) => x.id)).toEqual([id]);
    fx.close();
  });

  it("counts an out-of-schema answer toward the cap: a rewriter that always answers off-schema gives up (kieran-typescript-reviewer-2)", async () => {
    const offSchema = scriptedEvolveIo(() => ({ nope: 1 }));
    const fx = await evolveFixture({ files: FILES, extraYaml: "evolve:\n  maxOpenProposals: 2\n  maxGeneralizeAttempts: 2\n", prompts: () => PROMPTS, io: offSchema });
    await init([], fx.ctx);
    const id = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "Alpha change here", rationale: "r", evidence: ["pr:1"],
      change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "reflect:pr-1", "code", epoch, fx.deps.now()).id);
    for (let run = 0; run < 2; run++) expect((await stageProposals(fx.ctx)).held).toEqual([{ id, why: "generalize: the rewrite failed" }]);
    const calls = offSchema.calls.length;
    const o = await stageProposals(fx.ctx);
    expect(o.gaveUp).toEqual([id]);
    expect(offSchema.calls).toHaveLength(calls); // no more model calls once it gave up
    fx.close();
  });

  it("sindri evolve retry lets stage try a given-up proposal again: holds count only after the latest retry (architecture-strategist-0)", async () => {
    const leaky = scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge ? { leaks: true, category: "domain" } : generalizingScript()(c)));
    const fx = await evolveFixture({ files: FILES, extraYaml: "evolve:\n  maxOpenProposals: 2\n  maxGeneralizeAttempts: 1\n", prompts: () => PROMPTS, io: leaky });
    await init([], fx.ctx);
    const id = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "Alpha change here", rationale: "r", evidence: ["pr:1"],
      change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "reflect:pr-1", "code", epoch, fx.deps.now()).id);
    await stageProposals(fx.ctx);
    expect((await stageProposals(fx.ctx)).gaveUp).toEqual([id]);
    expect(leaky.calls).toHaveLength(2);
    const r = await retry([id], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(`Proposal ${id} will be generalized again by the next sindri evolve stage; its 1 earlier hold(s) no longer count toward evolve.maxGeneralizeAttempts (1).\nNext: sindri evolve stage\n`);
    expect(fx.ctx.db.prepare("SELECT verb, actor, detail FROM evolve_audit WHERE verb = 'generalize-retry'").all()).toEqual([{ verb: "generalize-retry", actor: fx.deps.system.username(), detail: id }]);
    // Retried once (two model calls), held again by content, so it gives up again at the cap.
    const again = await stageProposals(fx.ctx);
    expect(again.held).toEqual([{ id, why: "generalize: the judge found domain" }]);
    expect(leaky.calls).toHaveLength(4);
    expect((await stageProposals(fx.ctx)).gaveUp).toEqual([id]);
    expect(leaky.calls).toHaveLength(4);
    // A retry that succeeds stages it.
    await retry([id, "--json"], fx.ctx);
    expect((await stageProposals({ ...fx.ctx, io: scriptedEvolveIo(generalizingScript()) })).staged.map((x) => x.id)).toEqual([id]);
    fx.close();
  });

  it("sindri evolve retry refuses an unknown id and does nothing for a proposal that isn't held at the cap", async () => {
    const { fx, save } = await ready(2, scriptedEvolveIo((c) => (c.role === GENERALIZE_ROLES.judge ? { leaks: true, category: "people" } : generalizingScript()(c))));
    await expect(retry([], fx.ctx)).rejects.toThrow(expect.objectContaining({ code: "SND-CLI-002" }));
    await expect(retry(["01NOPE"], fx.ctx)).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-008" }));
    const fresh = save("Fresh change here", {}, "code");
    expect((await retry([fresh], fx.ctx)).stdout).toBe(`Proposal ${fresh} is proposed, not held by the generalization pass; nothing to do.\nNext: sindri evolve proposals\n`);
    await stageProposals(fx.ctx);
    expect(getProposal(fx.ctx.db, fresh)?.status).toBe("held");
    expect((await retry([fresh], fx.ctx)).stdout).toBe(`Proposal ${fresh} has 1 of 3 generalize holds; stage already retries it. Nothing to do.\nNext: sindri evolve stage\n`);
    const gated = save("Gated change here", {}, "code", "held");
    fx.ctx.write((epoch) => savePublic(fx.ctx.db, gated, getProposal(fx.ctx.db, gated)!.proposal, { category: "none", model: "m" }, epoch, fx.deps.now()));
    expect((await retry([gated], fx.ctx)).stdout).toBe(`Proposal ${gated} is held by publish's privacy gate, not held by the generalization pass; nothing to do.\nNext: sindri evolve proposals\n`);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit WHERE verb = 'generalize-retry'").get()).toEqual({ c: 0 });
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
