import fs from "node:fs";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";

import type { Deps } from "../src/deps.js";
import { makeIndexCommand } from "../src/index/commands.js";
import { makeShapeCommand } from "../src/index/shape.js";
import type { computeSignals as ComputeSignals } from "../src/index/signals.js";
import { spoolDir } from "../src/index/spool.js";
import { approvedIndexDeps, BODY, fakeIndexIo, OFF, ring0Name, ring0Repo } from "./index-fixtures.js";
import { git } from "./helpers.js";

// computeSignals is the step that can throw or run long (it has no deadline of its own), so these
// tests swap it out; everything else (ledger, profile, index, git) is real.
const swap = vi.hoisted(() => ({ impl: null as null | ((...a: Parameters<typeof ComputeSignals>) => ReturnType<typeof ComputeSignals>) }));
vi.mock("../src/index/signals.js", async (original) => {
  const real = await original<typeof import("../src/index/signals.js")>();
  return { ...real, computeSignals: (...a: Parameters<typeof ComputeSignals>) => (swap.impl ?? real.computeSignals)(...a) };
});

async function staged(budgetMs: number): Promise<Deps> {
  const root = ring0Repo({ "src/util/text.ts": BODY("clip") });
  const d = await approvedIndexDeps(root, { index: `${OFF}shape:\n  budgetMs: ${budgetMs}\n` });
  await makeIndexCommand(fakeIndexIo())(["build", "--repo", ring0Name(d)], d);
  fs.mkdirSync(path.join(root, "src"), { recursive: true });
  fs.writeFileSync(path.join(root, "src/feature.ts"), BODY("shorten"));
  git(root, "add", "src/feature.ts");
  return d;
}

const record = (d: Deps) => makeShapeCommand(fakeIndexIo())(["--record", "--staged"], d);
const spoolFiles = (d: Deps): string[] => (fs.existsSync(spoolDir(d)) ? fs.readdirSync(spoolDir(d)) : []);

describe("sindri shape --record never blocks a commit", () => {
  it("a throwing computeSignals is exit 0 with a note, and records nothing", async () => {
    const d = await staged(2000);
    swap.impl = async () => {
      throw new Error("boom");
    };
    try {
      expect(await record(d)).toEqual({ exitCode: 0, stdout: "", stderr: "sindri-shape: skipped (boom)\n" });
      swap.impl = async () => {
        throw "not an Error";
      };
      expect((await record(d)).stderr).toBe("sindri-shape: skipped (not an Error)\n");
    } finally {
      swap.impl = null;
    }
    expect(spoolFiles(d)).toEqual([]);
  });

  it("stops at shape.budgetMs, exits 0, and the late result is never written", async () => {
    const d = await staged(150);
    let finished = (): void => undefined;
    const late = new Promise<void>((r) => (finished = r));
    swap.impl = async () => {
      await new Promise((r) => setTimeout(r, 600));
      finished();
      return { signals: [], deferred: [] };
    };
    try {
      const t0 = Date.now();
      const r = await record(d);
      expect(Date.now() - t0).toBeLessThan(550);
      expect(r).toEqual({ exitCode: 0, stdout: "", stderr: "sindri-shape: skipped (over the 150 ms budget; nothing recorded)\n" });
      await late;
      await new Promise((r) => setTimeout(r, 50));
    } finally {
      swap.impl = null;
    }
    expect(spoolFiles(d)).toEqual([]);
  });

  it("a computeSignals that holds the thread past the budget (the timer can't fire) records nothing", async () => {
    const d = await staged(150);
    let t = Date.parse("2026-10-08T12:00:00.000Z");
    const moving = { ...d, now: () => new Date(t) };
    swap.impl = async () => {
      t += 1000;
      return { signals: [], deferred: [] };
    };
    try {
      expect((await record(moving)).stderr).toBe("sindri-shape: skipped (over the 150 ms budget; nothing recorded)\n");
    } finally {
      swap.impl = null;
    }
    expect(spoolFiles(d)).toEqual([]);
  });
});
