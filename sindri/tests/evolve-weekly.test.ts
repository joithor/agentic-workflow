import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { awStateDir } from "../src/deps.js";
import { audit } from "../src/evolve/audit.js";
import { init } from "../src/evolve/cmd/registry.js";
import { weekly } from "../src/evolve/cmd/weekly.js";
import { ProposalSchema, saveProposal } from "../src/evolve/proposals.js";
import { heavyLockDir } from "../src/index/heavy-lock.js";
import { evolveFixture, fakeProc, git, scriptedEvolveIo, withDeps } from "./evolve-fixtures.js";

const FILES = { "judge/package.json": "{}", "skills/review/SKILL.md": "x\n" };
const view = JSON.stringify({ title: "Fix", body: "B", headRefName: "feat/x", files: [{ path: "a.ts" }], state: "MERGED", mergedAt: "2026-10-07T00:00:00Z", author: { login: "joi-t" } });

function gh(list: string, over: { listCode?: number; open?: boolean } = {}) {
  return fakeProc((argv) => {
    if (argv[0] !== "gh") return { stdout: "fine" }; // a suite
    if (argv[1] === "api") return { stdout: '{"login":"joi-t"}' };
    if (argv[2] === "list") return over.listCode === undefined ? { stdout: list } : { code: over.listCode, stderr: "rate limited\nretry later" };
    if (argv[2] === "view") return { stdout: over.open === true ? view.replace('"MERGED"', '"OPEN"').replace(/"mergedAt":"[^"]*"/, '"mergedAt":null') : view };
    return { stdout: "diff --git a/a.ts b/a.ts\n" };
  });
}

async function ready(proc = gh('[{"number":12},{"number":13}]')) {
  const io = scriptedEvolveIo((c) => (c.model === "sonnet" ? { findings: [] } : { accepted: [], rejected: [], backlog: [] }), proc);
  const fx = await evolveFixture({ files: FILES, io });
  git(fx.repo, "remote", "add", "origin", "https://github.com/acme/toolkit.git");
  await init([], fx.ctx);
  fx.ctx.write((epoch) => audit(fx.ctx.db, fx.deps, "reflect", "pr-13", epoch));
  return { fx, proc };
}

describe("sindri evolve weekly", () => {
  it("runs the five steps in order, reflects only on merged PRs that haven't been reflected on, and exits 0 when all are ok", async () => {
    const { fx, proc } = await ready();
    const r = await weekly([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(
      [
        `ok   telemetry: No hook fires found in sessions of ${fx.repo} since 2026-10-01.`,
        "ok   reflect: reflected on 1 merged PR(s): #12 ok",
        `ok   correct: No repeated corrections in sessions of ${fx.repo} since 2026-10-01: labeled 0 turns: 0 design, 0 process, 0 restate, 0 scope (0 label errors).`,
        "ok   check: Checked 1 suite(s): 1 ok, 0 FAILED.",
        "ok   stage: Nothing to stage.",
        "Weekly: 5 steps, 5 ok, 0 need attention.",
        "Next: sindri evolve proposals",
        "",
      ].join("\n"),
    );
    const list = proc.calls.find((c) => c.argv[2] === "list");
    expect(list?.argv).toEqual(["gh", "pr", "list", "--state", "merged", "--search", "merged:>=2026-10-01", "--json", "number", "--limit", "100", "--repo", "acme/toolkit"]);
    expect(proc.calls.filter((c) => c.argv[2] === "view").map((c) => c.argv[3])).toEqual(["12"]);
    fx.close();
  });

  it("isolates failures: a failing step is reported, the rest still run, and the exit code is 1", async () => {
    const { fx } = await ready(gh("", { listCode: 1 }));
    fs.writeFileSync(path.join(fx.repo, "untracked.txt"), "x");
    fx.ctx.write((epoch) =>
      saveProposal(fx.ctx.db, ProposalSchema.parse({
        artifact: "skill:review", kind: "skill-edit", title: "Tighten review scope", rationale: "r", evidence: ["pr:1"],
        change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
      }), "reflect:pr-1", "code", epoch, fx.deps.now()));
    const r = await weekly([], fx.ctx);
    expect(r.exitCode).toBe(1);
    const lines = r.stdout.split("\n");
    expect(lines[0]).toBe(`ok   telemetry: No hook fires found in sessions of ${fx.repo} since 2026-10-01.`);
    expect(lines[1]).toBe("FAIL reflect: SND-EVOLVE-003 gh pr list failed: rate limited");
    expect(lines[2]).toBe(`ok   correct: No repeated corrections in sessions of ${fx.repo} since 2026-10-01: labeled 0 turns: 0 design, 0 process, 0 restate, 0 scope (0 label errors).`);
    expect(lines[3]).toBe("attn check: skipped: the working tree has uncommitted changes, so suites wouldn't match a commit");
    expect(lines[4]).toMatch(/^ok {3}stage: Staged 1 proposal\(s\) \(0 approval tier\) in .*\.$/);
    expect(lines.slice(5)).toEqual(["Weekly: 5 steps, 3 ok, 2 need attention.", "Next: fix the lines above, then rerun sindri evolve weekly (or just the failing step)", ""]);
    expect(fx.ctx.db.prepare("SELECT status FROM proposals").get()).toEqual({ status: "staged" });
    fx.close();
  });

  it("counts a PR that can't be reflected on as a failed reflect step", async () => {
    const { fx } = await ready(gh('[{"number":12}]', { open: true }));
    const r = await weekly([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("FAIL reflect: reflected on 1 merged PR(s): #12 FAIL");
    fx.close();
  });

  it("points at publish when something was staged, and reports a step that throws", async () => {
    const { fx } = await ready();
    fx.ctx.write((epoch) =>
      saveProposal(fx.ctx.db, ProposalSchema.parse({
        artifact: "skill:review", kind: "skill-edit", title: "Tighten review scope", rationale: "r", evidence: ["pr:1"],
        change: { type: "describe", files: ["skills/review/SKILL.md"], description: "d" },
      }), "reflect:pr-1", "code", epoch, fx.deps.now()));
    const r = await weekly([], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout.endsWith("Weekly: 5 steps, 5 ok, 0 need attention.\nNext: sindri evolve publish\n")).toBe(true);
    const broken = withDeps(fx.ctx, { git: { run: async () => { throw new Error("git exploded"); } } });
    const b = await weekly([], broken);
    expect(b.stdout).toContain("FAIL reflect: git exploded");
    expect(b.stdout).toContain("FAIL check: git exploded");
    fx.close();
  });

  it("skips everything while a heavy job holds the box-wide lock", async () => {
    const { fx, proc } = await ready();
    fs.mkdirSync(heavyLockDir(awStateDir(fx.deps), fx.deps.env), { recursive: true });
    const before = proc.calls.length;
    const r = await weekly([], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toBe("Skipped: a heavy job holds the box-wide lock. Nothing ran.\nNext: rerun sindri evolve weekly later\n");
    expect(proc.calls).toHaveLength(before);
    fx.close();
  });

  it("prints the plan with --dry-run and runs nothing", async () => {
    const { fx, proc } = await ready();
    const r = await weekly(["--dry-run"], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe("Weekly plan (dry run): telemetry --since 7d; reflect on 1 merged PR(s) (#12); correct --since 7d; check --changed; stage. Nothing ran.\nNext: sindri evolve weekly\n");
    expect(proc.calls.filter((c) => c.argv[2] !== "list" && c.argv[0] === "gh")).toHaveLength(0);
    expect(JSON.parse((await weekly(["--dry-run", "--json"], fx.ctx)).stdout)).toMatchObject({ dryRun: true, prs: [12] });
    const bad = await ready(gh("", { listCode: 1 }));
    expect((await weekly(["--dry-run"], bad.fx.ctx)).stdout).toContain("reflect: couldn't list merged PRs (SND-EVOLVE-003 gh pr list failed: rate limited)");
    fx.close();
    bad.fx.close();
  });

  it("says when no merged PR needs a reflection", async () => {
    const { fx } = await ready(gh("[]"));
    const r = await weekly([], fx.ctx);
    expect(r.stdout).toContain("ok   reflect: no merged PRs from the last 7 days need a reflection");
    fx.close();
  });

  it("reports a step that throws a bare string", async () => {
    const { fx } = await ready();
    const broken = withDeps(fx.ctx, { git: { run: async () => { throw "boom"; } } });
    expect((await weekly([], broken)).stdout).toContain("FAIL reflect: boom");
    fx.close();
  });

  it("plans no reflections in a --dry-run when nothing merged", async () => {
    const { fx } = await ready(gh("[]"));
    const r = await weekly(["--dry-run"], fx.ctx);
    expect(r.stdout).toBe("Weekly plan (dry run): telemetry --since 7d; reflect on 0 merged PR(s); correct --since 7d; check --changed; stage. Nothing ran.\nNext: sindri evolve weekly\n");
    expect(JSON.parse((await weekly(["--dry-run", "--json"], fx.ctx)).stdout)).toMatchObject({ dryRun: true, prs: [] });
    fx.close();
  });
});
