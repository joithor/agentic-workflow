import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { audit } from "../src/evolve/audit.js";
import { proposals, reject, show, tier } from "../src/evolve/cmd/proposals.js";
import { init } from "../src/evolve/cmd/registry.js";
import { status } from "../src/evolve/cmd/status.js";
import { ProposalSchema, saveProposal, stagedFile, type Proposal, type ProposalStatus } from "../src/evolve/proposals.js";
import { evolveFixture, git, setStatus } from "./evolve-fixtures.js";

const FILES = {
  "config/hooks/done-gate.sh": "#!/bin/sh\n",
  "skills/review/SKILL.md": "x\n",
  "sindri/package.json": "{}",
  "sindri/src/observe/observe.ts": "export const a = 1;\n",
  "sindri/tests/existing.test.ts": "export {};\n",
  "sindri/tests/helpers.ts": "export const h = 1;\n",
  "sindri/src/scrub/patterns.ts": "export const b = 0;\n",
};

const prop = (over: Record<string, unknown> = {}): Proposal =>
  ProposalSchema.parse({
    artifact: "skill:review", kind: "skill-edit", title: "Tighten review scope", rationale: "Seen twice.\nSecond line.", evidence: ["pr:12", "-Users-joi-app/5e55a1d0-1.jsonl#4"],
    change: { type: "describe", files: ["skills/review/SKILL.md"], description: "Add a step." }, ...over,
  });

async function ready(extraYaml?: string) {
  const fx = await evolveFixture({ files: FILES, extraYaml });
  await init([], fx.ctx);
  let n = 0; // a clock that moves, so created_at orders the proposals
  const save = (over: Record<string, unknown>, tierName: "code" | "approval" | "self-adopt" = "code", status?: ProposalStatus) => {
    const id = fx.ctx.write((epoch) => saveProposal(fx.ctx.db, prop(over), "reflect:pr-12", tierName, epoch, new Date(Date.parse("2026-10-08T12:00:00Z") + n++ * 1000)).id);
    if (status !== undefined) fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, status, epoch, fx.deps.now()));
    return id;
  };
  return { fx, save };
}

describe("sindri evolve proposals", () => {
  it("lists open proposals by default, all with --all, and filters by status", async () => {
    const { fx, save } = await ready();
    expect((await proposals([], fx.ctx)).stdout).toBe("No proposals yet.\nNext: sindri evolve reflect --pr <n>\n");
    const a = save({ title: "First proposal here" });
    const b = save({ title: "Second proposal here" }, "approval", "staged");
    const c = save({ title: "Third proposal here" }, "code", "rejected");
    const open = await proposals([], fx.ctx);
    const row = (state: string, tierName: string, id: string, title: string) => `${state.padEnd(19)} ${tierName.padEnd(10)} ${id}  skill:review: ${title}  (0d)`;
    expect(open.stdout).toBe(`${row("proposed", "code", a, "First proposal here")}\n${row("staged", "approval", b, "Second proposal here")}\nNext: sindri evolve show ${a}\n`);
    expect((await proposals(["--all"], fx.ctx)).stdout).toContain(c);
    expect((await proposals(["--status", "rejected,staged"], fx.ctx)).stdout).toContain(b);
    expect((await proposals(["--status", "won"], fx.ctx)).stdout).toBe("No proposals with status won.\nNext: sindri evolve proposals --all\n");
    await expect(proposals(["--status", "bogus"], fx.ctx)).rejects.toThrow(/unknown status: bogus/);
    const json = JSON.parse((await proposals(["--json"], fx.ctx)).stdout) as { proposals: { id: string }[] };
    expect(json.proposals.map((p) => p.id)).toEqual([a, b]);
    fx.close();
  });
});

describe("sindri evolve show", () => {
  it("prints the proposal, the recomputed tier, reduced evidence, and the stored comparison", async () => {
    const { fx, save } = await ready();
    const id = save({ artifact: "hook:done-gate", kind: "hook-fix", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "Narrow it." } }, "code");
    const out = (await show([id], fx.ctx)).stdout;
    expect(out).toContain(`Proposal ${id}: Tighten review scope`);
    expect(out).toContain("Status: proposed   Tier: code (recomputed: approval, touches a protected artifact)   Source: reflect:pr-12");
    expect(out).toContain("Artifact: hook:done-gate   Kind: hook-fix");
    expect(out).toContain("Files: config/hooks/done-gate.sh");
    expect(out).toContain("Why:\n  Seen twice.\n  Second line.");
    expect(out).toContain("Evidence: pr:12, transcript:5e55a1d0#4");
    expect(out).toContain("Next: sindri evolve stage");
    fx.ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, 1, '*', 'won', ?, 't', 1)").run(id, JSON.stringify({ line: "won: 20 of 22 decided pairs" }));
    expect((await show([id], fx.ctx)).stdout).toContain("Comparison (run 1): won: 20 of 22 decided pairs");
    const ok = save({ title: "A plain code change" });
    expect((await show([ok], fx.ctx)).stdout).toContain("Tier: code (a repo change, built and merged as an ordinary work item)");
    const prompt = save({ artifact: "prompt:scope.draft", kind: "prompt-edit", title: "Prompt text change", change: { type: "replace-prompt", text: "x".repeat(40) } }, "self-adopt");
    const text = (await show([prompt], fx.ctx)).stdout;
    expect(text).toContain("Prompt text: 40 characters");
    expect(text).toContain("Next: sindri evolve compare");
    const bare = save({ title: "No usable references", evidence: ["t3"] });
    expect((await show([bare], fx.ctx)).stdout).toContain("Evidence: none recorded (1 reference(s) withheld)");
    await expect(show(["nope"], fx.ctx)).rejects.toThrow(/no such proposal: nope/);
    await expect(show([], fx.ctx)).rejects.toThrow(/usage: sindri evolve show <id>/);
    fx.close();
  });

  it("prints scrubbed excerpts for transcript evidence", async () => {
    const { fx, save } = await ready();
    fs.writeFileSync(path.join(fx.transcripts, "5e55a1d0-1111.jsonl"), `${JSON.stringify({ type: "user", cwd: fx.repo, message: { content: "please do not delete the fixtures" } })}\n`);
    const id = save({ evidence: ["pr:12", "transcript:5e55a1d0#1", "transcript:ffffffff#1"] });
    const out = (await show([id], fx.ctx)).stdout;
    expect(out).toContain('Excerpts:\n  transcript:5e55a1d0#1: "please do not delete the fixtures"');
    expect(out).not.toContain("transcript:ffffffff#1: ");
    fx.close();
  });

  it("suggests the next command for each status", async () => {
    const { fx, save } = await ready();
    const cases: [ProposalStatus, string][] = [
      ["won", "sindri evolve adopt"], ["staged", "sindri evolve publish"], ["held", "sindri evolve reject"], ["published", "sindri evolve status"],
      ["insufficient-corpus", "sindri evolve status"], ["rejected", "sindri evolve proposals"],
    ];
    for (const [s, expected] of cases) {
      const id = save({ title: `Proposal in state ${s}` }, "code", s);
      expect((await show([id], fx.ctx)).stdout).toContain(`Next: ${expected}`);
    }
    fx.close();
  });
});

describe("sindri evolve reject", () => {
  it("needs a reason, records an audit row, removes a staged preview, and is idempotent", async () => {
    const { fx, save } = await ready();
    const id = save({}, "code", "staged");
    fs.mkdirSync(path.dirname(stagedFile(fx.deps, id)), { recursive: true });
    fs.writeFileSync(stagedFile(fx.deps, id), "preview");
    await expect(reject([id], fx.ctx)).rejects.toThrow(/usage: sindri evolve reject <id> --reason/);
    await expect(reject([id, "--reason", "  "], fx.ctx)).rejects.toThrow(/usage/);
    await expect(reject(["nope", "--reason", "x"], fx.ctx)).rejects.toThrow(/no such proposal/);
    const r = await reject([id, "--reason", "not useful"], fx.ctx);
    expect(r.stdout).toBe(`Rejected ${id}. The same proposal won't be saved again.\nNext: sindri evolve proposals\n`);
    expect(fs.existsSync(stagedFile(fx.deps, id))).toBe(false);
    expect(fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit").all()).toEqual([{ verb: "reject", detail: `${id}: not useful` }]);
    expect((await reject([id, "--reason", "again"], fx.ctx)).stdout).toBe(`Proposal ${id} is already rejected; nothing to do.\nNext: sindri evolve proposals\n`);
    fx.close();
  });
});

describe("terminal safety of model-written text (show and proposals)", () => {
  const OSC52 = "\u001b]52;c;Zm9v\u0007";
  const ANSI = "\u001b[2K\u001b[1A\u001b[31m";
  const CSI8 = "\u009b31m";

  it("escapes OSC 52, ANSI colour and C1 controls in every field of show, and in the proposals list", async () => {
    const { fx, save } = await ready();
    const id = save({ title: `Tighten docs ${OSC52}`, rationale: `Why ${ANSI}hidden\nsecond ${CSI8} line`, change: { type: "describe", files: ["skills/review/SKILL.md"], description: `Do it ${OSC52}\n\tindented` } });
    const shown = await show([id], fx.ctx);
    const list = await proposals([], fx.ctx);
    for (const out of [shown.stdout, list.stdout]) {
      expect(out).not.toMatch(/[\u0000-\u0008\u000B-\u001F\u007F-\u009F]/);
      expect(out).toContain("\\u{001B}]52;c;Zm9v\\u{0007}");
    }
    expect(shown.stdout).toContain("\\u{001B}[2K\\u{001B}[1A\\u{001B}[31mhidden\n  second \\u{009B}31m line");
    expect(shown.stdout).toContain("  \tindented"); // newline and tab are kept
    fx.close();
  });
});

describe("sindri evolve reject races (guarded transitions)", () => {
  it("never overwrites a status another terminal committed between reject's read and its write (adopted stays adopted)", async () => {
    const { fx, save } = await ready();
    const id = save({}, "code", "won");
    // Another terminal's adopt commits just before reject's write takes the lock.
    const racing = { ...fx.ctx, write: <T>(fn: (epoch: number) => T): T => { fx.ctx.write((epoch) => setStatus(fx.ctx.db, id, "adopted", epoch, fx.deps.now())); return fx.ctx.write(fn); } };
    const r = await reject([id, "--reason", "too late"], racing);
    expect(r.stdout).toBe(`Proposal ${id} is already adopted; nothing to do.\nNext: sindri evolve proposals\n`);
    expect(fx.ctx.db.prepare("SELECT status FROM proposals WHERE id = ?").get(id)).toEqual({ status: "adopted" });
    expect(fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([]);
    fx.close();
  });
});

describe("sindri evolve reject with a profile scrub pattern", () => {
  it("redacts a scrub.extraPatterns match from the audit detail", async () => {
    const { fx, save } = await ready('scrub:\n  extraPatterns:\n    - kind: ticket\n      regex: "WRK-[0-9]{3,6}"\n');
    const id = save({}, "code", "staged");
    await reject([id, "--reason", "see WRK-4242"], fx.ctx);
    expect(fx.ctx.db.prepare("SELECT detail FROM evolve_audit").get()).toEqual({ detail: `${id}: see [REDACTED:ticket]` });
    fx.close();
  });
});

describe("sindri evolve tier", () => {
  it("recomputes the tier from the real diff and exits 1 when the diff is stricter than declared", async () => {
    const { fx, save } = await ready();
    const id = save({ artifact: "package:sindri", kind: "code", change: { type: "describe", files: ["sindri/src/observe/observe.ts"], description: "d" } }, "code");
    git(fx.repo, "checkout", "-q", "-b", "feat");
    fs.writeFileSync(path.join(fx.repo, "sindri/src/observe/observe.ts"), "export const a = 2;\n");
    git(fx.repo, "commit", "-qam", "touch observe");
    const clean = await tier([id], fx.ctx);
    expect(clean.exitCode).toBe(0);
    expect(clean.stdout).toBe(`Proposal ${id} declared code; the diff against main touches 1 file(s), 0 protected. Actual tier: code.\nNext: open the PR\n`);
    fs.mkdirSync(path.join(fx.repo, "sindri/src/scrub"), { recursive: true });
    fs.writeFileSync(path.join(fx.repo, "sindri/src/scrub/patterns.ts"), "export const b = 1;\n");
    git(fx.repo, "add", "-A");
    git(fx.repo, "commit", "-qm", "touch scrub");
    const strict = await tier([id, "--base", "main"], fx.ctx);
    expect(strict.exitCode).toBe(1);
    expect(strict.stdout).toBe(
      `Proposal ${id} declared code; the diff against main touches 2 file(s), 1 protected: sindri/src/scrub/patterns.ts. Actual tier: approval.\nNext: get the owner's approval before merging\n`,
    );
    // a test file the PR adds can't weaken the existing suite; a modified one can
    fs.writeFileSync(path.join(fx.repo, "sindri/tests/brand-new.test.ts"), "export {};\n");
    git(fx.repo, "add", "-A");
    git(fx.repo, "commit", "-qm", "add a test");
    expect((await tier([id, "--base", "main"], fx.ctx)).stdout).toContain("touches 3 file(s), 1 protected: sindri/src/scrub/patterns.ts.");
    fs.writeFileSync(path.join(fx.repo, "sindri/tests/existing.test.ts"), "export const x = 1;\n");
    fs.mkdirSync(path.join(fx.repo, "sindri/src/evolve"), { recursive: true });
    fs.writeFileSync(path.join(fx.repo, "sindri/src/evolve/added.test.ts"), "export {};\n");
    git(fx.repo, "add", "-A");
    git(fx.repo, "commit", "-qm", "modify a test and add one under the machinery");
    const modified = await tier([id, "--base", "main"], fx.ctx);
    expect(modified.stdout).toContain("3 protected: sindri/src/evolve/added.test.ts, sindri/src/scrub/patterns.ts, sindri/tests/existing.test.ts.");
    git(fx.repo, "rm", "-q", "sindri/tests/existing.test.ts");
    git(fx.repo, "commit", "-qm", "delete a test");
    expect((await tier([id, "--base", "main"], fx.ctx)).stdout).toContain("touches 5 file(s), 3 protected");
    await expect(tier([id, "--base", "no-such-ref"], fx.ctx)).rejects.toThrow(/couldn't diff against no-such-ref/);
    const approval = save({ title: "An approval-tier change" }, "approval");
    const same = await tier([approval, "--base", "HEAD"], fx.ctx);
    expect(same.exitCode).toBe(0);
    expect(same.stdout).toBe(`Proposal ${approval} declared approval; the diff against HEAD touches 0 file(s), 0 protected. Actual tier: approval.\nNext: get the owner's approval before merging\n`);
    await expect(tier(["nope"], fx.ctx)).rejects.toThrow(/no such proposal/);
    await expect(tier([], fx.ctx)).rejects.toThrow(/usage: sindri evolve tier <id>/);
    fx.close();
  });
});

describe("the proposals section of status", () => {
  it("counts by status, shows the cap and the merge rate, and marks merged proposals", async () => {
    const { fx, save } = await ready();
    const published = save({ title: "Landed proposal one" }, "code", "published");
    save({ title: "Waiting proposal two" }, "code", "published");
    save({ title: "Staged proposal three" }, "code", "staged");
    save({ title: "Fresh proposal four" }, "code");
    git(fx.repo, "commit", "-q", "--allow-empty", "-m", `feat: it\n\nProposal \`${published}\``);
    const out = await status([], fx.ctx);
    expect(out.stdout).toContain("Proposals: 1 proposed, 1 staged, 1 published, 1 merged");
    expect(out.stdout).toContain("In flight: 2 of 10 (evolve.maxOpenProposals)");
    expect(out.stdout).toContain("Merge rate: 1 of 2 published proposals merged (50%)");
    expect(out.stdout).toContain("Next: sindri evolve check --changed");
    expect(JSON.parse((await status(["--json"], fx.ctx)).stdout)).toMatchObject({ proposals: { proposed: 1, staged: 1, published: 1, merged: 1 }, inFlight: 2, cap: 10, mergeRate: 0.5 });
    audit(fx.ctx.db, fx.deps, "x", "y", 1);
    expect(fx.ctx.db.prepare("SELECT verb, actor FROM evolve_audit").get()).toEqual({ verb: "x", actor: fx.deps.system.username() });
    fx.close();
  });

  it("surfaces held proposals, which don't count against the cap", async () => {
    const { fx, save } = await ready();
    save({ title: "Held by the privacy gate" }, "code", "held");
    save({ title: "Staged proposal here" }, "code", "staged");
    const out = (await status([], fx.ctx)).stdout;
    expect(out).toContain("Proposals: 1 staged, 1 held");
    expect(out).toContain("In flight: 1 of 10 (evolve.maxOpenProposals)");
    expect(out).toContain(`Held: 1 proposal(s) withheld by publish's privacy gate; they don't count against the cap. Reject one with: sindri evolve reject <id> --reason "..."`);
    expect(JSON.parse((await status(["--json"], fx.ctx)).stdout)).toMatchObject({ proposals: { staged: 1, held: 1 }, inFlight: 1 });
    fx.close();
  });

  it("shows no proposals line when there are none, and no merge rate before anything shipped", async () => {
    const { fx, save } = await ready();
    expect((await status([], fx.ctx)).stdout).not.toContain("Proposals:");
    save({ title: "Only a fresh proposal" });
    const out = (await status([], fx.ctx)).stdout;
    expect(out).toContain("Proposals: 1 proposed");
    expect(out).not.toContain("Merge rate");
    expect(JSON.parse((await status(["--json"], fx.ctx)).stdout)).toMatchObject({ mergeRate: null });
    fx.close();
  });
});

describe("sindri evolve tier, hardened", () => {
  const prBranch = async (files: Record<string, string>, over: Record<string, unknown> = { artifact: "package:sindri", kind: "code", change: { type: "describe", files: ["sindri/src/observe/observe.ts"], description: "d" } }) => {
    const { fx, save } = await ready();
    const id = save(over, "code");
    git(fx.repo, "checkout", "-q", "-b", "feat");
    for (const [f, text] of Object.entries(files)) {
      fs.mkdirSync(path.dirname(path.join(fx.repo, f)), { recursive: true });
      fs.writeFileSync(path.join(fx.repo, f), text);
    }
    git(fx.repo, "add", "-A");
    git(fx.repo, "commit", "-q", "--allow-empty", "-m", "pr");
    return { fx, id, out: async () => tier([id, "--base", "main"], fx.ctx) };
  };

  it("keeps added helpers, same-stem shadows, heavy tests and new suite files at approval", async () => {
    for (const f of ["sindri/tests/helpers.js", "sindri/tests/existing.test.js", "sindri/tests/heavy/real.test.ts", "sindri/tests/notes.txt"]) {
      const { fx, out } = await prBranch({ [f]: "x\n" });
      const r = await out();
      expect(r.exitCode, f).toBe(1);
      expect(r.stdout, f).toContain(`1 protected: ${f}.`);
      fx.close();
    }
    const ok = await prBranch({ "sindri/tests/fresh.test.ts": "x\n", "sindri/tests/fresh.spec.ts": "x\n" });
    expect((await ok.out()).exitCode).toBe(0);
    ok.fx.close();
  });

  it("refuses a PR that adds the suite of an artifact that has none", async () => {
    const { fx, out } = await prBranch({ "config/hooks/tests/done-gate.test.sh": "exit 0\n" });
    expect((await out()).exitCode).toBe(1);
    fx.close();
  });

  it("applies the artifact-ownership rule: a changed path outside the artifact is approval", async () => {
    const owned = await prBranch({ "skills/review/SKILL.md": "y\n", "judge/src/x.ts": "export {};\n" }, { artifact: "skill:review", kind: "skill-edit", change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" } });
    const r = await owned.out();
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("0 protected, 1 outside skill:review: judge/src/x.ts. Actual tier: approval.");
    owned.fx.close();
    const ghost = await prBranch({ "sindri/src/observe/observe.ts": "export const a = 5;\n" }, { artifact: "skill:ghost", kind: "skill-edit", change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" } });
    const g = await ghost.out();
    expect(g.exitCode).toBe(1);
    expect(g.stdout).toContain("1 outside skill:ghost: sindri/src/observe/observe.ts.");
    ghost.fx.close();
  });

  it("sees a protected file or a test that was renamed away", async () => {
    const { fx, id, out } = await prBranch({});
    git(fx.repo, "mv", "sindri/src/scrub/patterns.ts", "sindri/src/observe/moved.ts");
    git(fx.repo, "mv", "sindri/tests/existing.test.ts", "sindri/tests/renamed.test.ts");
    git(fx.repo, "commit", "-qm", "renames");
    const r = await out();
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("2 protected: sindri/src/scrub/patterns.ts, sindri/tests/existing.test.ts.");
    expect(id).toBeTruthy();
    fx.close();
  });
});
