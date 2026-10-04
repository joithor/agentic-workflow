// One POST /v1/systemone carries any number of typed questions (docs.typesafe.ai/api,
// confirmed 2026-10-04: `questions` is a map, no documented count limit; choice ≤ 255
// options, score ≤ 10 levels). jev.ts adapts this to the single-question Provider
// contract; batched callers (the prompt sorter) use callJev directly.
export interface Fetch {
  (url: string, init: { method: string; headers: Record<string, string>; body: string; signal: AbortSignal }): Promise<{ status: number; json(): Promise<unknown> }>;
}

export const JEV_MODEL = "jev-1.13.0";
const JEV_ENDPOINT = "https://api.typesafe.ai/v1/systemone";
const MAX_CHOICE_OPTIONS = 255;
const MAX_SCORE_LEVELS = 10;

export type JevQuestion =
  | { type: "choice"; instructions: string; criteria: Record<string, string> }
  | { type: "noul"; instructions: string }
  | { type: "score"; instructions: string; criteria: string[] };

export type JevAnswer =
  | { type: "choice"; choice: string; probabilities: Record<string, number>; confidence: number }
  | { type: "noul"; noul: number }
  | { type: "score"; score: number; probabilities: Record<string, number>; confidence: number };

export type JevQuestionResult = { ok: true; answer: JevAnswer } | { ok: false; reason_code: "unparseable-result" };

export type JevCallResult =
  | { status: "ok"; answers: Record<string, JevQuestionResult>; usage: { input_tokens: number; output_tokens: number } }
  | { status: "unavailable" | "error"; reason_code: string };

export interface JevDeps {
  fetch: Fetch;
  apiKey: () => Promise<string | null>;
  model?: string;
}

const isNum = (v: unknown): v is number => typeof v === "number" && Number.isFinite(v);
const isProbs = (v: unknown): v is Record<string, number> =>
  typeof v === "object" && v !== null && Object.values(v).every(isNum);

function parseAnswer(q: JevQuestion, raw: unknown): JevQuestionResult {
  const bad = { ok: false, reason_code: "unparseable-result" } as const;
  if (typeof raw !== "object" || raw === null) return bad;
  const a = raw as Record<string, unknown>;
  if (a.type !== q.type) return bad;
  if (q.type === "noul") return isNum(a.noul) ? { ok: true, answer: { type: "noul", noul: a.noul } } : bad;
  if (q.type === "choice") {
    if (typeof a.choice !== "string" || !(a.choice in q.criteria)) return bad;
    return { ok: true, answer: { type: "choice", choice: a.choice, probabilities: isProbs(a.probabilities) ? a.probabilities : {}, confidence: isNum(a.confidence) ? a.confidence : 1 } };
  }
  if (!isNum(a.score)) return bad;
  return { ok: true, answer: { type: "score", score: a.score, probabilities: isProbs(a.probabilities) ? a.probabilities : {}, confidence: isNum(a.confidence) ? a.confidence : 1 } };
}

export async function callJev(deps: JevDeps, state: unknown, questions: Record<string, JevQuestion>, budgetMs: number): Promise<JevCallResult> {
  for (const q of Object.values(questions)) {
    if (q.type === "choice" && Object.keys(q.criteria).length > MAX_CHOICE_OPTIONS) return { status: "error", reason_code: "too-many-options" };
    if (q.type === "score" && q.criteria.length > MAX_SCORE_LEVELS) return { status: "error", reason_code: "too-many-levels" };
  }
  const apiKey = await deps.apiKey();
  if (apiKey === null) return { status: "unavailable", reason_code: "no-api-key" };

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), budgetMs);
  try {
    const response = await deps.fetch(JEV_ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiKey}` },
      body: JSON.stringify({ state, model: deps.model ?? JEV_MODEL, questions }),
      signal: controller.signal,
    });
    clearTimeout(timer);
    if (response.status < 200 || response.status >= 300) return { status: "error", reason_code: `http-${response.status}` };
    const body = (await response.json()) as { answers?: Record<string, unknown>; usage?: { input_tokens?: unknown; output_tokens?: unknown } };
    const answers: Record<string, JevQuestionResult> = {};
    for (const [id, q] of Object.entries(questions)) answers[id] = parseAnswer(q, body.answers?.[id]);
    const usage = {
      input_tokens: isNum(body.usage?.input_tokens) ? body.usage.input_tokens : 0,
      output_tokens: isNum(body.usage?.output_tokens) ? body.usage.output_tokens : 0,
    };
    return { status: "ok", answers, usage };
  } catch (e) {
    clearTimeout(timer);
    if (e instanceof DOMException && e.name === "AbortError") return { status: "unavailable", reason_code: "timeout" };
    return { status: "unavailable", reason_code: "network-error" };
  }
}
