import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { init } from "../src/evolve/cmd/registry.js";
import { publish, stage } from "../src/evolve/cmd/stage.js";
import { GENERALIZE_ROLES } from "../src/evolve/generalize.js";
import { ProposalSchema, saveProposal } from "../src/evolve/proposals.js";
import { indexPath, openIndex } from "../src/index/db.js";
import { evolveFixture, git, scriptedEvolveIo, unescapeFence } from "./evolve-fixtures.js";
import { seedIndex } from "./index-fixtures.js";
import { tempDir } from "./helpers.js";

// A made-up workplace. Nothing below may reach the plan file.
const SECRETS = [
  "Globex", "quarbleScheduler", "GLX-1234", "zentrovaRota", "overnight rota swap left two shifts without a charge nurse",
  "night handover sheet listed the wrong ward for three patients",
];
const LATER = "then the night handover sheet listed the wrong ward for three patients again";
const STORY = "the quarbleScheduler for Globex broke on GLX-1234 because the overnight rota swap left two shifts without a charge nurse on zentrovaRota";
const PRIVACY = "privacy:\n  denyTerms:\n    - Globex\n  denyPatterns:\n    - \"GLX-\\\\d+\"\n";
// The ISO-week Monday the fixture clock produces (same as evolve-publish.test.ts).
const REL = "docs/superpowers/plans/2026-10-05-sindri-plan-proposals.md";

async function world(rewriteTitle: string, json: boolean) {
  const words = path.join(tempDir("w-"), "words");
  fs.writeFileSync(words, ["the", "for", "broke", "because", "overnight", "rota", "swap", "left", "shifts", "without", "charge", "nurse", "retry", "scheduler", "checks"].join("\n"));
  const io = scriptedEvolveIo((c) => {
    if (c.role === GENERALIZE_ROLES.rewrite) {
      const p = JSON.parse(unescapeFence(/<untrusted id="proposal">([\s\S]*?)<\/untrusted>/.exec(c.input)![1])) as { change: Record<string, unknown> };
      // The rewrite restates every free-text field (title, rationale, change description) as the one under test.
      return { ...p, title: rewriteTitle, rationale: `Rewritten: ${rewriteTitle}`, change: { ...p.change, description: `Rewritten: ${rewriteTitle}` } };
    }
    if (c.role === GENERALIZE_ROLES.judge) return { leaks: false, category: "none" }; // a careless judge: the gates must still hold
    throw new Error(`unexpected call ${c.role}`);
  });
  const fx = await evolveFixture({ files: { "skills/review/SKILL.md": "x\n" }, extraYaml: PRIVACY, extraRepos: [{ name: "globex-app", private: true }], io });
  fx.deps.env.SINDRI_WORDS = words;
  seedIndex(openIndex(indexPath(fx.deps, "globex-app")), { files: ["src/zentrova/rota.ts"], symbols: ["quarbleScheduler", "zentrovaRota"] }).close();
  fs.writeFileSync(
    path.join(fx.transcripts, "5e55a1d0-0000-4000-8000-000000000001.jsonl"),
    // The evidence cites line 1; line 2 is a later turn of the same session that no ref points at.
    [STORY, LATER].map((content, i) => `${JSON.stringify({ type: "user", timestamp: `2026-10-08T10:0${i}:00Z`, cwd: fx.repo, message: { content } })}\n`).join(""),
  );
  await init([], fx.ctx);
  git(fx.repo, "checkout", "-q", "-b", "docs/proposals");
  fx.ctx.write((epoch) => saveProposal(fx.ctx.db, ProposalSchema.parse({
    artifact: "skill:review", kind: "skill-edit", title: "Globex quarbleScheduler GLX-1234 retries", rationale: STORY,
    evidence: ["transcript:5e55a1d0#1"], change: { type: "describe", files: ["skills/review/SKILL.md"], description: STORY },
  }), "correct", "code", epoch, new Date("2026-10-08T12:00:00Z")));
  const mode = json ? ["--json"] : [];
  // Every command's output is checked too (threat-model point 3), in the mode under test.
  const stdout = [(await stage(mode, fx.ctx)).stdout];
  const dry = (await publish(["--dry-run", "--json"], fx.ctx)).stdout; // writes nothing; gives the outcome in both modes
  const r = JSON.parse(dry) as { published: unknown[]; held: { layer: string }[] };
  stdout.push(dry, (await publish(mode, fx.ctx)).stdout);
  const file = path.join(fx.repo, REL);
  const exists = fs.existsSync(file);
  const text = exists ? fs.readFileSync(file, "utf8") : "";
  fx.close();
  return { r, text, exists, stdout };
}

describe("privacy end to end: a synthetic workplace never reaches the plan file", () => {
  const leaks = (text: string): string[] => SECRETS.filter((s) => text.toLowerCase().includes(s.toLowerCase()));

  const HELD = [
    ["a term the rewrite kept", "Retry Globex scheduler checks", "terms"],
    ["a ticket key the rewrite kept", "Retry scheduler checks after GLX-1234", "patterns"],
    ["a private identifier the rewrite kept", "Retry quarbleScheduler checks", "identifiers"],
    ["a copied transcript run", "overnight rota swap left two shifts without a charge nurse", "verbatim"],
    ["a run copied from an uncited turn of the cited session", "night handover sheet listed the wrong ward for three patients", "verbatim"],
  ] as const;

  describe.each([["human", false], ["--json", true]] as const)("with %s output", (_mode, json) => {
    it.each(HELD)("holds %s, judge notwithstanding", async (_name, title, layer) => {
      const { r, exists, stdout } = await world(title, json);
      expect(r.published).toEqual([]);
      expect(r.held.map((h) => h.layer)).toEqual([layer]);
      expect(exists).toBe(false); // nothing was written, so there is no plan file to leak into
      expect(stdout).toHaveLength(3);
      for (const out of stdout) expect(leaks(out)).toEqual([]);
    });

    it("publishes a clean rewrite and nothing else", async () => {
      const { r, text, exists, stdout } = await world("Retry flaky scheduler checks once", json);
      expect(r.published).toHaveLength(1);
      expect(exists).toBe(true);
      expect(text).toContain("Retry flaky scheduler checks once");
      expect(leaks(text)).toEqual([]);
      for (const out of stdout) expect(leaks(out)).toEqual([]);
    });
  });
});
