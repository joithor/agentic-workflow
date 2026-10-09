import fs from "node:fs";

import type { LoadedProfile } from "../profile/load.js";
import { sandboxArgv } from "./graph.js";
import type { IndexProbes } from "./io.js";
import { GRAPHIFY_PIN, GRAPHIFY_PIN_DATE } from "./pins.js";

export type StepStatus = "ok" | "done" | "would" | "skip" | "warn" | "fail";
export interface Step {
  name: string;
  status: StepStatus;
  detail: string;
  fix?: string;
}

export function hasModel(tags: unknown, model: string): boolean {
  const models = (tags as { models?: { name?: unknown }[] } | null)?.models ?? [];
  return models.some((m) => typeof m.name === "string" && (m.name === model || m.name.startsWith(`${model}:`)));
}

// The first x.y.z in `graphify --version`, compared for equality with the pin (1.2.30 is not 1.2.3).
export function graphifyVersion(stdout: string): string | null {
  return /\b(\d+\.\d+\.\d+)\b/.exec(stdout)?.[1] ?? null;
}

// Ollama's model list; `setup` and `doctor` both ask it.
export const tagsUrl = (url: string): string => `${url.replace(/\/+$/, "")}/api/tags`;

// `graphify --version` inside the sandbox (no network, clean env): the installed version, or null.
// `null` sandbox: none on this machine. Shared by `setup` and `doctor`.
export function sandboxedVersionArgv(platform: NodeJS.Platform, probes: IndexProbes, home: string): string[] | null {
  return sandboxArgv(platform, ["graphify", "--version"], probes.has, { writable: [], home, exists: fs.existsSync });
}

export async function installedGraphify(probes: IndexProbes, boxed: string[]): Promise<string | null> {
  if (!probes.has("graphify")) return null;
  const v = await probes.run(boxed, { cwd: "/", timeoutMs: 30_000, cleanEnv: true });
  return v.code === 0 ? graphifyVersion(v.stdout) : null;
}

const CURL = ["curl", "-sS", "--max-time", "3", "https://example.com"];

// Spec §11.3 index dependencies: Ollama + the embedding model, pinned and age-gated graphify, and
// a network sandbox that really denies the network. Every step is evaluated and reported, so one
// rerun shows everything that is wrong; nothing returns early.
export async function runSetup(
  loaded: LoadedProfile,
  probes: IndexProbes,
  o: { dryRun: boolean; platform: NodeJS.Platform; home: string; log: (line: string) => void },
): Promise<{ steps: Step[] }> {
  const steps: Step[] = [];
  const ix = loaded.profile.index;
  const act = async (name: string, argv: string[], say: string): Promise<Step> => {
    if (o.dryRun) return { name, status: "would", detail: argv.join(" ") };
    o.log(say);
    const r = await probes.run(argv, { cwd: "/", timeoutMs: 1_800_000 });
    return r.code === 0
      ? { name, status: "done", detail: argv.join(" ") }
      : { name, status: "fail", detail: `${argv.join(" ")} exited ${r.code}`, fix: `run \`${argv.join(" ")}\` by hand and read its output` };
  };
  if (ix.embeddings.enabled) {
    if (!probes.has("ollama")) {
      steps.push({ name: "ollama", status: "fail", detail: "ollama is not installed", fix: "install Ollama (https://ollama.com/download), then rerun" });
      steps.push({ name: "ollama-server", status: "skip", detail: "needs ollama" }, { name: "embedding-model", status: "skip", detail: "needs ollama" });
    } else {
      steps.push({ name: "ollama", status: "ok", detail: "installed" });
      const tags = await probes.getJson(tagsUrl(ix.embeddings.url), 3000);
      if (tags === null) {
        steps.push({ name: "ollama-server", status: "fail", detail: `no answer from ${ix.embeddings.url}`, fix: "start Ollama (the app, or `ollama serve`), then rerun" });
        steps.push({ name: "embedding-model", status: "skip", detail: "needs the Ollama server" });
      } else {
        steps.push({ name: "ollama-server", status: "ok", detail: ix.embeddings.url });
        steps.push(
          hasModel(tags, ix.embeddings.model)
            ? { name: "embedding-model", status: "ok", detail: ix.embeddings.model }
            : await act("embedding-model", ["ollama", "pull", ix.embeddings.model], `pulling ${ix.embeddings.model} (a few hundred MB) ...`),
        );
      }
    }
  }
  if (ix.graph === "graphify") {
    const version = sandboxedVersionArgv(o.platform, probes, o.home);
    const curl = sandboxArgv(o.platform, CURL, probes.has, { writable: [], home: o.home, exists: fs.existsSync });
    if (version === null || curl === null) {
      steps.push({ name: "graphify", status: "skip", detail: "needs a network sandbox" });
      steps.push({ name: "sandbox", status: "fail", detail: "no network sandbox (sandbox-exec or bwrap)", fix: "Linux: install bubblewrap; or set index.graph: none" });
    } else {
      const installed = await installedGraphify(probes, version);
      if (installed === GRAPHIFY_PIN) {
        steps.push({ name: "graphify", status: "ok", detail: `graphify ${installed}` });
      } else if (probes.has("uv")) {
        const pin = `graphifyy==${GRAPHIFY_PIN}`;
        steps.push(await act("graphify", ["uv", "tool", "install", pin, "--exclude-newer", GRAPHIFY_PIN_DATE], `installing ${pin} (uv, dependencies age-gated to ${GRAPHIFY_PIN_DATE}) ...`));
      } else {
        steps.push({ name: "graphify", status: "fail", detail: "uv is not installed", fix: "install uv (https://docs.astral.sh/uv/), then rerun" });
      }
      // Positive control: only an unsandboxed request that works makes a sandboxed failure mean anything.
      const free = probes.has("curl") ? await probes.run(CURL, { cwd: "/", timeoutMs: 10_000 }) : null;
      if (free === null || free.code !== 0) {
        steps.push({ name: "sandbox", status: "warn", detail: "can't verify: offline or curl missing; rerun online" });
      } else {
        const boxed = await probes.run(curl, { cwd: "/", timeoutMs: 15_000 });
        steps.push(
          boxed.code !== 0
            ? { name: "sandbox", status: "ok", detail: "network denied inside the sandbox" }
            : { name: "sandbox", status: "fail", detail: "a network request succeeded inside the sandbox", fix: "report this; graphify stays off until the sandbox denies the network" },
        );
      }
    }
  }
  return { steps };
}
