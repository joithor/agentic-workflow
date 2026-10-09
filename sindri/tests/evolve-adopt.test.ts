import { createHash } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { adopt, revert } from "../src/evolve/cmd/adopt.js";
import { inspectOverlay, loadPrompt, overlayFile } from "../src/evolve/overlay.js";
import { defaultPrompt, SOURCES_CLAUSE } from "../src/evolve/prompts.js";
import { getProposal, ProposalSchema, saveProposal, type ProposalStatus } from "../src/evolve/proposals.js";
import { isHoldout, saveReplay, type ReplayItem } from "../src/evolve/corpus.js";
import { evolveFixture, withDeps, type EvolveFixture, setStatus } from "./evolve-fixtures.js";

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

  it("keeps the previously adopted overlay when the ledger write fails (the rename is the last step)", async () => {
    const { fx, tty, save, answerWith } = await ready();
    const a = save("Better draft prompt", VARIANT, "won");
    await adopt([a], tty);
    const second = `${VARIANT}\nEven BETTER.`;
    const b = save("Even better prompt", second, "won");
    fx.ctx.db.exec("CREATE TRIGGER no_adoptions BEFORE INSERT ON adoptions BEGIN SELECT RAISE(ABORT, 'disk full'); END");
    answerWith(sha8(second));
    await expect(adopt([b], tty)).rejects.toThrow(/disk full/);
    expect(loadPrompt(fx.deps, "scope.draft")).toBe(VARIANT);
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("active"); // still the variant the latest adoption row names
    expect(getProposal(fx.ctx.db, b)?.status).toBe("won");
    fx.close();
  });

  it("restores the previous overlay (or removes the new one) when the write fails after the rename", async () => {
    const { fx, tty, save, answerWith } = await ready();
    const failing = { ...tty, write: <T>(fn: (epoch: number) => T): T => { fx.ctx.write(fn); throw new Error("commit failed"); } };
    const a = save("Better draft prompt", VARIANT, "won");
    await expect(adopt([a], failing)).rejects.toThrow(/commit failed/);
    expect(fs.existsSync(overlayFile(fx.deps, "scope.draft"))).toBe(false);
    fx.ctx.write((epoch) => setStatus(fx.ctx.db, a, "won", epoch, fx.deps.now())); // the fake commit above did persist the row
    await adopt([a], tty);
    const second = `${VARIANT}\nEven BETTER.`;
    const b = save("Even better prompt", second, "won");
    answerWith(sha8(second));
    await expect(adopt([b], failing)).rejects.toThrow(/commit failed/);
    expect(fs.readFileSync(overlayFile(fx.deps, "scope.draft"), "utf8")).toBe(VARIANT);
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

describe("sindri evolve adopt: state changes while the human confirms (Task 10 fix round 1, I2)", () => {
  const insert = (fx: EvolveFixture, id: string, run: number, verdict: string, line = `${verdict}: x`): void => {
    fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', ?, ?, 't', 1)").run(id, run, verdict, JSON.stringify({ line }));
  };
  type Change = (fx: EvolveFixture, id: string) => void;
  const CHANGES: [string, Change][] = [
    ["the proposal is rejected", (fx, id) => fx.ctx.write((e) => setStatus(fx.ctx.db, id, "rejected", e, fx.deps.now()))],
    ["a rerun starts (status evaluating, a running marker)", (fx, id) => { fx.ctx.write((e) => setStatus(fx.ctx.db, id, "evaluating", e, fx.deps.now())); insert(fx, id, 2, "running", ""); }],
    ["a running marker appears", (fx, id) => insert(fx, id, 2, "running", "")],
    ["a newer run lost", (fx, id) => insert(fx, id, 2, "lost")],
    ["a newer run errored", (fx, id) => insert(fx, id, 2, "errored")],
    ["the variant text changes", (fx, id) => {
      const body = JSON.parse((fx.ctx.db.prepare("SELECT body FROM proposals WHERE id = ?").get(id) as { body: string }).body) as { change: { text: string } };
      body.change.text += " tampered";
      fx.ctx.db.prepare("UPDATE proposals SET body = ? WHERE id = ?").run(JSON.stringify(body), id);
    }],
  ];

  for (const [name, change] of CHANGES) {
    it(`aborts with no overlay written when ${name} between the confirmation and the write`, async () => {
      const { fx, save } = await ready();
      const id = save(`Racing variant ${name.length}`, `${VARIANT} racing`, "won");
      const before = getProposal(fx.ctx.db, id)?.status;
      const tty = withDeps(fx.ctx, { isTTY: true, prompt: async () => { change(fx, id); return sha8(`${VARIANT} racing`); } });
      await expect(adopt([id], tty)).rejects.toThrow(/SND-EVOLVE-004|changed while you were confirming|only a won|no won comparison|still running/);
      expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("none");
      expect(fs.existsSync(overlayFile(fx.deps, "scope.draft"))).toBe(false);
      expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM adoptions").get()).toEqual({ c: 0 });
      expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit WHERE verb = 'adopt'").get()).toEqual({ c: 0 });
      const after = getProposal(fx.ctx.db, id)?.status;
      expect(after).not.toBe("adopted");
      if (!name.startsWith("the proposal is rejected") && !name.startsWith("a rerun")) expect(after).toBe(before);
      fx.close();
    });
  }

  it("names the change when the won run is no longer the one that was shown", async () => {
    const { fx, save } = await ready();
    const id = save("Racing variant message", `${VARIANT} message`, "won");
    const tty = withDeps(fx.ctx, { isTTY: true, prompt: async () => { insert(fx, id, 2, "won", "won: 22 of 22 decided pairs"); return sha8(`${VARIANT} message`); } });
    await expect(adopt([id], tty)).rejects.toThrow(/changed while you were confirming \(shown run 1, now run 2\)/);
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("none");
    fx.close();
  });
});

describe("sindri evolve adopt: the diff is safe to read (Task 10 fix round 1, I3)", () => {
  it("shows ESC sequences, CR and bidi controls as visible \\u{XXXX} escapes", async () => {
    const { fx, tty, asked, save, answerWith } = await ready();
    const text = `${VARIANT}\n\u001b[1A\u001b[2Kinnocent line\nfirst\rsecond\nrtl \u202eevil\u2066 iso\u2069 \u200b zero \u0085 \u007f`;
    const id = save("Hostile looking variant", text, "won");
    answerWith(sha8(text));
    await adopt([id], tty);
    const q = asked[0];
    expect(q).not.toMatch(/[\u0000-\u0009\u000b-\u001f\u007f-\u009f\u200b-\u200f\u202a-\u202e\u2060-\u206f]/u);
    for (const esc of ["\\u{001B}[1A\\u{001B}[2Kinnocent line", "first\\u{000D}second", "\\u{202E}evil\\u{2066}", "iso\\u{2069}", "\\u{200B}", "\\u{0085}", "\\u{007F}"]) expect(q, esc).toContain(esc);
    fx.close();
  });
});

describe("sindri evolve adopt: astral and format invisibles (Task 10 fix round 2, I4)", () => {
  it("shows tag characters, variation selectors and U+061C as visible escapes", async () => {
    const { fx, tty, asked, save, answerWith } = await ready();
    const text = `${VARIANT}\nhid\u{E0041}den \ufe0f vs \u{E0100} alm \u061c end\n\ttabbed`;
    const id = save("Smuggling variant", text, "won");
    answerWith(sha8(text));
    await adopt([id], tty);
    const q = asked[0];
    expect(q).not.toMatch(/[\u{E0000}-\u{E007F}\u{E0100}-\u{E01EF}\ufe00-\ufe0f\u061c]/u);
    expect(q).toContain("\ttabbed");
    for (const esc of ["hid\\u{E0041}den", "\\u{FE0F}", "\\u{E0100}", "\\u{061C}"]) expect(q, esc).toContain(esc);
    fx.close();
  });
});

describe("sindri evolve adopt: holdout titles and the overlay directory (Task 10 fix round 1, m10 + m12)", () => {
  const holdoutId = Array.from({ length: 600 }, (_, i) => `item-${i}`).find(isHoldout) as string;
  const it1: ReplayItem = {
    id: holdoutId, artifact: "scope.draft", createdAt: "2026-10-08T00:00:00Z",
    brief: { ref: "file:/b.md", kind: "brief", title: "Quarterly onboarding revamp", text: "b", author: null, createdAt: null, trust: "trusted" },
    records: [], outcome: { status: "complete", surfaces: 1, recall: null },
  };

  it("refuses a variant that copies a holdout title (m10)", async () => {
    const { fx, tty, save, answerWith } = await ready();
    fx.ctx.db.prepare("INSERT OR IGNORE INTO scope_runs (run_id, subject, mode, ts, status, rounds, surfaces, tokens, out_path, epoch) VALUES (?, 's', 'scope', 't', 'complete', 1, 1, 1, '/o', 1)").run(it1.id);
    saveReplay(fx.deps, it1);
    const text = `${VARIANT} Think of the quarterly onboarding revamp when drafting.`;
    const id = save("Copies a holdout title", text, "won");
    answerWith(sha8(text));
    await expect(adopt([id], tty)).rejects.toThrow(/keep the safety clause and pass the leak check/);
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("none");
    fx.close();
  });

  it("refuses when the overlay directory is a symlink, and writes nothing (m12)", async () => {
    const { fx, tty, save, answerWith } = await ready();
    const other = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-elsewhere-"));
    const prompts = path.dirname(overlayFile(fx.deps, "scope.draft"));
    fs.mkdirSync(path.dirname(prompts), { recursive: true });
    fs.symlinkSync(other, prompts);
    const text = `${VARIANT} symlinked`;
    const id = save("Symlinked overlay dir", text, "won");
    answerWith(sha8(text));
    await expect(adopt([id], tty)).rejects.toThrow(/isn't a plain directory/);
    expect(fs.readdirSync(other)).toEqual([]);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM adoptions").get()).toEqual({ c: 0 });
    expect(getProposal(fx.ctx.db, id)?.status).toBe("won");
    fs.rmSync(prompts);
    fs.rmSync(path.dirname(prompts), { recursive: true });
    fs.symlinkSync(other, path.dirname(prompts));
    await expect(adopt([id], tty)).rejects.toThrow(/isn't a plain directory/);
    expect(fs.readdirSync(other)).toEqual([]);
    fx.close();
  });
});

describe("sindri evolve revert: an overlay already deleted by hand (Task 10 fix round 1, m5)", () => {
  it("records a reverted row anyway, so a restored file stays unadopted, and says what happened", async () => {
    const { fx, tty, save } = await ready();
    const id = save("Better draft prompt", VARIANT, "won");
    await adopt([id], tty);
    fs.rmSync(overlayFile(fx.deps, "scope.draft")); // deleted by hand
    const r = await revert(["scope.draft"], tty);
    expect(r.stdout).toBe("Reverted scope.draft to the built-in prompt (the overlay file was already gone; the adoption is now recorded as reverted).\nNext: sindri evolve status\n");
    expect(JSON.parse((await revert(["scope.draft", "--json"], tty)).stdout)).toMatchObject({ reverted: false });
    fs.writeFileSync(overlayFile(fx.deps, "scope.draft"), VARIANT, { mode: 0o600 }); // a backup restore
    expect(inspectOverlay(fx.deps, "scope.draft").state).toBe("unadopted");
    expect(fx.ctx.db.prepare("SELECT sha256 FROM adoptions ORDER BY seq").all()).toEqual([{ sha256: createHash("sha256").update(VARIANT).digest("hex") }, { sha256: "reverted" }]);
    const audits = fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit WHERE verb = 'revert'").all() as { detail: string }[];
    expect(audits).toHaveLength(1);
    expect(audits[0].detail).toBe(`scope.draft (adopted proposal ${id} keeps status adopted: there is no reverted status)`);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("adopted");
    fx.close();
  });
});

