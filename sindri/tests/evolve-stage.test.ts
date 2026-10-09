import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { parsePlan } from "../src/adapters/plan-file/parse.js";
import { init } from "../src/evolve/cmd/registry.js";
import { stage } from "../src/evolve/cmd/stage.js";
import { getProposal, ProposalSchema, saveProposal, setStatus, stagedFile, type ProposalStatus, type Tier } from "../src/evolve/proposals.js";
import { PROMPTS } from "../src/evolve/prompts.js";
import { evolveFixture } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "skills/review/SKILL.md": "x\n", "sindri/package.json": "{}", "sindri/src/observe/observe.ts": "export const a = 1;\n" };

async function ready(cap = 2) {
  const fx = await evolveFixture({ files: FILES, extraYaml: `evolve:\n  maxOpenProposals: ${cap}\n`, prompts: () => PROMPTS });
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
