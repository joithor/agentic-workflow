import fs from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { LiveSnapshotSchema } from "../src/live-snapshot.js";

// The aw-live mod's tests run against a sample `scorer live --json` result. If the scorer's
// schema moves and the sample does not, the mod would be tested against a shape the scorer no
// longer produces. This parses the mod's own sample with the scorer's schema.
const SAMPLE = fileURLToPath(new URL("../../mods/aw-live/hooks/tests/fixtures/sample.ts", import.meta.url));

describe("aw-live contract", () => {
  it("the mod's sample snapshot is a valid LiveSnapshot", () => {
    const source = fs.readFileSync(SAMPLE, "utf8");
    const json = /SAMPLE_JSON = `([\s\S]*?)`/.exec(source)?.[1];
    expect(json).toBeDefined();
    expect(LiveSnapshotSchema.parse(JSON.parse(json as string)).sessionId).toBe("session-1");
  });
});
