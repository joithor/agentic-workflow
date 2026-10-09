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
import type { GitRunner } from "../src/git.js";

// computeSignals is the step that can throw or run long (it has no deadline of its own), so these
// tests swap it out; everything else (ledger, profile, index, git) is real.
const swap = vi.hoisted(() => ({ impl: null as null | ((...a: Parameters<typeof ComputeSignals>) => ReturnType<typeof ComputeSignals>) }));
vi.mock("../src/index/signals.js", async (original) => {
  const real = await original<typeof import("../src/index/signals.js")>();
  return { ...real, computeSignals: (...a: Parameters<typeof ComputeSignals>) => (swap.impl ?? real.computeSignals)(...a) };
});

const overlaySwap = vi.hoisted(() => ({ before: null as null | (() => void) }));
vi.mock("../src/index/overlay.js", async (original) => {
  const real = await original<typeof import("../src/index/overlay.js")>();
  return {
    ...real,
    buildOverlay: (...a: Parameters<typeof real.buildOverlay>) => {
      overlaySwap.before?.();
      return real.buildOverlay(...a);
    },
  };
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
    const d = await staged(60_000);
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
      await new Promise((r) => setTimeout(r, 1500));
      finished();
      return { signals: [], deferred: [] };
    };
    try {
      const t0 = Date.now();
      const r = await record(d);
      // Wide margins: the point is "well before the slow step ends", not timer precision.
      expect(Date.now() - t0).toBeLessThan(1200);
      expect(r).toEqual({ exitCode: 0, stdout: "", stderr: "sindri-shape: skipped (over the 150 ms budget; nothing recorded)\n" });
      // On a slow machine an earlier check stops the run before the slow step even starts.
      await Promise.race([late, new Promise((r) => setTimeout(r, 2000))]);
      await new Promise((r) => setTimeout(r, 50));
    } finally {
      swap.impl = null;
    }
    expect(spoolFiles(d)).toEqual([]);
  });

  it("a computeSignals that holds the thread past the budget (the timer can't fire) records nothing", async () => {
    const d = await staged(60_000);
    let t = Date.parse("2026-10-08T12:00:00.000Z");
    const moving = { ...d, now: () => new Date(t) };
    swap.impl = async () => {
      t += 100_000;
      return { signals: [], deferred: [] };
    };
    try {
      expect((await record(moving)).stderr).toBe("sindri-shape: skipped (over the 60000 ms budget; nothing recorded)\n");
    } finally {
      swap.impl = null;
    }
    expect(spoolFiles(d)).toEqual([]);
  });

  // A long real budget, so only the fake clock decides which check stops the run.
  it("checks the budget after each step: the staged diff, the parse, and HEAD/write-tree (Task 9 I1)", async () => {
    const OVER = "sindri-shape: skipped (over the 60000 ms budget; nothing recorded)\n";
    const at = (d: Deps) => {
      let t = Date.parse("2026-10-08T12:00:00.000Z");
      // A git whose named call takes 100 s of (fake) clock time.
      const slowGit = (needle: string): GitRunner => ({
        run: async (args, cwd, o) => {
          if (args.join(" ").includes(needle)) t += 100_000;
          return d.git.run(args, cwd, o);
        },
      });
      return { tick: () => (t += 100_000), deps: (git: GitRunner = d.git): Deps => ({ ...d, git, now: () => new Date(t) }), slowGit };
    };
    const d = await staged(60_000);

    const a = at(d);
    expect((await record(a.deps(a.slowGit("--numstat")))).stderr).toBe(OVER);

    const b = at(d);
    overlaySwap.before = b.tick;
    try {
      expect((await record(b.deps())).stderr).toBe(OVER);
    } finally {
      overlaySwap.before = null;
    }

    const c = at(d);
    expect((await record(c.deps(c.slowGit("write-tree")))).stderr).toBe(OVER);
    expect(spoolFiles(d)).toEqual([]);
  });
});
