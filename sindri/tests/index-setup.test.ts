import { describe, expect, it } from "vitest";

import { makeIndexCommand } from "../src/index/commands.js";
import type { IndexProbes } from "../src/index/io.js";
import { GRAPHIFY_PIN, GRAPHIFY_PIN_DATE } from "../src/index/pins.js";
import { graphifyVersion, hasModel, runSetup } from "../src/index/setup.js";
import type { LoadedProfile } from "../src/profile/load.js";
import { ProfileSchema } from "../src/profile/schema.js";
import { approvedIndexDeps, fakeIndexIo, ring0Repo } from "./index-fixtures.js";
import { makeDeps } from "./helpers.js";

const loaded = (index: object = {}): LoadedProfile => ({
  root: "/p", files: [], bytes: {}, hash: "h", raw: { profile: {}, repos: {} }, repos: {},
  profile: ProfileSchema.parse({ schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"], index }),
});

interface Opts { bins?: string[]; tags?: unknown; graphify?: string; freeCurl?: number; boxedCurl?: number; pullCode?: number }
const ALL = ["ollama", "uv", "sandbox-exec", "curl", "graphify"];

function probes(o: Opts = {}): IndexProbes & { ran: string[][] } {
  const ran: string[][] = [];
  return {
    ran,
    has: (b) => (o.bins ?? ALL).includes(b),
    getJson: async () => (o.tags === undefined ? { models: [{ name: "nomic-embed-text:latest" }] } : o.tags),
    run: async (argv) => {
      ran.push(argv);
      if (argv.includes("--version")) return { code: o.graphify === "missing" ? 1 : 0, stdout: `graphify ${o.graphify ?? GRAPHIFY_PIN}\n`, stderr: "" };
      if (argv.includes("curl")) return { code: argv[0] === "curl" ? (o.freeCurl ?? 0) : (o.boxedCurl ?? 6), stdout: "", stderr: "" };
      if (argv[0] === "ollama") return { code: o.pullCode ?? 0, stdout: "", stderr: "" };
      return { code: 0, stdout: "", stderr: "" };
    },
  };
}

const run = (p: IndexProbes, o: { dryRun?: boolean; platform?: NodeJS.Platform; log?: (l: string) => void } = {}, index: object = {}) =>
  runSetup(loaded(index), p, { dryRun: o.dryRun ?? false, platform: o.platform ?? "darwin", home: "/home/u", log: o.log ?? (() => undefined) });
const statuses = async (p: IndexProbes, o = {}) => Object.fromEntries((await run(p, o)).steps.map((s) => [s.name, s]));

describe("sindri index setup", () => {
  it("reports everything ok when installed, pinned and sandboxed", async () => {
    const r = await run(probes());
    expect(r.steps.map((s) => [s.name, s.status])).toEqual([["ollama", "ok"], ["ollama-server", "ok"], ["embedding-model", "ok"], ["graphify", "ok"], ["sandbox", "ok"]]);
  });

  it("pulls a missing model and installs the pinned graphify with an age gate, or says what it would do", async () => {
    const dry = probes({ tags: { models: [] }, graphify: "missing" });
    const d = await statuses(dry, { dryRun: true });
    expect(d["embedding-model"]).toMatchObject({ status: "would", detail: "ollama pull nomic-embed-text" });
    expect(d.graphify).toMatchObject({ status: "would", detail: `uv tool install graphifyy==${GRAPHIFY_PIN} --exclude-newer ${GRAPHIFY_PIN_DATE}` });
    expect(dry.ran.some((a) => a[0] === "ollama" || a[0] === "uv")).toBe(false);
    const logs: string[] = [];
    const real = probes({ tags: { models: [] }, graphify: "missing" });
    const r = await statuses(real, { log: (l: string) => logs.push(l) });
    expect(r["embedding-model"].status).toBe("done");
    expect(r.graphify.status).toBe("done");
    expect(real.ran).toContainEqual(["uv", "tool", "install", `graphifyy==${GRAPHIFY_PIN}`, "--exclude-newer", GRAPHIFY_PIN_DATE]);
    expect(logs).toEqual([expect.stringContaining("pulling nomic-embed-text"), expect.stringContaining(`installing graphifyy==${GRAPHIFY_PIN}`)]);
  });

  it("reinstalls graphify when the installed version isn't exactly the pin", async () => {
    const r = await statuses(probes({ graphify: `${GRAPHIFY_PIN}0` }), { dryRun: true });
    expect(r.graphify.status).toBe("would");
    expect((await statuses(probes({ graphify: "dev" }), { dryRun: true })).graphify.status).toBe("would");
    expect((await statuses(probes({ bins: ALL.filter((b) => b !== "graphify") }), { dryRun: true })).graphify.status).toBe("would");
  });

  it("evaluates every step even when Ollama is missing (never returns early)", async () => {
    const r = await run(probes({ bins: ALL.filter((b) => b !== "ollama") }));
    expect(r.steps.map((s) => [s.name, s.status])).toEqual([["ollama", "fail"], ["ollama-server", "skip"], ["embedding-model", "skip"], ["graphify", "ok"], ["sandbox", "ok"]]);
    expect(r.steps[0]).toMatchObject({ fix: "install Ollama (https://ollama.com/download), then rerun" });
    expect(r.steps[1].detail).toBe("needs ollama");
  });

  it("reports a server that isn't answering, a failed pull, and a missing uv, each with a fix", async () => {
    const down = await statuses({ ...probes(), getJson: async () => null });
    expect(down["ollama-server"]).toMatchObject({ status: "fail", fix: "start Ollama (the app, or `ollama serve`), then rerun" });
    expect(down["embedding-model"]).toMatchObject({ status: "skip", detail: "needs the Ollama server" });
    const pull = await statuses(probes({ tags: { models: [] }, pullCode: 1 }));
    expect(pull["embedding-model"]).toMatchObject({ status: "fail", detail: "ollama pull nomic-embed-text exited 1" });
    const noUv = await statuses(probes({ bins: ALL.filter((b) => b !== "uv"), graphify: "missing" }));
    expect(noUv.graphify).toMatchObject({ status: "fail", detail: "uv is not installed", fix: "install uv (https://docs.astral.sh/uv/), then rerun" });
    expect(noUv.sandbox.status).toBe("ok");
  });

  it("proves the sandbox with a positive control: warns when it can't, fails when the network gets through", async () => {
    expect((await statuses(probes({ freeCurl: 6 }))).sandbox).toMatchObject({ status: "warn", detail: "can't verify: offline or curl missing; rerun online" });
    expect((await statuses(probes({ bins: ALL.filter((b) => b !== "curl") }))).sandbox.status).toBe("warn");
    expect((await statuses(probes({ boxedCurl: 0 }))).sandbox).toMatchObject({ status: "fail", detail: "a network request succeeded inside the sandbox" });
    expect((await statuses(probes())).sandbox).toMatchObject({ status: "ok", detail: "network denied inside the sandbox" });
  });

  it("without a network sandbox graphify is skipped and the sandbox step fails", async () => {
    const r = await statuses(probes({ bins: ["ollama", "uv", "curl", "graphify"] }), { platform: "linux" });
    expect(r.graphify).toMatchObject({ status: "skip", detail: "needs a network sandbox" });
    expect(r.sandbox).toMatchObject({ status: "fail", fix: "Linux: install bubblewrap; or set index.graph: none" });
  });

  it("runs every graphify --version inside the sandbox", async () => {
    const p = probes();
    await run(p);
    const version = p.ran.filter((a) => a.includes("--version"));
    expect(version).toHaveLength(1);
    expect(version[0][0]).toBe("sandbox-exec");
  });

  it("skips layers the profile turns off", async () => {
    expect((await run(probes(), {}, { embeddings: { enabled: false }, graph: "none" })).steps).toEqual([]);
  });

  it("hasModel and graphifyVersion", () => {
    expect(hasModel({ models: [{ name: "nomic-embed-text:latest" }] }, "nomic-embed-text")).toBe(true);
    expect(hasModel({ models: [{ name: "nomic-embed-text" }] }, "nomic-embed-text")).toBe(true);
    expect(hasModel({ models: [{ name: 5 }] }, "nomic-embed-text")).toBe(false);
    expect(hasModel(null, "m")).toBe(false);
    expect(hasModel({}, "m")).toBe(false);
    expect(graphifyVersion("graphify 1.2.30\n")).toBe("1.2.30");
    expect(graphifyVersion("graphify dev")).toBeNull();
  });
});

describe("sindri index setup (the command)", () => {
  it("reads the approved profile, prints one line per step with fixes, and exits 2 on a failure", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "export const a = 1;\n" }), { index: "index:\n  embeddings:\n    enabled: true\n" });
    const r = await makeIndexCommand(fakeIndexIo())(["setup"], d);
    expect(r.exitCode).toBe(2);
    expect(r.stdout).toMatch(/^fail\s+ollama\s+ollama is not installed$/m);
    expect(r.stdout).toContain("     fix: install Ollama (https://ollama.com/download), then rerun");
    expect(r.stdout).toMatch(/^skip\s+ollama-server\s+needs ollama$/m);
    expect((await makeIndexCommand(fakeIndexIo())(["setup"], makeDeps())).stderr).toContain("SND-PROFILE-012");
  });

  it("exits 0 when everything is ok, 1 on a warning, and says so when there is nothing to set up", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "export const a = 1;\n" }), { index: "index:\n  embeddings:\n    enabled: true\n" });
    const ok = await makeIndexCommand(fakeIndexIo({ probes: probes() }))(["setup", "--json"], d);
    expect(ok.exitCode).toBe(0);
    expect(JSON.parse(ok.stdout).map((s: { name: string }) => s.name)).toEqual(["ollama", "ollama-server", "embedding-model", "graphify", "sandbox"]);
    expect((await makeIndexCommand(fakeIndexIo({ probes: probes({ freeCurl: 6 }) }))(["setup", "--dry-run"], d)).exitCode).toBe(1);
    const off = await approvedIndexDeps(ring0Repo({ "a.ts": "export const a = 1;\n" }));
    expect((await makeIndexCommand(fakeIndexIo())(["setup"], off)).stdout).toContain("Nothing to set up");
  });
});
