import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { z } from "zod";
import { zodToJsonSchema } from "zod-to-json-schema";

import { makeClaudeRunner } from "../../src/scope/model.js";
import { realSpawner } from "../../src/scope/model-real.js";
import { makeScrubber } from "../../src/scrub/scrub.js";

const Three = z.object({ color: z.string(), count: z.number().int(), ok: z.boolean() });

// One real model call. Run it with `npm run test:heavy`; it proves the CLI accepts
// the flags, the Zod-derived schema and the stdin prompt, and that structured_output parses.
describe.skipIf(process.env.SINDRI_HEAVY !== "1")("real claude -p (heavy: one real model call)", () => {
  it("returns structured output that parses for a three-field schema", async () => {
    const runner = makeClaudeRunner({
      spawn: realSpawner(), providers: ["anthropic"], scrubber: makeScrubber(),
      makeDir: () => fs.mkdtempSync(path.join(os.tmpdir(), "sindri-heavy-")), removeDir: (d) => fs.rmSync(d, { recursive: true, force: true }),
      env: process.env, effort: "low", allowBaseUrl: false,
    });
    const r = await runner.run({
      role: "draft", model: "sonnet", system: "Answer with the requested fields only.", input: "Give a colour, the number 3, and true.",
      schema: zodToJsonSchema(Three, { $refStrategy: "none" }) as Record<string, unknown>, parse: (v) => Three.parse(v), timeoutMs: 120_000,
    });
    expect(r.value.count).toBe(3);
    expect(r.usage.outputTokens).toBeGreaterThan(0);
  }, 180_000);
});
