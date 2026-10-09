import { createHash } from "node:crypto";
import fs from "node:fs";
import { describe, expect, it } from "vitest";

import { adopt, revert } from "../src/evolve/cmd/adopt.js";
import { inspectOverlay, loadPrompt, overlayFile } from "../src/evolve/overlay.js";
import { defaultPrompt, SOURCES_CLAUSE } from "../src/evolve/prompts.js";
import { getProposal, ProposalSchema, saveProposal, setStatus, type ProposalStatus } from "../src/evolve/proposals.js";
import { evolveFixture, withDeps, type EvolveFixture } from "./evolve-fixtures.js";

const VARIANT = `${SOURCES_CLAUSE}\nBETTER draft prompt.`;
const sha8 = (t: string): string => createHash("sha256").update(t).digest("hex").slice(0, 8);

async function ready() {
  const fx = await evolveFixture();
  const asked: string[] = [];
  let answer = sha8(VARIANT);
  const tty = withDeps(fx.ctx, { isTTY: true, prompt: async (q: string) => { asked.push(q); return answer; } });
  const save = (title: string, text: string, status: ProposalStatus, over: Record<string, unknown> = {}, won: "won" | "lost" | null = "won") => {
    const id = fx.ctx.write((epoch) =>
      saveProposal(fx.ctx.db, ProposalSchema.parse({
        artifact: "prompt:scope.draft", kind: "prompt-edit", title, rationale: "r", evidence: ["pr:1"],
        change: { type: "replace-prompt", text }, ...over,
      }), "s", "self-adopt", epoch, fx.deps.now()).id);
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, status, epoch, fx.deps.now()));
    if (won !== null) fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, 1, '*', ?, ?, 't', 1)").run(id, won, JSON.stringify({ line: "won: 20 of 22 decided pairs" }));
    return id;
  };
  return { fx, tty, asked, save, answerWith: (a: string) => { answer = a; } };
}

describe("sindri evolve adopt (Review Focus 7)", () => {
  it("shows the diff and the comparison, takes a typed confirmation bound to the variant's hash, writes the overlay and records the hash", async () => {
    const { fx, tty, asked, save } = await ready();
    const id = save("Better draft prompt", VARIANT, "won");
    const r = await adopt([id], tty);
    expect(r.exitCode).toBe(0);
    const file = overlayFile(fx.deps, "scope.draft");
    expect(r.stdout).toBe(`Adopted ${id}: wrote ${file} (sha256 ${sha8(VARIANT)}). It takes effect on the next sindri scope run.\nNext: sindri evolve status\n`);
    expect(asked[0]).toContain("Adopt this variant for scope.draft?");
    expect(asked[0]).toContain("+ BETTER draft prompt.");
    expect(asked[0]).toContain("- You scope a software project before work starts.");
    expect(asked[0]).toContain("Comparison: won: 20 of 22 decided pairs");
    expect(asked[0]).toContain(`first 8 characters of the variant's sha256 (${sha8(VARIANT)})`);
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    expect(loadPrompt(fx.deps, "scope.draft")).toBe(VARIANT);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("adopted");
    expect(fx.ctx.db.prepare("SELECT prompt_id, proposal_id, sha256, adopted_by FROM adoptions").all()).toEqual([
      { prompt_id: "scope.draft", proposal_id: id, sha256: createHash("sha256").update(VARIANT).digest("hex"), adopted_by: fx.deps.system.username() },
    ]);
    expect(fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([{ verb: "adopt" }]);
    fx.close();
  });

  it("refuses without a terminal, a wrong confirmation, or anything that isn't a won comparison of a clean variant", async () => {
    const { fx, tty, save, answerWith } = await ready();
    const won = save("Better draft prompt", VARIANT, "won");
    await expect(adopt([won], fx.ctx)).rejects.toThrow(/needs an interactive terminal/);
    answerWith("deadbeef");
    await expect(adopt([won], tty)).rejects.toThrow(/confirmation didn't match/);
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("none");
    await expect(adopt(["nope"], tty)).rejects.toThrow(/no such proposal: nope/);
    await expect(adopt([], tty)).rejects.toThrow(/usage: sindri evolve adopt <id>/);
    const cases: [string, string][] = [
      [save("Not yet compared", VARIANT, "proposed"), "this one is proposed"],
      [save("A skill edit of some sort", VARIANT, "won", { artifact: "skill:review", change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" } }), "this one is won"],
      [save("Not a prompt artifact", VARIANT, "won", { artifact: "skill:review" }), "isn't a prompt artifact"],
      [save("Never compared at all", `${VARIANT} one`, "won", {}, null), "no won comparison is on record"],
      [save("Lost its comparison", `${VARIANT} two`, "won", {}, "lost"), "no won comparison is on record"],
      [save("Dropped the clause", "Write a scope map.", "won"), "keep the safety clause and pass the leak check"],
      [save("Leaks the evaluation", `${VARIANT} The judge rewards this.`, "won"), "keep the safety clause and pass the leak check"],
    ];
    for (const [id, why] of cases) await expect(adopt([id], tty), why).rejects.toThrow(why);
    fx.close();
  });
});

describe("sindri evolve revert", () => {
  it("removes the overlay, records a revert so an old file can't come back, and needs a terminal", async () => {
    const { fx, tty, save } = await ready();
    const id = save("Better draft prompt", VARIANT, "won");
    await adopt([id], tty);
    await expect(revert(["scope.draft"], fx.ctx)).rejects.toThrow(/needs an interactive terminal/);
    const r = await revert(["scope.draft"], tty);
    expect(r.stdout).toBe("Reverted scope.draft to the built-in prompt.\nNext: sindri evolve status\n");
    expect(loadPrompt(fx.deps, "scope.draft")).toBe(defaultPrompt("scope.draft"));
    fs.writeFileSync(overlayFile(fx.deps, "scope.draft"), VARIANT, { mode: 0o600 }); // an attacker restores the old file
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("unadopted");
    expect((await revert(["scope.draft"], tty)).stdout).toBe("Reverted scope.draft to the built-in prompt.\nNext: sindri evolve status\n");
    expect((await revert(["scope.draft"], tty)).stdout).toBe("There is no overlay for scope.draft; nothing to revert.\nNext: sindri evolve status\n");
    await expect(revert(["nope"], tty)).rejects.toThrow(/usage: sindri evolve revert <prompt-id>/);
    await expect(revert([], tty)).rejects.toThrow(/usage/);
    expect(fx.ctx.db.prepare("SELECT verb FROM evolve_audit ORDER BY seq").all()).toEqual([{ verb: "adopt" }, { verb: "revert" }, { verb: "revert" }]);
    fx.close();
  });
});

describe("sindri evolve adopt (Task 10 rulings)", () => {
  const row = (fx: EvolveFixture, id: string, run: number, verdict: string, line = `${verdict}: x`): void => {
    fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', ?, ?, 't', 1)").run(id, run, verdict, JSON.stringify({ line }));
  };

  it("refuses when the latest run errored, lost or is still running, even after an earlier won run", async () => {
    const { fx, tty, save } = await ready();
    for (const [verdict, why] of [["errored", "no won comparison is on record for this proposal (the latest, run 2, is errored)"], ["lost", "no won comparison is on record for this proposal (the latest, run 2, is lost)"], ["inconclusive", "no won comparison is on record for this proposal (the latest, run 2, is inconclusive)"]] as const) {
      const id = save(`Rerun after a win ${verdict}`, `${VARIANT} ${verdict}`, "won");
      row(fx, id, 2, verdict);
      await expect(adopt([id], tty), verdict).rejects.toThrow(why);
    }
    const running = save("Rerun still in progress", `${VARIANT} busy`, "won");
    row(fx, running, 2, "running", "");
    await expect(adopt([running], tty)).rejects.toThrow(/comparison is still running \(run 2\)/);
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("none");
    fx.close();
  });

  it("shows how many comparison runs the proposal has had", async () => {
    const { fx, tty, asked, save, answerWith } = await ready();
    const text = `${VARIANT} counted`;
    const id = save("Counted variant", text, "won");
    row(fx, id, 2, "won", "won: 21 of 22 decided pairs");
    row(fx, id, 3, "running", "");
    row(fx, id, 3, "won", "won: 22 of 22 decided pairs");
    answerWith(sha8(text));
    await adopt([id], tty);
    expect(asked[0]).toContain("Comparison: won: 22 of 22 decided pairs");
    expect(asked[0]).toContain("Comparison runs so far: 3 (a variant that kept rerunning until it won is not evidence; check the history in sindri evolve show)");
    fx.close();
  });

  it("hashes the decoded UTF-8 text inspectOverlay verifies, so a non-ASCII or lone-surrogate variant is active after adoption", async () => {
    const { fx, tty, save, answerWith } = await ready();
    const text = `${VARIANT} Caf\u00e9 \u2013 na\u00efve \u{1F600} and a stray \uD800 half.`;
    const decoded = Buffer.from(text, "utf8").toString("utf8");
    const id = save("Unicode variant text", text, "won");
    answerWith(sha8(decoded));
    const r = await adopt([id], tty);
    expect(r.exitCode).toBe(0);
    expect(inspectOverlay(fx.deps, "scope.draft")).toEqual({ state: "active", text: decoded });
    expect(fx.ctx.db.prepare("SELECT sha256 FROM adoptions").all()).toEqual([{ sha256: createHash("sha256").update(decoded).digest("hex") }]);
    fx.close();
  });
});
