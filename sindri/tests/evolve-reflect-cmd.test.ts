import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { init } from "../src/evolve/cmd/registry.js";
import { reflectCommand } from "../src/evolve/cmd/reflect.js";
import { isHoldout, saveReplay } from "../src/evolve/corpus.js";
import { evolveFixture, fakeProc, git, scriptedEvolveIo, type EvolveFixture } from "./evolve-fixtures.js";

const FILES = { "config/hooks/done-gate.sh": "#!/bin/sh\n", "skills/review/SKILL.md": "x\n" };
const finding = { findings: [{ title: "Review skips tests", evidence: ["transcript:5e55a1d0#2"], suggestion: "check tests", artifact: "skill:review" }] };
const proposal = (artifact: string, title: string, files: string[]) => ({ artifact, kind: artifact.startsWith("hook") ? "hook-fix" : "skill-edit", title, rationale: "seen twice", evidence: ["transcript:5e55a1d0#2", "pr:12"], change: { type: "describe", files, description: "add a step" } });

const view = (over: Record<string, unknown> = {}) =>
  JSON.stringify({ title: "Fix the thing", body: "B", headRefName: "feat/x", files: [{ path: "a.ts" }], state: "MERGED", mergedAt: "2026-10-07T00:00:00Z", author: { login: "joi-t" }, ...over });
const gh = (over: Record<string, unknown> = {}) =>
  fakeProc((argv) => {
    if (argv[1] === "api") return { stdout: '{"login":"joi-t"}' };
    if (argv.includes("view")) return { stdout: view(over) };
    return { stdout: "diff --git a/a.ts b/a.ts\n+x\n" };
  });

async function ready(synth: unknown, proc = gh()) {
  const fx = await evolveFixture({
    files: FILES,
    io: scriptedEvolveIo((c) => (c.model === "sonnet" ? finding : synth), proc),
  });
  git(fx.repo, "remote", "add", "origin", "https://github.com/acme/toolkit.git");
  await init([], fx.ctx);
  const lines = ["user", "assistant"].map((type, i) => JSON.stringify({ type, gitBranch: "feat/x", cwd: fx.repo, timestamp: "2026-10-06T10:00:00Z", message: { content: i === 0 ? "please fix the thing" : [{ type: "text", text: "fixed it" }] } }));
  fs.writeFileSync(path.join(fx.transcripts, "5e55a1d0-aaaa.jsonl"), `${lines.join("\n")}\n`);
  return { fx, proc };
}
const synthesis = (accepted: unknown[]) => ({ accepted, rejected: [{ title: "Style nit", why: "taste" }], backlog: [{ title: "Maybe later", why: "thin" }, { title: "Also later", why: "thin" }, { title: "And later", why: "thin" }] });
const sorted = (fx: EvolveFixture): string[] => (fx.ctx.db.prepare("SELECT id FROM proposals ORDER BY id").all() as { id: string }[]).map((r) => r.id);

describe("sindri evolve reflect", () => {
  it("reflects on a merged PR, saves each accepted proposal with its tier, and doesn't repeat itself", async () => {
    const { fx, proc } = await ready(synthesis([proposal("skill:review", "Review must run the suite", ["skills/review/SKILL.md"]), proposal("hook:done-gate", "Done gate should check tests", ["config/hooks/done-gate.sh"])]));
    const r = await reflectCommand(["--pr", "12"], fx.ctx);
    expect(r.exitCode).toBe(0);
    const [first, second] = (fx.ctx.db.prepare("SELECT id, tier, title FROM proposals ORDER BY title").all() as { id: string; tier: string; title: string }[]);
    expect(first).toMatchObject({ title: "Done gate should check tests", tier: "approval" });
    expect(second).toMatchObject({ title: "Review must run the suite", tier: "code" });
    expect(r.stdout).toBe(
      [
        "Reflected on PR #12: 2 accepted (1 code, 1 approval), 1 rejected, 3 backlog.",
        `  ${second.id}  code      Review must run the suite`,
        `  ${first.id}  approval  Done gate should check tests`,
        `Next: sindri evolve show ${second.id}`,
        "",
      ].join("\n"),
    );
    expect(proc.calls.some((c) => c.argv.join(" ").includes("--repo acme/toolkit"))).toBe(true);
    expect((fx.io as ReturnType<typeof scriptedEvolveIo>).calls[0].input).toContain('<untrusted id="transcript:5e55a1d0#1" role="human">please fix the thing</untrusted>');
    const again = await reflectCommand(["--pr", "12"], fx.ctx);
    expect(again.stdout).toBe(`Already reflected on PR #12: ${sorted(fx).join(", ")}.\nNext: sindri evolve proposals\n`);
    fx.close();
  });

  it("escapes control characters in a model-written title before it reaches the terminal", async () => {
    const { fx } = await ready(synthesis([proposal("skill:review", "Review must run \u001b]52;c;Zm9v\u0007 the suite", ["skills/review/SKILL.md"])]));
    const r = await reflectCommand(["--pr", "12"], fx.ctx);
    expect(r.stdout).not.toMatch(/[\u0000-\u0008\u000B-\u001F\u007F-\u009F]/);
    expect(r.stdout).toContain("Review must run \\u{001B}]52;c;Zm9v\\u{0007} the suite");
    fx.close();
  });

  it("notes a repeated proposal as already proposed, and remembers a PR that produced nothing", async () => {
    const dupe = synthesis([proposal("skill:review", "Review must run the suite", ["skills/review/SKILL.md"])]);
    const { fx } = await ready(dupe);
    await reflectCommand(["--pr", "12"], fx.ctx);
    const r = await reflectCommand(["--pr", "13"], fx.ctx);
    expect(r.stdout).toMatch(/^Reflected on PR #13: 1 accepted \(1 code\), 1 rejected, 3 backlog\.\n {2}[0-9a-z]{26} {2}code {6}Review must run the suite \(already proposed\)\nNext: sindri evolve proposals\n$/);
    const empty = await ready(synthesis([]));
    const none = await reflectCommand(["--pr", "14"], empty.fx.ctx);
    expect(none.stdout).toBe("Reflected on PR #14: 0 accepted, 1 rejected, 3 backlog.\nNext: sindri evolve proposals\n");
    expect((await reflectCommand(["--pr", "14"], empty.fx.ctx)).stdout).toBe("Already reflected on PR #14 (nothing was proposed).\nNext: sindri evolve proposals\n");
    fx.close();
    empty.fx.close();
  });

  it("refuses bad input and unmerged or foreign PRs, and reports a partial result", async () => {
    const { fx } = await ready(synthesis([]));
    await expect(reflectCommand([], fx.ctx)).rejects.toThrow(/usage: sindri evolve reflect --pr <n>/);
    await expect(reflectCommand(["--pr", "abc"], fx.ctx)).rejects.toThrow(/usage/);
    const open = await ready(synthesis([]), gh({ state: "OPEN", mergedAt: null }));
    await expect(reflectCommand(["--pr", "5"], open.fx.ctx)).rejects.toThrow(/PR #5 isn't merged/);
    const foreign = await ready(synthesis([]), gh({ author: { login: "mallory" } }));
    await expect(reflectCommand(["--pr", "5"], foreign.fx.ctx)).rejects.toThrow(/written by mallory/);
    const bare = await evolveFixture({ files: FILES, io: scriptedEvolveIo(() => null, gh()) });
    await expect(reflectCommand(["--pr", "5"], bare.ctx)).rejects.toThrow(/registry is empty/);
    git(bare.repo, "remote", "add", "origin", "https://gitlab.com/a/b.git");
    await init([], bare.ctx);
    await expect(reflectCommand(["--pr", "5"], bare.ctx)).rejects.toThrow(/no GitHub remote called origin/);
    const partial = await ready({ nope: 1 });
    const r = await reflectCommand(["--pr", "6"], partial.fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Reflected on PR #6: 0 accepted, 0 rejected, 0 backlog.");
    expect(r.stdout).toContain("Partial result: synthesizer: the model's answer didn't match the schema");
    expect(r.stdout).toContain("Next: rerun sindri evolve reflect --pr 6 once the cause above is fixed");
    expect(partial.fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit").get()).toEqual({ c: 0 });
    for (const f of [fx, open.fx, foreign.fx, bare, partial.fx]) f.close();
  });

  it("a partial run doesn't block its own rerun; only a complete run writes the marker (S7)", async () => {
    let reviewerCalls = 0;
    let failSecond = true;
    const fx = await evolveFixture({
      files: FILES,
      io: scriptedEvolveIo((c) => {
        if (c.model !== "sonnet") return synthesis([proposal("skill:review", "Review must run the suite", ["skills/review/SKILL.md"])]);
        reviewerCalls++;
        return failSecond && reviewerCalls === 2 ? { nope: 1 } : finding;
      }, gh()),
    });
    git(fx.repo, "remote", "add", "origin", "https://github.com/acme/toolkit.git");
    await init([], fx.ctx);
    const partial = await reflectCommand(["--pr", "12", "--json"], fx.ctx);
    expect(partial.exitCode).toBe(1);
    expect(JSON.parse(partial.stdout)).toMatchObject({ pr: 12, incomplete: true });
    expect(sorted(fx)).toHaveLength(1);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS c FROM evolve_audit WHERE verb = 'reflect'").get()).toEqual({ c: 0 });
    failSecond = false;
    const rerun = await reflectCommand(["--pr", "12"], fx.ctx);
    expect(rerun.exitCode).toBe(0);
    expect(rerun.stdout).toMatch(/^Reflected on PR #12: 1 accepted \(1 code\), 1 rejected, 3 backlog\.\n {2}[0-9a-z]{26} {2}code {6}Review must run the suite \(already proposed\)\nNext: sindri evolve proposals\n$/);
    expect(sorted(fx)).toHaveLength(1);
    expect((await reflectCommand(["--pr", "12"], fx.ctx)).stdout).toBe(`Already reflected on PR #12: ${sorted(fx).join(", ")}.\nNext: sindri evolve proposals\n`);
    fx.close();
  });

  it("leaves out turns that quote a holdout brief's title", async () => {
    const { fx } = await ready(synthesis([]));
    const held = Array.from({ length: 600 }, (_, i) => `item-${i}`).find(isHoldout) as string;
    // the corpus only counts an item the ledger has a recorded run for
    fx.ctx.db.prepare("INSERT INTO scope_runs (run_id, subject, mode, ts, status, rounds, surfaces, tokens, out_path, epoch) VALUES (?, 's', 'scope', 't', 'complete', 1, 1, 1, '/o', 1)").run(held);
    saveReplay(fx.deps, {
      id: held, artifact: "scope.draft", createdAt: "2026-10-08T00:00:00Z",
      brief: { ref: "file:/b.md", kind: "brief", title: "Quarterly staffing overhaul", text: "t", author: null, createdAt: null, trust: "trusted" },
      records: [], outcome: { status: "complete", surfaces: 1, recall: null },
    });
    fs.appendFileSync(path.join(fx.transcripts, "5e55a1d0-aaaa.jsonl"), `${JSON.stringify({ type: "user", gitBranch: "feat/x", cwd: fx.repo, message: { content: "about the Quarterly Staffing Overhaul project" } })}\n`);
    await reflectCommand(["--pr", "12"], fx.ctx);
    const input = (fx.io as ReturnType<typeof scriptedEvolveIo>).calls[0].input;
    expect(input).toContain("please fix the thing");
    expect(input).not.toContain("Quarterly Staffing Overhaul");
    fx.close();
  });

  it("never sends harness-injected turns (isMeta) to the reviewers (I3)", async () => {
    const { fx } = await ready(synthesis([]));
    const meta = (text: string) => JSON.stringify({ type: "user", isMeta: true, gitBranch: "feat/x", cwd: fx.repo, timestamp: "2026-10-06T10:01:00Z", message: { content: text } });
    fs.appendFileSync(path.join(fx.transcripts, "5e55a1d0-aaaa.jsonl"), `${meta("Stop hook feedback:\n[/r/config/hooks/done-gate.sh # aw:done-gate]: Claiming done")}\n${meta("Base directory for this skill: /skills/review\n\n# Review skill body")}\n`);
    await reflectCommand(["--pr", "12"], fx.ctx);
    const sent = (fx.io as ReturnType<typeof scriptedEvolveIo>).calls[0].input;
    expect(sent).toContain("please fix the thing");
    expect(sent).not.toContain("Stop hook feedback");
    expect(sent).not.toContain("Review skill body");
    fx.close();
  });

  it("keeps the audit marker key exact when a profile scrub pattern would match it", async () => {
    const proc = gh();
    const fx = await evolveFixture({ files: FILES, extraYaml: "scrub:\n  extraPatterns:\n    - kind: marker\n      regex: \"pr-[0-9]+\"\n", io: scriptedEvolveIo((c) => (c.model === "sonnet" ? finding : synthesis([])), proc) });
    git(fx.repo, "remote", "add", "origin", "https://github.com/acme/toolkit.git");
    await init([], fx.ctx);
    await reflectCommand(["--pr", "12"], fx.ctx);
    expect(fx.ctx.db.prepare("SELECT detail FROM evolve_audit WHERE verb = 'reflect'").all()).toEqual([{ detail: "pr-12" }]);
    expect((await reflectCommand(["--pr", "12"], fx.ctx)).stdout).toContain("Already reflected on PR #12");
    fx.close();
  });
});
