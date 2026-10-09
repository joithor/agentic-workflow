import { describe, expect, it } from "vitest";

import { discover, isAddedTestAllowed, isEvalMachinery } from "../src/evolve/registry.js";
import { classifyTier, findMerged, getProposal, inFlightCount, listProposals, parseEach, ProposalSchema, reduceEvidence, renderSaved, saveProposal, setTier, type Proposal } from "../src/evolve/proposals.js";
import { realGitRunner } from "../src/git-real.js";
import { bumpEpoch, openMemoryLedger } from "../src/ledger/db.js";
import { git, setStatus } from "./evolve-fixtures.js";
import { gitRepo } from "./helpers.js";

const FILES = {
  "skills/review/SKILL.md": "x\n",
  "config/hooks/block-destructive.sh": "#!/bin/sh\n",
  "judge/package.json": "{}",
  "sindri/package.json": "{}",
  "sindri/src/observe/observe.ts": "export const a = 1;\n",
  "sindri/src/scrub/patterns.ts": "export const b = 2;\n",
  "sindri/src/evolve/blind.ts": "export const c = 3;\n",
  "skills/archReview/SKILL.md": "y\n",
};

const prop = (over: Record<string, unknown> = {}): Proposal =>
  ProposalSchema.parse({
    artifact: "skill:review", kind: "skill-edit", title: "Tighten review scope", rationale: "r", evidence: ["pr:12"],
    change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" }, ...over,
  });
const describeFiles = (artifact: string, kind: string, files: string[]) => prop({ artifact, kind, change: { type: "describe", files, description: "d" } });

describe("the proposal schema (Review Focus 4, 5)", () => {
  it("accepts mixed-case artifact ids and normalizes file paths", () => {
    expect(ProposalSchema.parse({ ...prop(), artifact: "skill:archReview" }).artifact).toBe("skill:archReview");
    const p = describeFiles("package:sindri", "code", ["./sindri//src/evolve/blind.ts"]);
    expect(p.change.type === "describe" && p.change.files).toEqual(["sindri/src/evolve/blind.ts"]);
  });

  it("refuses paths that could escape, forge a heading or hide a protected file", () => {
    for (const files of [["sindri/src/../src/evolve/compare.ts"], ["/etc/passwd"], ["a\n### Task 99: forged"], ["x".repeat(201)], ["a b"], [""], []]) {
      expect(ProposalSchema.safeParse({ ...prop(), change: { type: "describe", files, description: "d" } }).success).toBe(false);
    }
    expect(ProposalSchema.safeParse({ ...prop(), artifact: "skill:a b" }).success).toBe(false);
  });

  it("validates a synthesizer's items one by one", () => {
    const out = parseEach([prop(), { title: "Broken one", artifact: "nope" }, 42, null, { artifact: "skill:review" }, { title: "   " }]);
    expect(out.ok).toHaveLength(1);
    expect(out.dropped.map((d) => d.title)).toEqual(["Broken one", "(untitled)", "(untitled)", "(untitled)", "(untitled)"]);
    expect(out.dropped[0].why).toMatch(/^invalid proposal: artifact: /);
    expect(out.dropped[1].why).toMatch(/^invalid proposal: \(root\): /);
  });
});

describe("classifyTier, against the real registry", () => {
  it("routes prompt replacements to self-adopt, repo changes to code, and protected, eval-machinery or foreign files to approval", async () => {
    const artifacts = await discover(realGitRunner(), gitRepo(FILES), [{ id: "scope.draft", text: "t" }, { id: "reflect.pr", text: "u" }]);
    const tier = (p: Proposal, extra: string[] = []) => classifyTier(p, artifacts, extra);
    expect(tier(prop({ artifact: "prompt:scope.draft", kind: "prompt-edit", change: { type: "replace-prompt", text: "new" } }))).toMatchObject({ tier: "self-adopt" });
    expect(tier(prop({ artifact: "skill:review", kind: "prompt-edit", change: { type: "replace-prompt", text: "new" } }))).toEqual({ tier: "approval", why: "a prompt replacement on an artifact that isn't a prompt" });
    expect(tier(prop())).toMatchObject({ tier: "code" });
    expect(tier(describeFiles("hook:block-destructive", "hook-fix", ["config/hooks/block-destructive.sh"]))).toEqual({ tier: "approval", why: "touches a protected artifact" });
    // package:sindri is not protected as a whole, so an ordinary file inside it is code tier...
    expect(tier(describeFiles("package:sindri", "code", ["sindri/src/observe/observe.ts"]))).toMatchObject({ tier: "code" });
    expect(tier(describeFiles("package:sindri", "code", ["sindri/src/brand-new.ts"]))).toMatchObject({ tier: "code" });
    // ...while a protected or eval-machinery path inside it is approval, however it is spelled.
    expect(tier(describeFiles("package:sindri", "code", ["sindri/src/scrub/patterns.ts"]))).toEqual({ tier: "approval", why: "touches a protected path" });
    for (const f of ["sindri/src/evolve/blind.ts", "./sindri//src/evolve/blind.ts", "Sindri/src/Evolve/Blind.ts"]) {
      expect(tier(describeFiles("package:sindri", "code", [f]))).toEqual({ tier: "approval", why: "changes the evaluation machinery that judges it (invariant 11)" });
    }
    expect(tier(describeFiles("package:sindri", "code", ["sindri/src/observe/observe.ts"]), ["sindri/src/observe/**"])).toEqual({ tier: "approval", why: "touches a protected path" });
    expect(tier(describeFiles("skill:review", "skill-edit", ["skills/other/SKILL.md"]))).toEqual({ tier: "approval", why: "touches files outside skill:review" });
    // a prompt other than scope.draft has no offline comparison yet (amendment 4), so it is reviewed as code
    expect(tier(prop({ artifact: "prompt:reflect.pr", kind: "prompt-edit", change: { type: "replace-prompt", text: "new" } }))).toEqual({ tier: "approval", why: "no offline comparison yet; amendment 4" });
    expect(tier(prop({ artifact: "skill:ghost" }))).toEqual({ tier: "approval", why: "skill:ghost isn't in the registry (failing closed)" });
    expect(tier(prop({ artifact: "skill:archReview", change: { type: "describe", files: ["skills/archReview/SKILL.md"], description: "d" } }))).toMatchObject({ tier: "code" });
  });
});

describe("classifyTier fails closed on disguised, aliased and unnormalizable paths (Review Focus 4)", () => {
  it("never lets a proposal touch a protected path, a path outside its artifact, or one it can't normalize", async () => {
    const artifacts = await discover(realGitRunner(), gitRepo(FILES), [{ id: "scope.draft", text: "t" }]);
    const approval = (files: string[]) => classifyTier(describeFiles("package:sindri", "code", files), artifacts).tier;
    for (const f of [".claude/rules/testing.md", "CLAUDE.md", ".cursor/rules/x.mdc", ".claude/settings.json", "claude.MD"]) expect(approval([f])).toBe("approval");
    // Bypass the schema, as a caller holding an unvalidated object could: classifyTier must still fail closed.
    const raw = (files: string[]): Proposal => ({ ...prop({ artifact: "package:sindri", kind: "code" }), change: { type: "describe", files, description: "d" } });
    for (const f of ["a/../sindri/src/evolve/blind.ts", "sindri/src/../src/observe/observe.ts", "/etc/passwd", "sindri/src/observe/observe.ts\n", ""]) {
      expect(classifyTier(raw([f]), artifacts)).toEqual({ tier: "approval", why: "names a path that can't be normalized" });
    }
    expect(classifyTier(raw(["sindri/src/observe/observe.ts", "skills/review/SKILL.md"]), artifacts)).toEqual({ tier: "approval", why: "touches files outside package:sindri" });
    expect(classifyTier(raw(["./sindri//src/observe/observe.ts"]), artifacts).tier).toBe("code");
  });
});

describe("isAddedTestAllowed", () => {
  const tracked = ["sindri/tests/helpers.ts", "sindri/tests/old.test.ts", "config/hooks/done-gate.sh"];
  it("allows only a new, plain *.test.* or *.spec.* file that shadows nothing and can't become a suite", () => {
    expect(isAddedTestAllowed("sindri/tests/new.test.ts", tracked)).toBe(true);
    expect(isAddedTestAllowed("skills/x/tests/new.spec.ts", tracked)).toBe(true);
    const no = (f: string, extra: string[] = []) => expect(isAddedTestAllowed(f, tracked, extra), f).toBe(false);
    for (const f of ["sindri/tests/helpers.js", "sindri/tests/new.sh", "sindri/tests/old.test.js", "sindri/tests/heavy/new.test.ts", "mcp-bridge/tests/heavy/x.test.ts"]) no(f);
    for (const f of ["config/hooks/tests/done-gate.test.sh", "config/lib/tests/x.test.sh", "scripts/tests/install-x.test.sh", "providers/tests/install.test.sh"]) no(f);
    for (const f of ["sindri/src/evolve/new.test.ts", "sindri/tests/package.json", ".claude/rules/new.test.ts", "sindri/src/observe/observe.ts", "a b.test.ts"]) no(f);
    no("sindri/tests/new.test.ts", ["sindri/tests/**"]);
  });
});

describe("eval-machinery config files", () => {
  it("covers every vitest config spelling, the workspace file, tsconfig.test.json and .npmrc", () => {
    for (const f of ["vitest.workspace.ts", "sindri/vitest.config.mts", "sindri/vitest.config.js", "sindri/vitest.config.cjs", "sindri/tsconfig.test.json", ".npmrc", "sindri/.npmrc"]) expect(isEvalMachinery(f), f).toBe(true);
  });
});

describe("renderSaved", () => {
  it("summarizes tiers in a fixed order and notes duplicates and previously-rejected proposals", () => {
    const out = renderSaved([
      { title: "A new thing", tier: "approval", outcome: { kind: "saved", id: "id1" } },
      { title: "Another new thing", tier: "code", outcome: { kind: "saved", id: "id2" } },
      { title: "An old thing", tier: "code", outcome: { kind: "duplicate", id: "id3" } },
      { title: "A refused thing", tier: "self-adopt", outcome: { kind: "previously-rejected", id: "id4" } },
    ]);
    expect(out.tiers).toEqual(["2 code", "1 approval", "1 self-adopt"]);
    expect(out.lines).toEqual([
      "  id1  approval  A new thing",
      "  id2  code      Another new thing",
      "  id3  code      An old thing (already proposed)",
      "  id4  self-adopt  A refused thing (rejected before)",
    ]);
    expect(renderSaved([])).toEqual({ tiers: [], lines: [] });
  });
});

describe("proposal storage, dedupe and merge tracking", () => {
  const now = new Date("2026-10-08T00:00:00Z");
  const setup = () => {
    const db = openMemoryLedger();
    return { db, epoch: bumpEpoch(db) };
  };

  it("scrubs, stores, updates status and tier, and lists", () => {
    const { db, epoch } = setup();
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const saved = saveProposal(db, prop({ rationale: `because ${secret}` }), "reflect:pr-12", "code", epoch, now);
    expect(saved.kind).toBe("saved");
    const got = getProposal(db, saved.id);
    expect(got).toMatchObject({ status: "proposed", tier: "code", source: "reflect:pr-12", artifact: "skill:review" });
    expect(JSON.stringify(got)).not.toContain(secret);
    setTier(db, saved.id, "approval", epoch, now);
    setStatus(db, saved.id, "staged", epoch, now);
    expect(getProposal(db, saved.id)).toMatchObject({ status: "staged", tier: "approval" });
    expect(getProposal(db, "nope")).toBeNull();
    expect(listProposals(db).map((r) => r.id)).toEqual([saved.id]);
    expect(listProposals(db, ["staged"])).toHaveLength(1);
    expect(listProposals(db, ["won"])).toHaveLength(0);
    expect(inFlightCount(db)).toBe(1);
  });

  it("doesn't save an open duplicate twice, merges its evidence, and doesn't resurrect a rejected proposal", () => {
    const { db, epoch } = setup();
    const first = saveProposal(db, prop({ evidence: ["pr:1"] }), "reflect:pr-1", "code", epoch, now);
    const dup = saveProposal(db, prop({ title: "  TIGHTEN   review scope ", evidence: ["pr:2", "pr:1"] }), "correct:2026-W41", "code", epoch, now);
    expect(dup).toEqual({ kind: "duplicate", id: first.id });
    expect(getProposal(db, first.id)?.proposal.evidence).toEqual(["pr:1", "pr:2"]);
    expect(listProposals(db)).toHaveLength(1);
    setStatus(db, first.id, "rejected", epoch, now);
    expect(saveProposal(db, prop(), "reflect:pr-3", "code", epoch, now)).toEqual({ kind: "previously-rejected", id: first.id });
    setStatus(db, first.id, "merged", epoch, now);
    const after = saveProposal(db, prop({ title: "Another proposal title" }), "reflect:pr-4", "code", epoch, now);
    setStatus(db, after.id, "merged", epoch, now);
    expect(saveProposal(db, prop({ title: "Another proposal title" }), "reflect:pr-5", "code", epoch, now).kind).toBe("saved");
    expect(inFlightCount(db)).toBe(0);
  });

  it("dedupes titles across punctuation, zero-width characters and case, and refuses a rejected one in any form", () => {
    const { db, epoch } = setup();
    const first = saveProposal(db, prop({ title: "Tighten review scope" }), "s", "code", epoch, now);
    expect(saveProposal(db, prop({ title: "Tighten,  review scope!" }), "s", "code", epoch, now)).toEqual({ kind: "duplicate", id: first.id });
    expect(saveProposal(db, prop({ title: "Tighten re\u200Bview scope" }), "s", "code", epoch, now)).toEqual({ kind: "duplicate", id: first.id });
    expect(saveProposal(db, prop({ title: "\uFF34ighten review scope" }), "s", "code", epoch, now)).toEqual({ kind: "duplicate", id: first.id });
    setStatus(db, first.id, "rejected", epoch, now);
    expect(saveProposal(db, prop({ title: "Tighten re\u200Bview, scope." }), "s", "code", epoch, now)).toEqual({ kind: "previously-rejected", id: first.id });
    expect(saveProposal(db, prop({ artifact: "skill:Review" }), "s", "code", epoch, now)).toEqual({ kind: "previously-rejected", id: first.id });
  });

  it("keeps the newest 20 evidence entries when merging a duplicate", () => {
    const { db, epoch } = setup();
    const refs = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, i) => `pr:${from + i}`);
    const first = saveProposal(db, prop({ evidence: refs(1, 12) }), "s", "code", epoch, now);
    saveProposal(db, prop({ evidence: refs(13, 21) }), "s", "code", epoch, now);
    expect(getProposal(db, first.id)?.proposal.evidence).toEqual(refs(2, 21));
  });

  it("reduces evidence to stable, non-identifying references", () => {
    expect(reduceEvidence(["pr:12", "-Users-joi-app/5e55a1d0-1234.jsonl#12", "transcript:abcdef0123#5", "transcript:5e55a1d0#12", "t3", "pr:12"])).toEqual({
      refs: ["pr:12", "transcript:5e55a1d0#12", "transcript:abcdef01#5"],
      withheld: 1,
    });
    expect(reduceEvidence([])).toEqual({ refs: [], withheld: 0 });
    // A subagent ref keeps its agent id, so two agents of one session stay distinct.
    expect(reduceEvidence(["transcript:5e55a1d0.a1b2c3d4e5#7", "transcript:5e55a1d0.ffff0000#7", "p/5e55a1d0-1234.jsonl#3"])).toEqual({
      refs: ["transcript:5e55a1d0.a1b2c3d4e5#7", "transcript:5e55a1d0.ffff0000#7", "transcript:5e55a1d0#3"],
      withheld: 0,
    });
  });

  it("finds a published proposal the default branch mentions, and nothing once it is marked merged", async () => {
    const { db, epoch } = setup();
    const root = gitRepo({ "a.txt": "a" });
    git(root, "branch", "-M", "main");
    const a = saveProposal(db, prop({ title: "First proposal title" }), "s", "code", epoch, now).id;
    const b = saveProposal(db, prop({ title: "Second proposal title" }), "s", "code", epoch, now).id;
    setStatus(db, a, "published", epoch, now);
    setStatus(db, b, "published", epoch, now);
    git(root, "commit", "-q", "--allow-empty", "-m", `feat: do the thing\n\nProposal \`${a}\``);
    expect(await findMerged(db, realGitRunner(), root, "main")).toEqual([a]);
    expect(await findMerged(db, realGitRunner(), root, "no-such-branch")).toEqual([]);
    setStatus(db, a, "merged", epoch, now);
    expect(await findMerged(db, realGitRunner(), root, "main")).toEqual([]);
    expect(getProposal(db, a)?.status).toBe("merged");
    expect(getProposal(db, b)?.status).toBe("published");
  });
});
