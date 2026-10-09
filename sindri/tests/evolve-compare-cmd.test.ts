import { createHash } from "node:crypto";
import { describe, expect, it } from "vitest";

import { adopt } from "../src/evolve/cmd/adopt.js";
import { compare } from "../src/evolve/cmd/compare.js";
import { show } from "../src/evolve/cmd/proposals.js";
import { init } from "../src/evolve/cmd/registry.js";
import { isHoldout, saveReplay, type ReplayItem } from "../src/evolve/corpus.js";
import { ProposalSchema, getProposal, runningComparison, saveProposal, setStatus, type ProposalStatus } from "../src/evolve/proposals.js";
import { SOURCES_CLAUSE } from "../src/evolve/prompts.js";
import { evolveFixture, scriptedEvolveIo, withDeps, type EvolveFixture, type ScriptedEvolveIo } from "./evolve-fixtures.js";

const map = (title: string) => ({
  subject: "B", surfaces: [{ id: "S1", kind: "ui", title, detail: "", citations: ["R1"] }], implications: [],
  workstreams: [{ id: "W1", title: "W", surfaces: ["S1"], dependsOn: [], acceptance: ["a"] }], questions: [],
});
const script = (call: { model: string; system: string; input: string }): unknown => {
  if (call.model === "sonnet") {
    if (call.input.includes("boom")) throw new Error("model exploded");
    return map(call.system.includes("BETTER") ? "better" : "plain");
  }
  const a = call.input.indexOf('id="output-A"');
  const b = call.input.indexOf('id="output-B"');
  const pos = call.input.indexOf("better");
  return { winner: pos > a && pos < b ? "A" : "B", reasons: [] };
};
const preferPlain = (call: { model: string; system: string; input: string }): unknown => {
  if (call.model === "sonnet") return script(call);
  const a = call.input.indexOf('id="output-A"');
  const b = call.input.indexOf('id="output-B"');
  const pos = call.input.indexOf("plain");
  return { winner: pos > a && pos < b ? "A" : "B", reasons: [] };
};
const holdoutIds = Array.from({ length: 600 }, (_, i) => `item-${i}`).filter(isHoldout);
const item = (id: string, text = "brief"): ReplayItem => ({
  id, artifact: "scope.draft", createdAt: "2026-10-08T00:00:00Z",
  brief: { ref: "file:/b.md", kind: "brief", title: "B", text, author: null, createdAt: null, trust: "trusted" },
  records: [], outcome: { status: "complete", surfaces: 1, recall: null },
});
const variant = `BETTER prompt. ${SOURCES_CLAUSE}`;

// The corpus only counts an item the ledger has a scope run for, as a real run would have recorded.
function seed(fx: EvolveFixture, it: ReplayItem): void {
  fx.ctx.db.prepare("INSERT OR IGNORE INTO scope_runs (run_id, subject, mode, ts, status, rounds, surfaces, tokens, out_path, epoch) VALUES (?, 's', 'scope', 't', 'complete', 1, 1, 1, '/o', 1)").run(it.id);
  saveReplay(fx.deps, it);
}
const saver = (fx: EvolveFixture) => (over: Record<string, unknown> = {}) =>
  fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
    artifact: "prompt:scope.draft", kind: "prompt-edit", title: "Better scope draft prompt", rationale: "r", evidence: ["pr:12"],
    change: { type: "replace-prompt", text: variant }, ...over,
  }), "reflect:pr-12", "self-adopt", epoch, fx.deps.now()).id);
const status = (fx: EvolveFixture, id: string): ProposalStatus | undefined => getProposal(fx.ctx.db, id)?.status;

async function ready(count: number, o: { extraYaml?: string; io?: ScriptedEvolveIo; items?: ReplayItem[] } = {}) {
  const fx = await evolveFixture({ extraYaml: o.extraYaml ?? "", io: o.io ?? scriptedEvolveIo(script) });
  for (const it of o.items ?? holdoutIds.slice(0, count).map((id) => item(id))) seed(fx, it);
  return { fx, save: saver(fx), calls: () => (fx.io as ScriptedEvolveIo).calls.length };
}

describe("sindri evolve compare", () => {
  it("compares once, stores the result and the rows, and prints the stored result on a second call", async () => {
    const { fx, save, calls } = await ready(22);
    const id = save();
    const r = await compare([id], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(`won: 22 of 22 decided pairs (win rate 1.00, lower bound 0.85) on 22 holdout items; 0 ties.\nNext: sindri evolve adopt ${id}\n`);
    expect(status(fx, id)).toBe("won");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM comparisons WHERE proposal_id = ? AND item_id != '*'").get(id)).toEqual({ c: 22 });
    const made = calls();
    const again = await compare([id], fx.ctx);
    expect(again.stdout).toBe(`Stored result (run 1): won: 22 of 22 decided pairs (win rate 1.00, lower bound 0.85) on 22 holdout items; 0 ties.\nA proposal is compared once; pass --rerun to compare again (the rerun is recorded).\nNext: sindri evolve adopt ${id}\n`);
    expect(calls()).toBe(made);
    const rerun = await compare([id, "--rerun"], fx.ctx);
    expect(rerun.stdout).toContain("won: 22 of 22");
    expect(fx.ctx.db.prepare("SELECT MAX(run) AS r FROM comparisons WHERE proposal_id = ?").get(id)).toEqual({ r: 2 });
    const json = JSON.parse((await compare([id, "--json"], fx.ctx)).stdout) as { status: string; run: number; stored: boolean };
    expect(json).toMatchObject({ status: "won", run: 2, stored: true });
    fx.close();
  });

  it("explains each non-verdict result, and lets it be rerun freely", async () => {
    const { fx, save, calls } = await ready(5);
    const id = save();
    const small = await compare([id], fx.ctx);
    expect(small.exitCode).toBe(1);
    expect(small.stdout).toBe("insufficient-corpus: 5 holdout items, need 20. About 50 more scope runs would add the missing 15.\nNext: keep running sindri scope; sindri evolve status shows the corpus\n");
    expect(status(fx, id)).toBe("insufficient-corpus");
    expect(calls()).toBe(0);
    for (const hid of holdoutIds.slice(5, 22)) seed(fx, item(hid));
    expect((await compare([id], fx.ctx)).stdout).toContain("won: 22 of 22");
    const leaky = save({ title: "A leaky draft prompt", change: { type: "replace-prompt", text: `The judge likes this. ${SOURCES_CLAUSE}` } });
    const l = await compare([leaky], fx.ctx);
    expect(l.stdout).toBe("leaky-variant: the variant mentions judge; remove it and propose again.\nNext: sindri evolve reject " + leaky + ' --reason "leaks the evaluation"\n');
    expect(status(fx, leaky)).toBe("lost");
    const noClause = save({ title: "A draft without the clause", change: { type: "replace-prompt", text: "Write a scope map." } });
    expect((await compare([noClause], fx.ctx)).stdout).toBe(`missing-safety-clause: the variant must keep this line: ${SOURCES_CLAUSE}\nNext: sindri evolve reject ${noClause} --reason "dropped the safety clause"\n`);
    fx.close();
  });

  it("reports an inconclusive result and a budget stop, and leaves neither as insufficient-corpus", async () => {
    const tied = await ready(22, { io: scriptedEvolveIo((c) => (c.model === "sonnet" ? map("plain") : { winner: "tie", reasons: [] })) });
    const id = tied.save({ title: "Tied scope draft prompt" });
    const r = await compare([id], tied.fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toBe("inconclusive: only 0 of 22 pairs were decided (need 10); 22 ties.\nNext: sindri evolve show " + id + "\n");
    expect(status(tied.fx, id)).toBe("proposed");
    expect((await compare([id], tied.fx.ctx)).stdout).toContain("Stored result (run 1): inconclusive");
    tied.fx.close();
    const { fx, save } = await ready(22, { extraYaml: "evolve:\n  maxTokensPerCompare: 8\n" });
    const cutId = save();
    const cut = await compare([cutId], fx.ctx);
    expect(cut.exitCode).toBe(1);
    expect(cut.stdout).toContain("incomplete: the token budget (8) ran out after 1 item(s); nothing was decided.");
    expect(cut.stdout).toContain("Next: raise evolve.maxTokensPerCompare in the profile (then sindri profile approve), then rerun sindri evolve compare");
    expect(status(fx, cutId)).toBe("proposed");
    fx.close();
  });

  it("reports a lost comparison with its errored items, and keeps a lost proposal lost when a rerun decides nothing", async () => {
    const items = [item(holdoutIds[0], "boom"), ...holdoutIds.slice(1, 22).map((id) => item(id))];
    const { fx, save } = await ready(0, { io: scriptedEvolveIo(preferPlain), items });
    const id = save();
    const r = await compare([id], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(`lost: 0 of 21 decided pairs (win rate 0.00, lower bound 0.00) on 22 holdout items; 1 ties. 1 item(s) errored (a failure on the variant alone counts as a loss, any other as a tie).\nNext: sindri evolve reject ${id} --reason "lost the comparison"\n`);
    expect(status(fx, id)).toBe("lost");
    expect(fx.ctx.db.prepare("SELECT detail FROM comparisons WHERE proposal_id = ? AND item_id = ?").get(id, holdoutIds[0])).toEqual({ detail: '{"reason":"model exploded"}' });
    fx.ctx.db.prepare("UPDATE comparisons SET verdict = 'incomplete' WHERE proposal_id = ? AND item_id = '*'").run(id); // as if the last run had stopped early
    const dead = { ...fx.ctx, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, evolve: { ...fx.ctx.loaded.profile.evolve, maxTokensPerCompare: 8 } } } };
    const rerun = await compare([id], dead);
    expect(rerun.stdout).toContain("incomplete:");
    expect(status(fx, id)).toBe("lost");
    fx.close();
  });

  it("sends a won proposal back to proposed when its rerun is inconclusive", async () => {
    const fx = await evolveFixture({ io: scriptedEvolveIo(script) });
    for (const hid of holdoutIds.slice(0, 22)) seed(fx, item(hid));
    const id = saver(fx)();
    expect((await compare([id], fx.ctx)).exitCode).toBe(0);
    const tied = { ...fx.ctx, io: scriptedEvolveIo((c) => (c.model === "sonnet" ? map("plain") : { winner: "tie", reasons: [] })) };
    expect((await compare([id, "--rerun"], tied)).stdout).toContain("inconclusive:");
    expect(status(fx, id)).toBe("proposed");
    fx.close();
  });

  it("puts the proposal back to its earlier status when the comparison throws", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    const m = fx.ctx.loaded.profile.models;
    const sameJudge = { ...fx.ctx, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, models: { ...m, adjudicator: m.scoping } } } };
    await expect(compare([id], sameJudge)).rejects.toThrow(/judge model must differ/);
    expect(status(fx, id)).toBe("proposed");
    expect((await compare([id], fx.ctx)).exitCode).toBe(0); // and the same proposal can still be compared
    await expect(compare([id, "--rerun"], sameJudge)).rejects.toThrow(/judge model must differ/);
    expect(status(fx, id)).toBe("won");
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, "evaluating", epoch, fx.deps.now()));
    await expect(compare([id, "--rerun"], sameJudge)).rejects.toThrow(/judge model must differ/);
    expect(status(fx, id)).toBe("proposed");
    fx.close();
  });

  it("refuses unknown proposals, other artifacts, prompt-less proposals, a missing id, and proposals that are done", async () => {
    const { fx, save } = await ready(0);
    await expect(compare(["nope"], fx.ctx)).rejects.toThrow(/no such proposal: nope/);
    await expect(compare([], fx.ctx)).rejects.toThrow(/usage: sindri evolve compare <id>/);
    const other = save({ artifact: "prompt:scope.challenger", title: "Challenger prompt tweak" });
    await expect(compare([other], fx.ctx)).rejects.toThrow(/no offline comparison for prompt:scope.challenger yet/);
    const code = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "A skill edit", rationale: "r", evidence: [], change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "s", "code", epoch, fx.deps.now()).id);
    await expect(compare([code], fx.ctx)).rejects.toThrow(/no offline comparison for skill:review yet/);
    const mine = save();
    for (const done of ["rejected", "adopted", "merged"] as const) {
      fx.ctx.write((epoch) => setStatus(fx.ctx.db, mine, done, epoch, fx.deps.now()));
      await expect(compare([mine], fx.ctx)).rejects.toThrow(new RegExp(`is ${done}; it is not compared again`));
    }
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM comparisons").get()).toEqual({ c: 0 });
    fx.close();
  });

  it("refuses a second plain compare while one is running, numbers overlapping --rerun runs apart, and judges 'latest' by run number", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    let release: () => void = () => undefined;
    const gate = new Promise<void>((r) => { release = r; });
    const inner = fx.ctx.io.runner(fx.ctx.loaded, undefined as never);
    const gated = { ...fx.ctx, io: { ...fx.ctx.io, runner: () => ({ run: async <T,>(c: Parameters<typeof inner.run<T>>[0]) => { await gate; return inner.run(c); } }) } };
    const first = compare([id, "--json"], gated);
    await expect(compare([id], gated)).rejects.toThrow(/already running \(run 1\)/);
    const second = compare([id, "--rerun", "--json"], gated);
    release();
    const runs = (await Promise.all([first, second])).map((r) => (JSON.parse(r.stdout) as { run: number }).run).sort();
    expect(runs).toEqual([1, 2]);
    // A slow run 1 that finishes after run 2 started doesn't make run 1 the latest.
    const other = save({ title: "Another draft prompt" });
    const row = (run: number, verdict: string, detail: string) =>
      fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', ?, ?, 't', 1)").run(other, run, verdict, detail);
    row(2, "running", "{}");
    row(1, "won", JSON.stringify({ line: "won: late" }));
    await expect(compare([other], fx.ctx)).rejects.toThrow(/already running \(run 2\)/);
    fx.close();
  });

  it("shows a proposal during a comparison and after a failed one, instead of failing on the running row", async () => {
    const { fx, save } = await ready(22);
    await init([], fx.ctx);
    const id = save();
    fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, 1, '*', 'running', '{}', 't', 1)").run(id);
    expect((await show([id], fx.ctx)).stdout).toContain("A comparison is running (run 1)");
    fx.ctx.db.prepare("DELETE FROM comparisons").run();
    const m = fx.ctx.loaded.profile.models;
    const sameJudge = { ...fx.ctx, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, models: { ...m, adjudicator: m.scoping } } } };
    await expect(compare([id], sameJudge)).rejects.toThrow(/judge model must differ/);
    const shown = (await show([id], fx.ctx)).stdout;
    expect(shown).toContain("Comparison (run 1): errored: the judge model must differ");
    expect(shown).not.toContain("is running");
    expect((await compare([id], fx.ctx)).exitCode).toBe(0); // an errored run doesn't block the next
    fx.close();
  });

  it("scrubs the message of a failed comparison before storing it", async () => {
    const { fx, save } = await ready(22);
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const id = save();
    const boom = { ...fx.ctx, io: { ...fx.ctx.io, runner: () => { throw new Error(`no runner ${secret}`); } } };
    await expect(compare([id], boom)).rejects.toThrow();
    const row = fx.ctx.db.prepare("SELECT detail FROM comparisons WHERE proposal_id = ? AND verdict = 'errored'").get(id) as { detail: string };
    expect(row.detail).not.toContain(secret);
    fx.close();
  });

  it("names the cause of an inconclusive result: too many errors, too few decided pairs, or both", async () => {
    const items = [...holdoutIds.slice(0, 3).map((id) => item(id, "boom")), ...holdoutIds.slice(3, 22).map((id) => item(id))];
    const errs = await ready(0, { io: scriptedEvolveIo(script), items });
    const r = await compare([errs.save()], errs.fx.ctx);
    expect(r.stdout).toContain("inconclusive: 3 of 22 items errored, more than 10%; 3 ties. 3 item(s) errored");
    errs.fx.close();
    const both = await ready(0, { io: scriptedEvolveIo((c) => (c.model === "sonnet" ? script(c) : { winner: "tie", reasons: [] })), items });
    expect((await compare([both.save()], both.fx.ctx)).stdout).toContain("inconclusive: 3 of 22 items errored, more than 10%; only 0 of 22 pairs were decided (need 10); 22 ties.");
    both.fx.close();
  });

  it("records a failure that isn't an Error object", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    const odd = { ...fx.ctx, io: { ...fx.ctx.io, runner: () => { throw "plain string"; } } };
    await expect(compare([id], odd)).rejects.toBe("plain string");
    expect(fx.ctx.db.prepare("SELECT detail FROM comparisons WHERE proposal_id = ? AND verdict = 'errored'").get(id)).toEqual({ detail: '{"line":"errored: plain string"}' });
    fx.close();
  });

  it("leaves the proposal status to the highest run when overlapping --rerun runs finish out of order", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    let release: () => void = () => undefined;
    const gate = new Promise<void>((r) => { release = r; });
    const loser = scriptedEvolveIo(preferPlain).runner(fx.ctx.loaded, undefined as never);
    const winner = fx.ctx.io.runner(fx.ctx.loaded, undefined as never);
    let made = 0;
    const lagging = { run: async <T,>(c: Parameters<typeof loser.run<T>>[0]) => { await gate; return loser.run(c); } };
    const ctx = { ...fx.ctx, io: { ...fx.ctx.io, runner: () => (made++ === 0 ? lagging : winner) } };
    const first = compare([id, "--rerun", "--json"], ctx);
    const started = (): number => (fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM comparisons WHERE proposal_id = ?").get(id) as { c: number }).c;
    while (started() === 0) await new Promise((r) => setTimeout(r, 1));
    const second = JSON.parse((await compare([id, "--rerun", "--json"], ctx)).stdout) as { run: number; status: string };
    expect(second).toMatchObject({ run: 2, status: "won" });
    expect(status(fx, id)).toBe("won");
    release();
    expect(JSON.parse((await first).stdout)).toMatchObject({ run: 1, status: "lost" });
    expect(status(fx, id)).toBe("won"); // run 1 finished last but is not the highest run
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM comparisons WHERE proposal_id = ? AND run = 1 AND verdict = 'lost'").get(id)).toEqual({ c: 1 });
    fx.close();
  });

  it("does not resurrect a proposal rejected while its comparison was running, whether that comparison finishes or throws", async () => {
    const { fx, save } = await ready(22);
    const rejectOnce = (): ((m: string) => void) => {
      let done = false;
      return () => {
        if (done) return;
        done = true;
        const mine = fx.ctx.db.prepare("SELECT proposal_id AS p FROM comparisons WHERE verdict = 'running'").all() as { p: string }[];
        for (const { p } of mine) fx.ctx.write((epoch) => setStatus(fx.ctx.db, p, "rejected", epoch, fx.deps.now()));
      };
    };
    const win = save();
    const finishing = { ...fx.ctx, deps: { ...fx.ctx.deps, log: rejectOnce() } };
    expect((await compare([win], finishing)).exitCode).toBe(0);
    expect(status(fx, win)).toBe("rejected");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM comparisons WHERE proposal_id = ? AND item_id = '*' AND verdict = 'won'").get(win)).toEqual({ c: 1 });
    const thrower = save({ title: "Another draft prompt" });
    const rejectThenThrow = rejectOnce();
    const throwing = { ...fx.ctx, deps: { ...fx.ctx.deps, log: (m: string) => { rejectThenThrow(m); throw new Error("log exploded"); } } };
    await expect(compare([thrower], throwing)).rejects.toThrow(/log exploded/);
    expect(status(fx, thrower)).toBe("rejected");
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM comparisons WHERE proposal_id = ? AND verdict = 'errored'").get(thrower)).toEqual({ c: 1 });
    fx.close();
  });

  it("surfaces the original error when the closing write of a failed comparison also fails", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    const m = fx.ctx.loaded.profile.models;
    let calls = 0;
    const writeRetry: typeof fx.ctx.writeRetry = (fn) => {
      if (++calls > 1) throw new Error("lock gone");
      return fx.ctx.writeRetry(fn);
    };
    const bad = { ...fx.ctx, writeRetry, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, models: { ...m, adjudicator: m.scoping } } } };
    await expect(compare([id], bad)).rejects.toThrow(/judge model must differ/);
    fx.close();
  });

  it("caps the stored errored message at 500 characters", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    const long = { ...fx.ctx, io: { ...fx.ctx.io, runner: () => { throw new Error("x".repeat(600)); } } };
    await expect(compare([id], long)).rejects.toThrow();
    expect(fx.ctx.db.prepare("SELECT detail FROM comparisons WHERE proposal_id = ? AND verdict = 'errored'").get(id)).toEqual({ detail: JSON.stringify({ line: `errored: ${"x".repeat(500)}` }) });
    fx.close();
  });

  it("scrubs the reason stored for an errored item", async () => {
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const items = [item(holdoutIds[0], "boom"), ...holdoutIds.slice(1, 22).map((id) => item(id))];
    const io = scriptedEvolveIo((c) => {
      if (c.model === "sonnet" && c.input.includes("boom")) throw new Error(`model exploded ${secret}`);
      return script(c);
    });
    const { fx, save } = await ready(0, { io, items });
    const id = save();
    await compare([id], fx.ctx);
    const row = fx.ctx.db.prepare("SELECT detail FROM comparisons WHERE proposal_id = ? AND item_id = ?").get(id, holdoutIds[0]) as { detail: string };
    expect(row.detail).toContain("model exploded");
    expect(row.detail).not.toContain(secret);
    fx.close();
  });

  it("shows the running run in --json", async () => {
    const { fx, save } = await ready(22);
    await init([], fx.ctx);
    const id = save();
    const running = async () => (JSON.parse((await show([id, "--json"], fx.ctx)).stdout) as { running: number | null }).running;
    expect(await running()).toBeNull();
    fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, 3, '*', 'running', '{}', 't', 1)").run(id);
    expect(await running()).toBe(3);
    fx.close();
  });

  it("closes every earlier running row when --rerun starts, so a won rerun can be adopted after a killed run (I1)", async () => {
    const { fx, save } = await ready(22);
    const id = save();
    // A killed compare: its `running` marker is in the ledger, nothing closed it, and the proposal is `evaluating`.
    fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, 1, '*', 'running', '{}', 't', 1)").run(id);
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, "evaluating", epoch, fx.deps.now()));
    expect(runningComparison(fx.ctx.db, id)).toBe(1);
    const r = await compare([id, "--rerun", "--json"], fx.ctx);
    expect(JSON.parse(r.stdout)).toMatchObject({ run: 2, status: "won" });
    expect(runningComparison(fx.ctx.db, id)).toBeNull();
    expect(fx.ctx.db.prepare("SELECT run, verdict, detail FROM comparisons WHERE proposal_id = ? AND item_id = '*' AND verdict = 'errored'").all(id)).toEqual([
      { run: 1, verdict: "errored", detail: JSON.stringify({ line: "superseded by run 2" }) },
    ]);
    const sha8 = createHash("sha256").update(variant).digest("hex").slice(0, 8);
    const adopted = await adopt([id], withDeps(fx.ctx, { isTTY: true, prompt: async () => sha8 }));
    expect(adopted.exitCode).toBe(0);
    expect(status(fx, id)).toBe("adopted");
    fx.close();
  });
});
