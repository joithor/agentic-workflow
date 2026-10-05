// judge/tests/prompt-sort/fixtures.ts
import { vi } from "vitest";

export const noul = (p: number) => ({ type: "noul", noul: p });
export const score = (index: number, probs: Record<string, number>, confidence: number) => ({ type: "score", score: index, probabilities: probs, confidence });

/** A full Jev response for "Fix the login button crash on the settings page": bug + UI, small, vague, no verification. */
export function jevBody(over: Record<string, unknown> = {}): { answers: Record<string, unknown>; usage: { input_tokens: number; output_tokens: number } } {
  const base: Record<string, unknown> = {
    is_task: noul(0.95), wants_loop: noul(0.05), scope_defined: noul(0.1), limits_defined: noul(0.1),
    approach_defined: noul(0.1), verification_defined: noul(0.05), is_bug_report: noul(0.9), touches_ui: noul(0.92),
    needs_research: noul(0.05),
    complexity: score(1, { "0": 0.05, "1": 0.8, "2": 0.1, "3": 0.05 }, 0.8),
    ambiguity: score(2, { "0": 0.05, "1": 0.15, "2": 0.8 }, 0.8),
  };
  return { answers: { ...base, ...over }, usage: { input_tokens: 300, output_tokens: 20 } };
}

export const jevFetch = (body: unknown, status = 200) => vi.fn().mockResolvedValue({ status, json: async () => body });
export const deps = (fetch: ReturnType<typeof vi.fn>) => ({ fetch, apiKey: async () => "k" as string | null });
