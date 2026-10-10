import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { makePlanFileTracker } from "../src/adapters/plan-file/tracker.js";
import { parsePlan } from "../src/adapters/plan-file/parse.js";
import { init } from "../src/evolve/cmd/registry.js";
import { publish, stage } from "../src/evolve/cmd/stage.js";
import { getProposal, inFlightCount, NO_TRANSCRIPT, ProposalSchema, savePublic, saveProposal, stagedFile, type Tier } from "../src/evolve/proposals.js";
import type { GitRunner } from "../src/git.js";
import { evolveFixture, git, scriptedEvolveIo, withDeps, setStatus, type EvolveFixture } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "skills/review/SKILL.md": "x\n" };
const DENY = "privacy:\n  denyTerms:\n    - Acme Care\n"; // every test but the N1 ones runs with a term list, as a real profile must
const REL = "docs/superpowers/plans/2026-10-05-sindri-plan-proposals.md";

async function ready(o: { extraYaml?: string; plans?: string; branch?: string | null } = {}) {
  const fx = await evolveFixture({ files: FILES, extraYaml: o.extraYaml ?? DENY, plans: o.plans });
  // The session the default evidence cites exists, with lines nothing below copies: publish holds a proposal none of
  // whose cited sessions resolve (security-sentinel-1).
  const plain = ["routine session work with no detail anyone would", "repeat in a plan file for the toolkit", "a third turn of nothing much at all here", "the fourth and last turn of this session"];
  fs.writeFileSync(path.join(fx.transcripts, "5e55a1d0-0000-4000-8000-000000000001.jsonl"), plain.map((t) => JSON.stringify({ type: "user", timestamp: "2026-10-08T09:00:00Z", cwd: fx.repo, message: { content: t } })).join("\n"));
  await init([], fx.ctx);
  if (o.branch !== null) git(fx.repo, "checkout", "-q", "-b", o.branch ?? "docs/proposals");
  let n = 0;
  // A reflect proposal records the sessions its transcript came from (security-sentinel-0): by default, that session.
  const save = (title: string, over: Record<string, unknown> = {}, tier: Tier = "code", src: { source?: string; sessions?: string[] } = {}) =>
    fx.ctx.write((epoch) =>
      saveProposal(fx.ctx.db, ProposalSchema.parse({
        artifact: "skill:review", kind: "skill-edit", title, rationale: "Seen twice in review sessions.", evidence: ["pr:12", "transcript:5e55a1d0#4"],
        change: { type: "describe", files: ["skills/review/SKILL.md"], description: "Add a step." }, ...over,
      }), src.source ?? "reflect:pr-12", tier, epoch, new Date(Date.parse("2026-10-08T12:00:00Z") + n++ * 1000), src.sessions ?? ["5e55a1d0"]).id);
  return { fx, save };
}

describe("sindri evolve publish (guarded transitions)", () => {
  it("skips a proposal rejected after publish listed it, and numbers the rest without a gap", async () => {
    const { fx, save } = await ready();
    const a = save("Alpha change here");
    const b = save("Beta change here");
    await stage([], fx.ctx);
    const racing = { ...fx.ctx, write: <T>(fn: (epoch: number) => T): T => { fx.ctx.write((epoch) => setStatus(fx.ctx.db, a, "rejected", epoch, fx.deps.now())); return fx.ctx.write(fn); } };
    const r = await publish(["--json"], racing);
    expect(getProposal(fx.ctx.db, a)?.status).toBe("rejected");
    expect(getProposal(fx.ctx.db, b)?.status).toBe("published");
    expect((JSON.parse(r.stdout) as { published: { id: string; n: number }[] }).published).toEqual([{ id: b, n: 1, tier: "code" }]);
    const text = fs.readFileSync(path.join(fx.repo, REL), "utf8");
    expect(text).not.toContain("Alpha change here");
    expect(parsePlan(text).tasks.map((t) => [t.number, t.title])).toEqual([[1, "Beta change here"]]);
    fx.close();
  });
});

describe("sindri evolve publish (home paths of the current OS user)", () => {
  it("holds a proposal that quotes a lowercase home path of the OS user from the system seam", async () => {
    const { fx, save } = await ready();
    const user = fx.deps.system.username();
    const id = save("Alpha change here", { rationale: `See /users/${user}/work/notes.md for the repro.` });
    await stage([], fx.ctx);
    const r = await publish(["--json"], fx.ctx);
    expect((JSON.parse(r.stdout) as { held: { id: string }[] }).held.map((h) => h.id)).toEqual([id]);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("held");
    fx.close();
  });
});

describe("sindri evolve publish (symlinks)", () => {
  const outside = (name: string): string => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-outside-"));
    return path.join(dir, name);
  };

  it("refuses a plan file that is a symlink, writes nothing through it and leaves the proposals staged", async () => {
    const { fx, save } = await ready();
    const id = save("Alpha change here");
    await stage([], fx.ctx);
    const target = outside("victim.md");
    fs.writeFileSync(target, "precious\n");
    fs.symlinkSync(target, path.join(fx.repo, REL));
    await expect(publish([], fx.ctx)).rejects.toThrow(/isn't a plain file/);
    expect(fs.readFileSync(target, "utf8")).toBe("precious\n");
    expect(getProposal(fx.ctx.db, id)?.status).toBe("staged");
    fx.close();
  });

  it("refuses a plans directory that is a symlink, even with --dry-run", async () => {
    const { fx, save } = await ready();
    save("Alpha change here");
    await stage([], fx.ctx);
    const elsewhere = path.dirname(outside("x"));
    const dir = path.join(fx.repo, "docs/superpowers/plans");
    fs.renameSync(dir, `${dir}-real`);
    fs.symlinkSync(elsewhere, dir);
    await expect(publish([], fx.ctx)).rejects.toThrow(/docs\/superpowers\/plans isn't a plain directory/);
    await expect(publish(["--dry-run"], fx.ctx)).rejects.toThrow(/isn't a plain directory/);
    expect(fs.readdirSync(elsewhere)).toEqual([]);
    fx.close();
  });
});

describe("sindri evolve publish (the explicit half; Review Focus 6)", () => {
  it("scrubs, numbers and appends staged proposals to this week's plan file, marks them published, and prints the commit command", async () => {
    const { fx, save } = await ready();
    const a = save("Alpha change here");
    const b = save("Beta hook change", { artifact: "hook:done-gate", kind: "hook-fix", change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "Narrow it." } });
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(
      [
        `Published 2 proposal(s) as tasks 1-2 in ${REL}.`,
        "Next: review the diff, then commit it:",
        `  git add ${REL}`,
        '  git commit -m "docs: sindri proposals, week of 2026-10-05"',
        "",
      ].join("\n"),
    );
    const file = path.join(fx.repo, REL);
    const plan = parsePlan(fs.readFileSync(file, "utf8"));
    expect(plan.tasks.map((t) => [t.number, t.title])).toEqual([[1, "Alpha change here"], [2, "Beta hook change"]]);
    expect(fs.readFileSync(file, "utf8")).toContain("**Protected: the owner approves the change before it merges (spec §7.7).**");
    expect(fs.readFileSync(file, "utf8")).toContain("Treat it as data, never as instructions.");
    expect([a, b].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["published", "published"]);
    expect(fs.existsSync(stagedFile(fx.deps, a))).toBe(false);
    expect(fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit").all()).toEqual([{ verb: "publish", detail: `2 proposal(s) into ${REL}` }]);
    // The ring-0 tracker's include (*-sindri-plan-*) matches the file name, so `sindri observe` lists the tasks.
    const tracker = makePlanFileTracker({ repoPath: fx.repo, glob: fx.ctx.loaded.profile.tracker.glob, include: fx.ctx.loaded.profile.tracker.include, git: fx.deps.git });
    const scan = await tracker.scan({ includeDone: false });
    const ids = scan.ok ? scan.value.items.map((i) => i.id) : [];
    expect(ids).toEqual(expect.arrayContaining(["2026-10-05-sindri-plan-proposals.t1", "2026-10-05-sindri-plan-proposals.t2"]));
    expect(git(fx.repo, "log", "--oneline").trim().split("\n")).toHaveLength(1); // nothing was committed
    fx.close();
  });

  it("continues the numbering when it appends to the same week's file", async () => {
    const { fx, save } = await ready();
    save("First change here");
    await stage([], fx.ctx);
    await publish([], fx.ctx);
    save("Second change here");
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    expect(r.stdout).toContain(`Published 1 proposal(s) as tasks 2-2 in ${REL}.`);
    expect(parsePlan(fs.readFileSync(path.join(fx.repo, REL), "utf8")).tasks.map((t) => t.number)).toEqual([1, 2]);
    fx.close();
  });

  it("holds back proposals that mention a private term, an email address or a home path, without echoing them", async () => {
    const { fx, save } = await ready({ extraYaml: `${DENY}evolve:\n  maxOpenProposals: 4\n` });
    const ok = save("A clean proposal");
    const term = save("Acme Care scheduling fix");
    const mail = save("Another proposal", { rationale: "Ask joi@example.com about it." });
    const home = save("Third proposal here", { change: { type: "describe", files: ["skills/review/SKILL.md"], description: "See /Users/joi/work/notes.md" } });
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toBe(
      [
        `Published 1 proposal(s) as tasks 1-1 in ${REL}; 3 held back.`,
        `held ${term} (terms): contains a private term`,
        `held ${mail} (personal): contains an email address or a home directory path`,
        `held ${home} (personal): contains an email address or a home directory path`,
        "Held by layer: terms 1, personal 2",
        "Next: review the diff, then commit it:",
        `  git add ${REL}`,
        '  git commit -m "docs: sindri proposals, week of 2026-10-05"',
        `Held proposals stay held and don't count against the cap. Reject one with: sindri evolve reject <id> --reason "..."`,
        "",
      ].join("\n"),
    );
    const text = fs.readFileSync(path.join(fx.repo, REL), "utf8");
    expect(text).toContain("A clean proposal");
    expect(text).not.toMatch(/Acme|joi@example|\/Users\/joi/);
    expect([ok, term, mail, home].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["published", "held", "held", "held"]);
    expect(fs.existsSync(stagedFile(fx.deps, term))).toBe(true); // a held proposal keeps its preview
    // All four fit the cap (4) and stage. Held proposals then leave it: only the published one is in flight, so a new proposal still stages (it would not if the three held ones counted).
    expect(inFlightCount(fx.ctx.db)).toBe(1);
    const later = save("A later proposal");
    expect((await stage([], fx.ctx)).stdout).toContain("Staged 1 proposal(s)");
    expect(getProposal(fx.ctx.db, later)?.status).toBe("staged");
    fx.close();
  });

  it("refuses to publish while privacy.denyTerms is empty, unless --no-privacy-terms is passed (security N1)", async () => {
    const { fx, save } = await ready({ extraYaml: "" });
    const id = save("A clean proposal");
    await stage([], fx.ctx);
    await expect(publish([], fx.ctx)).rejects.toThrow(/privacy\.denyTerms is empty/);
    await expect(publish(["--dry-run"], fx.ctx)).rejects.toThrow(expect.objectContaining({
      code: "SND-EVOLVE-015", fix: expect.stringContaining("--no-privacy-terms to publish with the private-terms layer skipped (the other five layers still run)"),
    }));
    expect(fs.existsSync(path.join(fx.repo, REL))).toBe(false);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("staged");
    const r = await publish(["--no-privacy-terms"], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Warning: no privacy.denyTerms were set, so the private-terms layer was skipped; the patterns, identifiers, verbatim, personal and scrubber layers ran.");
    expect(getProposal(fx.ctx.db, id)?.status).toBe("published");
    fx.close();
    // The flag changes nothing once terms are set, and the email and home-path checks still run without them.
    const withTerms = await ready();
    withTerms.save("Another clean proposal");
    await stage([], withTerms.fx.ctx);
    expect((await publish(["--no-privacy-terms"], withTerms.fx.ctx)).stdout).not.toContain("no privacy.denyTerms were set");
    withTerms.fx.close();
    const bare = await ready({ extraYaml: "" });
    const mail = bare.save("Mail proposal", { rationale: "Ask joi@example.com about it." });
    await stage([], bare.fx.ctx);
    const held = await publish(["--no-privacy-terms"], bare.fx.ctx);
    expect(held.exitCode).toBe(1);
    expect(held.stdout).toContain(`held ${mail} (personal): contains an email address or a home directory path`);
    bare.fx.close();
  });

  it("writes nothing for --dry-run, and says so when everything is held or nothing is staged", async () => {
    const { fx, save } = await ready({ extraYaml: "privacy:\n  denyTerms:\n    - Acme Care\n" });
    expect((await publish([], fx.ctx)).stdout).toBe("Nothing is staged.\nNext: sindri evolve stage\n");
    save("A clean proposal");
    await stage([], fx.ctx);
    const dry = await publish(["--dry-run"], fx.ctx);
    expect(dry.stdout).toContain(`Would publish 1 proposal(s) as tasks 1-1 in ${REL} (dry run; nothing was written).`);
    expect(fs.existsSync(path.join(fx.repo, REL))).toBe(false);
    expect(JSON.parse((await publish(["--dry-run", "--json"], fx.ctx)).stdout)).toMatchObject({ dryRun: true, published: [{ n: 1 }] });
    fx.close();
    const held = await ready({ extraYaml: "privacy:\n  denyTerms:\n    - Acme Care\n" });
    const id = held.save("Acme Care scheduling fix");
    await stage([], held.fx.ctx);
    const r = await publish([], held.fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(getProposal(held.fx.ctx.db, id)?.status).toBe("held");
    expect(r.stdout).toBe(`Nothing to publish: 1 held back.\nheld ${id} (terms): contains a private term\nHeld by layer: terms 1\nHeld proposals stay held and don't count against the cap. Reject one with: sindri evolve reject <id> --reason "..."\n`);
    held.fx.close();
  });

  it("refuses to write into the default branch, and warns when the tracker wouldn't list the file", async () => {
    const onMain = await ready({ branch: null });
    onMain.save("A clean proposal");
    await stage([], onMain.fx.ctx);
    await expect(publish([], onMain.fx.ctx)).rejects.toThrow(/default branch/);
    expect((await publish(["--dry-run"], onMain.fx.ctx)).exitCode).toBe(0);
    onMain.fx.close();
    const other = await ready({ plans: "*-other-*" });
    other.save("A clean proposal");
    await stage([], other.fx.ctx);
    const r = await publish([], other.fx.ctx);
    expect(r.stdout).toContain("Warning: tracker.include in the profile doesn't match 2026-10-05-sindri-plan-proposals.md, so sindri observe won't list these tasks.");
    other.fx.close();
  });
});

describe("publish: re-tiering, replace-prompt text, profile scrub patterns, the cap recount (Task 10 rulings)", () => {
  it("re-tiers at publish when the repo gained a protected path after staging, and renders no-evidence and replace-prompt proposals", async () => {
    const { fx, save } = await ready();
    const d = save("Skill step to add", { evidence: [] });
    const p = save("Prompt replacement on a skill", { change: { type: "replace-prompt", text: "Brand new text, never published verbatim." } }, "approval");
    await stage([], fx.ctx);
    expect(getProposal(fx.ctx.db, d)?.tier).toBe("code");
    const name = fx.ctx.loaded.profile.tracker.repo;
    const loaded = { ...fx.ctx.loaded, repos: { ...fx.ctx.loaded.repos, [name]: { ...fx.ctx.loaded.repos[name], protectedPaths: ["skills/**"] } } };
    const r = await publish([], { ...fx.ctx, loaded });
    expect(r.exitCode).toBe(0);
    const text = fs.readFileSync(path.join(fx.repo, REL), "utf8");
    expect(getProposal(fx.ctx.db, d)).toMatchObject({ status: "published", tier: "approval" });
    expect(getProposal(fx.ctx.db, p)?.status).toBe("published");
    expect(text).toContain("**Evidence:** none recorded");
    expect(text).toContain("Replace the prompt text; see the proposal in sindri evolve show.");
    expect(text.match(/Protected: the owner approves/g)).toHaveLength(2);
    expect(text).not.toContain("Brand new text");
    fx.close();
  });

  it("holds a proposal that matches a profile scrub.extraPatterns term, with the reason and without echoing it", async () => {
    const { fx, save } = await ready({ extraYaml: `${DENY}scrub:\n  extraPatterns:\n    - kind: ticket\n      regex: "WRK-[0-9]{3,6}"\n` });
    const ok = save("A clean proposal");
    const bad = save("Fix the flow from WRK-4821");
    const rat = save("Another clean title", { rationale: "Linked to WRK-99001 upstream." });
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain(`held ${bad} (scrubber): contains text the scrubber redacts (a secret or a scrub.extraPatterns match)`);
    expect(r.stdout).toContain(`held ${rat} (scrubber): contains text the scrubber redacts (a secret or a scrub.extraPatterns match)`);
    expect(r.stdout).not.toContain("WRK-");
    expect(fs.readFileSync(path.join(fx.repo, REL), "utf8")).not.toContain("WRK-");
    expect([ok, bad, rat].map((id) => getProposal(fx.ctx.db, id)?.status)).toEqual(["published", "held", "held"]);
    fx.close();
  });

  it("counts a held proposal again once a later publish releases it (the cap recount)", async () => {
    const { fx, save } = await ready({ extraYaml: `${DENY}evolve:\n  maxOpenProposals: 2\n` });
    save("A clean proposal");
    const term = save("Acme Care scheduling fix");
    await stage([], fx.ctx);
    await publish([], fx.ctx);
    expect(getProposal(fx.ctx.db, term)?.status).toBe("held");
    expect(inFlightCount(fx.ctx.db)).toBe(1);
    const relaxed = { ...fx.ctx, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, privacy: { ...fx.ctx.loaded.profile.privacy, denyTerms: ["Other Org"] } } } };
    const r = await publish([], relaxed);
    expect(r.stdout).toContain("Published 1 proposal(s) as tasks 2-2");
    expect(getProposal(fx.ctx.db, term)?.status).toBe("published");
    expect(inFlightCount(fx.ctx.db)).toBe(2);
    save("A later proposal");
    expect((await stage([], fx.ctx)).stdout).toContain("Cap reached: 2 proposals are staged or published");
    fx.close();
  });
});

describe("publish hardening (Task 10 fix round 1)", () => {
  it("holds a term hidden across a line break, a double space or an NBSP in the rationale, without echoing it (I1)", async () => {
    const { fx, save } = await ready({ extraYaml: `${DENY}evolve:\n  maxOpenProposals: 5\n` });
    const ids = ["Acme\nCare", "Acme  Care", "Acme\u00a0Care", "Acme\u2028Care"].map((h, i) => save(`Clean title number ${i}`, { rationale: `We saw this at ${h} twice` }));
    const fine = save("Perfectly clean proposal");
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    for (const id of ids) expect(r.stdout).toContain(`held ${id} (terms): contains a private term`);
    expect(r.stdout).not.toMatch(/Acme/);
    expect(getProposal(fx.ctx.db, fine)?.status).toBe("published");
    expect(fs.readFileSync(path.join(fx.repo, REL), "utf8")).not.toMatch(/Acme/);
    fx.close();
  });

  it("holds a private term that appears only in a replace-prompt text (m9)", async () => {
    const { fx, save } = await ready();
    const id = save("Prompt replacement on a skill", { change: { type: "replace-prompt", text: "Written for Acme Care teams only." } }, "approval");
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    expect(r.stdout).toContain(`held ${id} (terms): contains a private term`);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("held");
    fx.close();
  });

  it("refuses when HEAD is detached, and when git can't say which branch it is on (m6)", async () => {
    const detached = await ready({ branch: null });
    detached.save("A clean proposal");
    await stage([], detached.fx.ctx);
    git(detached.fx.repo, "checkout", "-q", "--detach");
    await expect(publish([], detached.fx.ctx)).rejects.toThrow(/not on a branch/);
    expect((await publish(["--dry-run"], detached.fx.ctx)).exitCode).toBe(0);
    expect(fs.existsSync(path.join(detached.fx.repo, REL))).toBe(false);
    detached.fx.close();
    const broken = await ready();
    broken.save("A clean proposal");
    await stage([], broken.fx.ctx);
    const failing = withDeps(broken.fx.ctx, { git: { run: async () => ({ ok: false, stdout: "", stderr: "boom", code: 128 }) } as never });
    await expect(publish([], failing)).rejects.toThrow(/couldn't tell which branch/);
    expect(fs.existsSync(path.join(broken.fx.repo, REL))).toBe(false);
    broken.fx.close();
  });
});

describe("publish renders only the generalized version (privacy B1)", () => {
  it("publishes the public text, never the original", async () => {
    const { fx, save } = await ready();
    const id = save("Original private wording");
    await stage([], fx.ctx);
    fx.ctx.write((epoch) => savePublic(fx.ctx.db, id, { ...getProposal(fx.ctx.db, id)!.proposal, title: "Generic wording to publish" }, { category: "none", model: "m" }, epoch, fx.deps.now()));
    await publish([], fx.ctx);
    const text = fs.readFileSync(path.join(fx.repo, REL), "utf8");
    expect(text).toContain("Generic wording to publish");
    expect(text).not.toContain("Original private wording");
    fx.close();
  });

  it("holds a staged proposal with no public row under the generalize layer", async () => {
    const { fx, save } = await ready();
    const id = save("Alpha change here");
    await stage([], fx.ctx);
    fx.ctx.db.prepare("DELETE FROM proposal_public WHERE proposal_id = ?").run(id);
    const r = JSON.parse((await publish(["--json"], fx.ctx)).stdout) as { held: { id: string; layer: string; why: string }[]; heldByLayer: Record<string, number> };
    expect(r.held).toEqual([{ id, layer: "generalize", why: "not generalized yet; run sindri evolve stage" }]);
    expect(r.heldByLayer).toEqual({ generalize: 1 });
    expect(getProposal(fx.ctx.db, id)?.status).toBe("held");
    fx.close();
  });

  it("holds a proposal whose public row no longer parses and publishes the rest; the next stage regenerates it (kieran-typescript-reviewer-1)", async () => {
    const { fx, save } = await ready();
    const bad = save("Alpha change here");
    const good = save("Beta change here");
    await stage([], fx.ctx);
    fx.ctx.db.prepare("UPDATE proposal_public SET body = '{\"drifted\":true}' WHERE proposal_id = ?").run(bad);
    const r = JSON.parse((await publish(["--json"], fx.ctx)).stdout) as { published: { id: string }[]; held: { id: string; layer: string; why: string }[] };
    expect(r.published.map((p) => p.id)).toEqual([good]);
    expect(r.held).toEqual([{ id: bad, layer: "generalize", why: "its public version is unreadable; run sindri evolve stage" }]);
    expect(getProposal(fx.ctx.db, bad)?.status).toBe("held");
    await stage([], fx.ctx);
    expect(getProposal(fx.ctx.db, bad)?.status).toBe("staged");
    expect((JSON.parse((await publish(["--json"], fx.ctx)).stdout) as { published: { id: string }[] }).published.map((p) => p.id)).toEqual([bad]);
    fx.close();
  });

  it("re-tiers from the public version: a rewrite that moves files to a protected path lands as approval (Review Focus 2)", async () => {
    const { fx, save } = await ready();
    const id = save("Alpha change here");
    await stage([], fx.ctx);
    const p = getProposal(fx.ctx.db, id)!.proposal;
    fx.ctx.write((epoch) => savePublic(fx.ctx.db, id, { ...p, change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "x" } }, { category: "none", model: "m" }, epoch, fx.deps.now()));
    const r = JSON.parse((await publish(["--json"], fx.ctx)).stdout) as { published: { tier: string }[] };
    expect(r.published[0].tier).toBe("approval");
    expect(getProposal(fx.ctx.db, id)?.tier).toBe("approval");
    fx.close();
  });

  it("keeps the stricter tier: a rewrite that moves files from a protected path to an ordinary one still lands as approval", async () => {
    const { fx, save } = await ready();
    const id = save("Alpha change here", { change: { type: "describe", files: ["config/hooks/done-gate.sh"], description: "x" } });
    await stage([], fx.ctx);
    const p = getProposal(fx.ctx.db, id)!.proposal;
    fx.ctx.write((epoch) => savePublic(fx.ctx.db, id, { ...p, change: { type: "describe", files: ["skills/review/SKILL.md"], description: "x" } }, { category: "none", model: "m" }, epoch, fx.deps.now()));
    const r = JSON.parse((await publish(["--json"], fx.ctx)).stdout) as { published: { tier: string }[] };
    expect(r.published[0].tier).toBe("approval");
    expect(getProposal(fx.ctx.db, id)?.tier).toBe("approval");
    expect(fs.readFileSync(path.join(fx.repo, REL), "utf8")).toContain("touches a protected path");
    fx.close();
  });

  it("checks the public version only: a private term left in the original doesn't hold a clean rewrite", async () => {
    const { fx, save } = await ready();
    const id = save("Acme Care scheduling fix");
    await stage([], fx.ctx);
    fx.ctx.write((epoch) => savePublic(fx.ctx.db, id, { ...getProposal(fx.ctx.db, id)!.proposal, title: "Scheduling fix" }, { category: "workplace", model: "m" }, epoch, fx.deps.now()));
    const r = await publish([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(fs.readFileSync(path.join(fx.repo, REL), "utf8")).not.toMatch(/Acme/);
    fx.close();
  });
});

describe("publish gate layers (privacy B1)", () => {
  const PATTERNS = `${DENY}  denyPatterns:\n    - "GLX-\\\\d+"\n`;
  const transcriptLine = (fx: EvolveFixture, text: string): string =>
    `${JSON.stringify({ type: "user", timestamp: "2026-10-08T10:00:00Z", cwd: fx.repo, message: { content: text } })}\n`;
  const COPIED = "the overnight rota swap left two shifts without a charge nurse";
  const SESSION = "5e55a1d0-0000-4000-8000-000000000001.jsonl";

  async function heldLayer(
    over: Record<string, unknown>,
    o: {
      extraYaml?: string; files?: (fx: EvolveFixture) => Record<string, string>; repoFiles?: Record<string, string>; stagedFiles?: Record<string, string>; noTranscripts?: boolean;
      removeAfterStage?: string[]; src?: { source?: string; sessions?: string[] };
    } = {},
  ): Promise<{ layer: string; why: string }[]> {
    const { fx, save } = await ready({ extraYaml: o.extraYaml ?? PATTERNS });
    for (const [f, t] of Object.entries(o.files?.(fx) ?? {})) fs.writeFileSync(path.join(fx.transcripts, f), t);
    // Toolkit text counts as public once it is on the remote default branch: commit it and point origin/main there.
    for (const [f, t] of Object.entries(o.repoFiles ?? {})) {
      fs.writeFileSync(path.join(fx.repo, f), t);
      git(fx.repo, "add", f);
      git(fx.repo, "commit", "-qm", `add ${f}`);
    }
    git(fx.repo, "update-ref", "refs/remotes/origin/main", "HEAD");
    for (const [f, t] of Object.entries(o.stagedFiles ?? {})) {
      fs.writeFileSync(path.join(fx.repo, f), t);
      git(fx.repo, "add", f);
    }
    save("Alpha change here", over, "code", o.src);
    await stage([], fx.ctx);
    if (o.noTranscripts === true) fs.rmSync(fx.transcripts, { recursive: true }); // deleted (cleanupPeriodDays) after stage
    for (const f of o.removeAfterStage ?? []) fs.rmSync(path.join(fx.transcripts, f));
    const r = JSON.parse((await publish(["--json", "--dry-run"], fx.ctx)).stdout) as { held: { layer: string; why: string }[] };
    fx.close();
    return r.held.map((h) => ({ layer: h.layer, why: h.why }));
  }

  it("terms", async () => {
    expect(await heldLayer({ rationale: "Seen at ACME care twice." })).toEqual([{ layer: "terms", why: "contains a private term" }]);
  });

  it("patterns, without printing the match", async () => {
    const held = await heldLayer({ rationale: "Seen on glx-1234." });
    expect(held).toEqual([{ layer: "patterns", why: "matches a privacy.denyPatterns entry" }]);
    expect(JSON.stringify(held)).not.toMatch(/1234/);
  });

  it("verbatim: a rationale copying 8 words of its evidence transcript", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }) },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
    expect(JSON.stringify(held)).not.toMatch(/rota/);
  });

  it("verbatim: a run the toolkit's own tracked files already contain is public, not a leak", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }), repoFiles: { "NOTES.md": "# Notes\n\nThe overnight rota swap left two shifts without anyone.\n" } },
    );
    expect(held).toEqual([]);
  });

  it("verbatim: a cited session that exists brings in all its lines even when the cited line doesn't resolve (fail closed)", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:0badc0de#9", "transcript:5e55a1d0#2"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }) },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a ref whose session isn't among this repo's sessions brings in nothing while another cited session resolves (Review Focus 1)", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:0badc0de#1", "transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, "please retry the flaky check once"), "0ddba11e-0000-4000-8000-000000000002.jsonl": transcriptLine(fx, COPIED) }) },
    );
    expect(held).toEqual([]);
  });

  it("verbatim: holds when none of a proposal's cited sessions resolves, so copied text can't be checked (security-sentinel-1)", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["pr:12", "transcript:0badc0de#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }), src: { sessions: [NO_TRANSCRIPT] } }, // nothing else to check
    );
    expect(held).toEqual([{ layer: "verbatim", why: "evidence transcripts are missing, so copied text can't be checked" }]);
  });

  it("verbatim: holds the same way when the transcripts dir doesn't exist, also for a proposal citing no transcript whose sources are recorded; telemetry passes", async () => {
    const sourceMissing = [{ layer: "verbatim", why: "a source transcript it was built from is missing, so copied text can't be checked" }];
    expect(await heldLayer({ evidence: ["transcript:5e55a1d0#1"] }, { noTranscripts: true })).toEqual(sourceMissing);
    expect(await heldLayer({ evidence: ["pr:12"] }, { noTranscripts: true })).toEqual(sourceMissing); // its recorded source is gone
    expect(await heldLayer({ evidence: ["transcript:5e55a1d0#1"] }, { noTranscripts: true, src: { sessions: [NO_TRANSCRIPT] } }))
      .toEqual([{ layer: "verbatim", why: "evidence transcripts are missing, so copied text can't be checked" }]); // only cited ones
    expect(await heldLayer({ evidence: ["pr:12"] }, { noTranscripts: true, src: { source: "telemetry:done-gate", sessions: [] } })).toEqual([]);
  });

  it("verbatim: a two-source proposal whose one recorded source was cleaned up holds, though the other still resolves (run 4 security-sentinel-1)", async () => {
    const other = "0ddba11e-0000-4000-8000-000000000002.jsonl";
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["pr:12"] },
      { files: (fx) => ({ [other]: transcriptLine(fx, COPIED) }), src: { sessions: ["5e55a1d0", "0ddba11e"] }, removeAfterStage: [other] },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "a source transcript it was built from is missing, so copied text can't be checked" }]);
    expect(JSON.stringify(held)).not.toMatch(/0ddba11e|overnight/);
    // a cited session that isn't there while the recorded ones resolve is still tolerated (Review Focus 1)
    expect(await heldLayer({ evidence: ["pr:12", "transcript:feedf00d#1"] })).toEqual([]);
  });

  it("verbatim: a reflect proposal citing only pr:N that copies 8 words of a branch session it was built from holds (security-sentinel-0)", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["pr:12"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }) },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a proposal citing session A that copies from source session B holds (security-sentinel-0)", async () => {
    const other = "0ddba11e-0000-4000-8000-000000000002.jsonl";
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, "please retry the flaky check once"), [other]: transcriptLine(fx, COPIED) }), src: { sessions: ["5e55a1d0", "0ddba11e"] } },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a transcript-sourced proposal with no recorded source sessions holds, whatever it cites (security-sentinel-0)", async () => {
    const why = [{ layer: "verbatim", why: "its source transcripts weren't recorded, so copied text can't be checked" }];
    expect(await heldLayer({ evidence: ["pr:12"] }, { src: { sessions: [] } })).toEqual(why);
    expect(await heldLayer({ evidence: ["transcript:5e55a1d0#1"] }, { src: { source: "correct:2026-W41", sessions: [] } })).toEqual(why);
  });

  it("verbatim: a reflect proposal recorded as having read no transcript passes; what it cites is still checked", async () => {
    expect(await heldLayer({ evidence: ["pr:12"] }, { src: { sessions: [NO_TRANSCRIPT] } })).toEqual([]);
    expect(await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }), src: { sessions: [NO_TRANSCRIPT] } },
    )).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a telemetry proposal (no transcripts behind it) is unaffected", async () => {
    expect(await heldLayer({ evidence: ["pr:12"] }, { src: { source: "telemetry:done-gate", sessions: [] } })).toEqual([]);
  });

  it("verbatim: a re-proposal adds its source sessions, and publish checks them (security-sentinel-0)", async () => {
    const { fx, save } = await ready({ extraYaml: PATTERNS });
    git(fx.repo, "update-ref", "refs/remotes/origin/main", "HEAD");
    fs.writeFileSync(path.join(fx.transcripts, "abcd1234-0000-4000-8000-000000000003.jsonl"), transcriptLine(fx, COPIED));
    const id = save("Alpha change here", { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["pr:12"] });
    await stage([], fx.ctx);
    expect((JSON.parse((await publish(["--json", "--dry-run"], fx.ctx)).stdout) as { held: unknown[] }).held).toEqual([]);
    expect(save("Alpha change here", { evidence: ["pr:13"] }, "code", { sessions: ["abcd1234"] })).toBe(id);
    const after = JSON.parse((await publish(["--json", "--dry-run"], fx.ctx)).stdout) as { held: { id: string; layer: string; why: string }[] };
    expect(after.held).toEqual([{ id, layer: "verbatim", why: "copies transcript text" }]);
    fx.close();
  });

  it("verbatim: a pre-provenance proposal merged with a re-proposal that records sources is still held as unrecorded (run 4 kieran-typescript-reviewer-1, security-sentinel-3)", async () => {
    const { fx, save } = await ready({ extraYaml: PATTERNS });
    git(fx.repo, "update-ref", "refs/remotes/origin/main", "HEAD");
    const id = save("Alpha change here", { evidence: ["pr:12"] }, "code", { sessions: [] }); // saved before v5: no source rows
    expect(save("Alpha change here", { evidence: ["pr:13"] }, "code", { source: "correct:2026-W41", sessions: ["5e55a1d0"] })).toBe(id);
    await stage([], fx.ctx);
    const out = JSON.parse((await publish(["--json", "--dry-run"], fx.ctx)).stdout) as { held: { id: string; layer: string; why: string }[] };
    expect(out.held).toEqual([{ id, layer: "verbatim", why: "its source transcripts weren't recorded, so copied text can't be checked" }]);
    fx.close();
  });

  it("verbatim: a run in a staged but uncommitted toolkit file isn't public, so the copy still holds (security-sentinel-0)", async () => {
    const held = await heldLayer(
      { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) }), stagedFiles: { "NOTES.md": "# Notes\n\nThe overnight rota swap left two shifts without anyone.\n" } },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a run copied from an uncited line of a cited session holds too (hand-test leak)", async () => {
    const held = await heldLayer(
      { title: "Fix: the overnight rota swap left two shifts without cover", evidence: ["transcript:5e55a1d0#2"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) + transcriptLine(fx, "please retry the flaky check once") }) },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
    expect(JSON.stringify(held)).not.toMatch(/rota/);
  });

  it("verbatim: a short human turn copied whole into the title holds", async () => {
    const held = await heldLayer(
      { title: "Migrate the zorb ledger sync before freeze", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, "migrate the zorb ledger sync before freeze") }) },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a quote spanning two turns of a cited session holds", async () => {
    const held = await heldLayer(
      { rationale: "So the overnight rota swap left two shifts uncovered.", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, "why did the overnight rota swap") + transcriptLine(fx, "left two shifts uncovered on the ward") }) },
    );
    expect(held).toEqual([{ layer: "verbatim", why: "copies transcript text" }]);
  });

  it("verbatim: a run copied from a session neither cited nor recorded as a source still passes (the gate's scope)", async () => {
    const other = "0ddba11e-0000-4000-8000-000000000002.jsonl";
    const held = await heldLayer(
      { title: "Fix: the overnight rota swap left two shifts without cover", evidence: ["transcript:5e55a1d0#1"] },
      { files: (fx) => ({ [SESSION]: transcriptLine(fx, "please retry the flaky check once"), [other]: transcriptLine(fx, COPIED) }) },
    );
    expect(held).toEqual([]);
  });

  it("verbatim: an uncited line's run the toolkit's tracked files already contain is public", async () => {
    const held = await heldLayer(
      { title: "Fix: the overnight rota swap left two shifts without cover", evidence: ["transcript:5e55a1d0#2"] },
      {
        files: (fx) => ({ [SESSION]: transcriptLine(fx, COPIED) + transcriptLine(fx, "please retry the flaky check once") }),
        repoFiles: { "NOTES.md": "# Notes\n\nThe overnight rota swap left two shifts without anyone.\n" },
      },
    );
    expect(held).toEqual([]);
  });

  it("verbatim: checks the original's evidence too, so a ref merged in after generalization still counts (fail closed)", async () => {
    const { fx, save } = await ready({ extraYaml: PATTERNS });
    fs.writeFileSync(path.join(fx.transcripts, "abcd1234-0000-4000-8000-000000000003.jsonl"), transcriptLine(fx, COPIED)); // a session the first evidence doesn't cite
    const id = save("Alpha change here");
    await stage([], fx.ctx);
    const pub = { ...getProposal(fx.ctx.db, id)!.proposal, title: "The overnight rota swap left two shifts without cover" };
    fx.ctx.write((epoch) => savePublic(fx.ctx.db, id, pub, { category: "none", model: "m" }, epoch, fx.deps.now()));
    const before = JSON.parse((await publish(["--json", "--dry-run"], fx.ctx)).stdout) as { held: unknown[] };
    expect(before.held).toEqual([]); // the public version's refs don't reach that session
    expect(save("Alpha change here", { evidence: ["transcript:abcd1234#1"] })).toBe(id); // a re-proposal merges its evidence into the original
    expect(getProposal(fx.ctx.db, id)!.proposal.evidence).toContain("transcript:abcd1234#1");
    const after = JSON.parse((await publish(["--json", "--dry-run"], fx.ctx)).stdout) as { held: { id: string; layer: string; why: string }[] };
    expect(after.held).toEqual([{ id, layer: "verbatim", why: "copies transcript text" }]);
    fx.close();
  });

  it("personal", async () => {
    expect(await heldLayer({ rationale: "mail ops@example.org" })).toEqual([{ layer: "personal", why: "contains an email address or a home directory path" }]);
  });

  it("scrubber, after every other layer", async () => {
    const yaml = `${PATTERNS}scrub:\n  extraPatterns:\n    - kind: ticket\n      regex: "WRK-[0-9]{3,6}"\n`;
    expect(await heldLayer({ rationale: "Linked to WRK-99001 upstream." }, { extraYaml: yaml })).toEqual([
      { layer: "scrubber", why: "contains text the scrubber redacts (a secret or a scrub.extraPatterns match)" },
    ]);
  });

  it("the first hit holds: a term, a pattern and an email together report terms", async () => {
    expect(await heldLayer({ rationale: "Acme Care saw GLX-1234 and mailed ops@example.org." })).toEqual([{ layer: "terms", why: "contains a private term" }]);
  });

  it("reads the toolkit corpus at most once, and only when some proposal shares a run with its evidence", async () => {
    const { fx, save } = await ready({ extraYaml: `${DENY}evolve:\n  maxOpenProposals: 4\n` });
    fs.writeFileSync(path.join(fx.transcripts, SESSION), transcriptLine(fx, COPIED));
    let lsFiles = 0;
    const counting: GitRunner = { run: async (args, cwd) => { if (args[0] === "ls-tree") lsFiles++; return fx.deps.git.run(args, cwd); } };
    const ctx = withDeps(fx.ctx, { git: counting });
    const clean = save("A clean proposal");
    await stage([], fx.ctx);
    await publish(["--dry-run"], ctx);
    expect(lsFiles).toBe(0); // nothing shares a run: no corpus
    const a = save("Copying proposal one", { rationale: "The overnight rota swap left two shifts without cover.", evidence: ["transcript:5e55a1d0#1"] });
    const b = save("Copying proposal two", { rationale: "So the overnight rota swap left two shifts without a lead.", evidence: ["transcript:5e55a1d0#1"] });
    await stage([], fx.ctx);
    const r = JSON.parse((await publish(["--dry-run", "--json"], ctx)).stdout) as { held: { id: string; layer: string }[]; heldByLayer: Record<string, number> };
    expect(lsFiles).toBe(1);
    expect(r.held).toEqual([{ id: a, layer: "verbatim", why: "copies transcript text" }, { id: b, layer: "verbatim", why: "copies transcript text" }]);
    expect(r.heldByLayer).toEqual({ verbatim: 2 });
    expect(getProposal(fx.ctx.db, clean)?.status).toBe("staged");
    fx.close();
  });

  it("prints the layer on each held line and a per-layer count, never the matched text", async () => {
    const { fx, save } = await ready({ extraYaml: `${PATTERNS}evolve:\n  maxOpenProposals: 4\n` });
    const t = save("Acme Care scheduling fix");
    const p = save("Pattern proposal", { rationale: "Seen on GLX-1234." });
    const m = save("Mail proposal", { rationale: "Ask joi@example.com about it." });
    await stage([], fx.ctx);
    const r = await publish([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toBe(
      [
        "Nothing to publish: 3 held back.",
        `held ${t} (terms): contains a private term`,
        `held ${p} (patterns): matches a privacy.denyPatterns entry`,
        `held ${m} (personal): contains an email address or a home directory path`,
        "Held by layer: terms 1, patterns 1, personal 1",
        `Held proposals stay held and don't count against the cap. Reject one with: sindri evolve reject <id> --reason "..."`,
        "",
      ].join("\n"),
    );
    expect(r.stdout).not.toMatch(/Acme|GLX|1234|joi@/);
    fx.close();
  });

  it("refuses with SND-EVOLVE-018 before writing anything, also on --dry-run, when a repo is private and the word list is missing", async () => {
    const { fx, save } = await ready();
    const id = save("A clean proposal");
    await stage([], fx.ctx);
    const name = fx.ctx.loaded.profile.tracker.repo;
    const loaded = { ...fx.ctx.loaded, repos: { ...fx.ctx.loaded.repos, globex: { ...fx.ctx.loaded.repos[name], private: true } } };
    const ctx = { ...withDeps(fx.ctx, { env: { ...fx.deps.env, SINDRI_WORDS: path.join(fx.transcripts, "no-words") } }), loaded };
    await expect(publish(["--dry-run"], ctx)).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-018" }));
    await expect(publish([], ctx)).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-018" }));
    expect(fs.existsSync(path.join(fx.repo, REL))).toBe(false);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("staged");
    fx.close();
  });

  it("with nothing staged or held, needs no index or word list: it says nothing is staged (architecture-strategist-2)", async () => {
    const { fx } = await ready();
    const name = fx.ctx.loaded.profile.tracker.repo;
    const loaded = { ...fx.ctx.loaded, repos: { ...fx.ctx.loaded.repos, globex: { ...fx.ctx.loaded.repos[name], private: true } } };
    const ctx = { ...withDeps(fx.ctx, { env: { ...fx.deps.env, SINDRI_WORDS: path.join(fx.transcripts, "no-words") } }), loaded };
    for (const args of [["--dry-run"], []]) {
      const r = await publish(args, ctx);
      expect(r.exitCode).toBe(0);
      expect(r.stdout).toBe("Nothing is staged.\nNext: sindri evolve stage\n");
    }
    fx.close();
  });

  it("with only proposals that have no public version, needs no index or word list: it holds them under generalize (kieran-typescript-reviewer-0)", async () => {
    const { fx, save } = await ready({ extraYaml: `${DENY}evolve:\n  maxGeneralizeAttempts: 1\n` });
    const fresh = save("Alpha change here");
    const givenUp = save("Beta change here");
    fx.ctx.write((epoch) => {
      setStatus(fx.ctx.db, fresh, "held", epoch, fx.deps.now());
      setStatus(fx.ctx.db, givenUp, "held", epoch, fx.deps.now());
      fx.ctx.db.prepare("INSERT INTO evolve_audit (ts, verb, actor, detail, epoch) VALUES ('t', 'generalize-hold', 'a', ?, ?)").run(`${givenUp}: generalize: the judge found domain`, epoch);
    });
    const name = fx.ctx.loaded.profile.tracker.repo;
    const loaded = { ...fx.ctx.loaded, repos: { ...fx.ctx.loaded.repos, globex: { ...fx.ctx.loaded.repos[name], private: true } } };
    const ctx = { ...withDeps(fx.ctx, { env: { ...fx.deps.env, SINDRI_WORDS: path.join(fx.transcripts, "no-words") } }), loaded };
    for (const args of [["--dry-run", "--json"], ["--json"]]) {
      const r = JSON.parse((await publish(args, ctx)).stdout) as { held: { id: string; layer: string; why: string }[] };
      expect(r.held).toEqual([
        { id: fresh, layer: "generalize", why: "not generalized yet; run sindri evolve stage" },
        // a proposal stage gave up on points at the way out, not at stage
        { id: givenUp, layer: "generalize", why: `generalize: gave up after 1 attempts; sindri evolve retry ${givenUp} to try again, or reject it` },
      ]);
    }
    fx.close();
  });

  it("reports how many non-toolkit repos leave private: unset, by count only, and still publishes", async () => {
    const fx = await evolveFixture({ files: FILES, extraYaml: DENY, extraRepos: [{ name: "initrode" }, { name: "zylo" }, { name: "vandelay", private: false }] });
    await init([], fx.ctx);
    git(fx.repo, "checkout", "-q", "-b", "docs/proposals");
    fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
      artifact: "skill:review", kind: "skill-edit", title: "A clean proposal", rationale: "r", evidence: ["pr:12"],
      change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
    }), "telemetry:done-gate", "code", epoch, fx.deps.now())); // no transcripts behind it
    await stage([], fx.ctx);
    const note = "Warning: 2 repo(s) other than the toolkit repo don't set private:, so publish treats them as public (sindri doctor lists them).";
    const dry = await publish(["--dry-run"], fx.ctx);
    expect(dry.stdout).toContain(note);
    expect(dry.stdout).not.toMatch(/initrode|zylo/);
    expect(JSON.parse((await publish(["--dry-run", "--json"], fx.ctx)).stdout)).toMatchObject({ privateUnset: 2 });
    const r = await publish([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain(note);
    fx.close();
  });

  it("releases a held proposal once the profile no longer matches it, without a model call (Review Focus 5)", async () => {
    const { fx, save } = await ready({ extraYaml: PATTERNS });
    const id = save("Alpha change here", { rationale: "Seen on GLX-1234." });
    await stage([], fx.ctx);
    expect(JSON.parse((await publish(["--json"], fx.ctx)).stdout).held[0].layer).toBe("patterns");
    const relaxed = { ...fx.ctx, loaded: { ...fx.ctx.loaded, profile: { ...fx.ctx.loaded.profile, privacy: { ...fx.ctx.loaded.profile.privacy, denyPatterns: [] } } }, io: scriptedEvolveIo(() => { throw new Error("no model call expected"); }) };
    const r = JSON.parse((await publish(["--json"], relaxed)).stdout) as { published: { id: string }[] };
    expect(r.published.map((p) => p.id)).toEqual([id]);
    expect(relaxed.io.calls).toEqual([]);
    expect(getProposal(fx.ctx.db, id)?.status).toBe("published");
    fx.close();
  });
});

