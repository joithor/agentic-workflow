import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { isAgentCliName } from "./detect.js";
import { SCAFFOLD_IDS, type ScaffoldId } from "./prompt-sort/scaffolds.js";
import type { AgentCliName, ProviderName } from "./types.js";

export interface JudgeQuestionConfig {
  enabled: boolean;
  threshold: number;
  // Restricts and orders the provider chain for this question, e.g. ["jev", "rules"].
  providers?: ProviderName[];
}

function isProviderName(v: unknown): v is ProviderName {
  return v === "rules" || v === "jev" || isAgentCliName(v);
}

function parseQuestions(raw: Record<string, unknown>): Record<string, JudgeQuestionConfig> {
  const out: Record<string, JudgeQuestionConfig> = {};
  for (const [name, entry] of Object.entries(raw)) {
    const providers = (entry as { providers?: unknown } | null)?.providers;
    const named = Array.isArray(providers) ? providers.filter(isProviderName) : [];
    if (named.length > 0) {
      out[name] = { ...(entry as JudgeQuestionConfig), providers: named };
    } else if (providers !== undefined) {
      // Not an array, or filtered to nothing: treat as unset (default chain).
      const { providers: _dropped, ...rest } = entry as JudgeQuestionConfig;
      out[name] = rest;
    } else {
      out[name] = entry as JudgeQuestionConfig;
    }
  }
  return out;
}

// Optional provider selection (config.json "providers"):
//   agentClis — which agent CLIs to use and in what priority order, e.g.
//               ["codex-cli", "claude-cli"]; overrides AW_PROVIDER and the
//               default order. Uninstalled ones are skipped. [] = none.
//   jev       — false drops Jev from every text class (default: included).
export interface JudgeProvidersConfig {
  agentClis?: AgentCliName[];
  jev?: boolean;
}

export interface JudgeConfig {
  questions: Record<string, JudgeQuestionConfig>;
  providers?: JudgeProvidersConfig;
  promptSort?: PromptSortOverrides;
}

export const DEFAULT_CONFIG: JudgeConfig = {
  questions: {
    "wake-gate": { enabled: true, threshold: 0.7 },
  },
};

export function configPath(home?: string): string {
  return path.join(home ?? os.homedir(), ".agentic-workflow", "judge", "config.json");
}

// The per-box state root (spec: "Per-box state lives under ~/.agentic-workflow/").
// AW_STATE_DIR overrides it wholesale — used so tests and smoke runs can point
// at a scratch directory while still using the real HOME (and its real
// agent-CLI logins) for everything else.
export function judgeStateDir(env: NodeJS.ProcessEnv = process.env): string {
  const override = env.AW_STATE_DIR;
  if (override !== undefined && override !== "") return override;
  return path.join(os.homedir(), ".agentic-workflow");
}

export function judgeConfigPath(env: NodeJS.ProcessEnv = process.env): string {
  return path.join(judgeStateDir(env), "judge", "config.json");
}

export function judgeDbPath(env: NodeJS.ProcessEnv = process.env): string {
  return path.join(judgeStateDir(env), "judge", "decisions.sqlite");
}

// Unknown provider names and wrong-typed fields are dropped, never fatal: a
// bad providers block degrades to the default chain.
function parseProviders(raw: unknown): JudgeProvidersConfig | undefined {
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return undefined;
  const { agentClis, jev } = raw as { agentClis?: unknown; jev?: unknown };
  const parsed: JudgeProvidersConfig = {};
  if (Array.isArray(agentClis)) parsed.agentClis = agentClis.filter(isAgentCliName);
  if (typeof jev === "boolean") parsed.jev = jev;
  return Object.keys(parsed).length > 0 ? parsed : undefined;
}

export interface PromptSortOverrides {
  enabled?: boolean;
  budgetMs?: number;
  cooldownPrompts?: number;
  scaffolds?: Partial<Record<ScaffoldId, boolean>>;
}
export interface PromptSortConfig {
  enabled: boolean;
  budgetMs: number;
  cooldownPrompts: number;
  scaffolds: Record<ScaffoldId, boolean>;
}

// Shadow mode: the sorter records every prompt; no scaffold is injected until a
// switch is turned on (Task 10 flips them from eval results).
export const DEFAULT_PROMPT_SORT: PromptSortConfig = {
  enabled: true,
  budgetMs: 1000,
  cooldownPrompts: 5,
  scaffolds: { brief: false, bugfix: false, "ui-evidence": false, "plan-first": false },
};

const MIN_BUDGET_MS = 200;
const MAX_BUDGET_MS = 1400;

function parsePromptSort(raw: unknown): PromptSortOverrides | undefined {
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return undefined;
  const { enabled, budgetMs, cooldownPrompts, scaffolds } = raw as Record<string, unknown>;
  const parsed: PromptSortOverrides = {};
  if (typeof enabled === "boolean") parsed.enabled = enabled;
  if (typeof budgetMs === "number" && Number.isFinite(budgetMs)) parsed.budgetMs = Math.min(MAX_BUDGET_MS, Math.max(MIN_BUDGET_MS, budgetMs));
  if (typeof cooldownPrompts === "number" && Number.isFinite(cooldownPrompts) && cooldownPrompts >= 0) parsed.cooldownPrompts = cooldownPrompts;
  if (typeof scaffolds === "object" && scaffolds !== null && !Array.isArray(scaffolds)) {
    const s: Partial<Record<ScaffoldId, boolean>> = {};
    for (const id of SCAFFOLD_IDS) {
      const v = (scaffolds as Record<string, unknown>)[id];
      if (typeof v === "boolean") s[id] = v;
    }
    parsed.scaffolds = s;
  }
  return parsed;
}

export function resolvePromptSort(config: JudgeConfig): PromptSortConfig {
  const o = config.promptSort ?? {};
  return {
    enabled: o.enabled ?? DEFAULT_PROMPT_SORT.enabled,
    budgetMs: o.budgetMs ?? DEFAULT_PROMPT_SORT.budgetMs,
    cooldownPrompts: o.cooldownPrompts ?? DEFAULT_PROMPT_SORT.cooldownPrompts,
    scaffolds: { ...DEFAULT_PROMPT_SORT.scaffolds, ...(o.scaffolds ?? {}) },
  };
}

export function loadConfig(file: string, defaults: JudgeConfig = DEFAULT_CONFIG): JudgeConfig {
  try {
    const raw: unknown = JSON.parse(fs.readFileSync(file, "utf8"));
    if (typeof raw !== "object" || raw === null) return defaults;
    const { questions: rawQuestions, providers: rawProviders, promptSort: rawPromptSort } = raw as { questions?: unknown; providers?: unknown; promptSort?: unknown };
    const questions = rawQuestions === undefined ? {} : rawQuestions;
    if (typeof questions !== "object" || questions === null || Array.isArray(questions)) return defaults;
    const merged: JudgeConfig = { questions: { ...defaults.questions, ...parseQuestions(questions as Record<string, unknown>) } };
    const providers = parseProviders(rawProviders);
    const promptSort = parsePromptSort(rawPromptSort);
    return { ...merged, ...(providers === undefined ? {} : { providers }), ...(promptSort === undefined ? {} : { promptSort }) };
  } catch {
    return defaults;
  }
}
