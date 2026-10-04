import crypto from "node:crypto";

import { recordDecision, recordDecisionDetails, recordFailure, type Agreement, type Db, type DecisionOutcome, type SkippedProvider } from "./db.js";
import type { JudgeConfig } from "./config.js";
import { providersFor, DEFAULT_CHAIN, type ChainSpec } from "./chain.js";
import { toRef, type QuestionModule } from "./question.js";
import { capText, INPUT_CAP, redactDeep } from "./redact.js";
import type { Decision, Provider } from "./types.js";

export interface EvaluateDeps {
  db: Db;
  config: JudgeConfig;
  providers: readonly Provider[];
  chain?: ChainSpec;
  now?: () => Date;
  randomId?: () => string;
  sessionId?: string;
}

export type EvaluateOutcome<O extends string> = Decision<O> | { escalate: true; reason_code: string };

function recordFailureSafe(db: Db, question: string, provider: Provider["name"], reasonCode: string): void {
  try {
    recordFailure(db, { ts: new Date().toISOString(), question, provider, reason_code: reasonCode });
  } catch {
    /* storage itself is down; nothing more to do, caller already fails open */
  }
}

// A real provider failure or timeout was attempted and didn't pan out — this
// is not a benign "nothing to decide" escalation (a disabled question, an
// input that failed validation, or every candidate merely unavailable/below
// threshold), it's the case `judge health` must surface loudly.
function hadRealFailure(skipped: readonly SkippedProvider[]): boolean {
  return skipped.some((s) => s.reason === "failed" || s.reason === "timeout");
}

export async function evaluate<I, O extends string>(
  question: QuestionModule<I, O>,
  rawInput: unknown,
  deps: EvaluateDeps,
): Promise<EvaluateOutcome<O>> {
  const id = deps.randomId?.() ?? crypto.randomUUID();
  const ts = (deps.now?.() ?? new Date()).toISOString();

  // Best-effort: details never change the outcome (fails open). Redact the
  // parsed values first (not the serialization: an escaped newline hides the
  // word boundary), then serialize, then cap, so a secret cut at the cap boundary
  // cannot survive. input_digest hashes the unredacted input separately.
  const recordDetails = (
    digestInput: unknown, probabilities: Record<string, number> | null = null,
    rulesOpinion: string | null = null, agreement: Agreement | null = null,
  ): void => {
    try {
      recordDecisionDetails(deps.db, {
        id, input_json: capText(JSON.stringify(redactDeep(digestInput)), INPUT_CAP),
        probabilities, rules_opinion: rulesOpinion, agreement, session_id: deps.sessionId ?? null,
      });
    } catch {
      /* details are best-effort */
    }
  };

  const recordRow = (
    outcome: DecisionOutcome, reasonCode: string, provider: Provider["name"] | "none", decision: O | null,
    confidence: number, latencyMs: number, chainPosition: number, skipped: SkippedProvider[], digestInput: unknown,
    probabilities: Record<string, number> | null = null, agreement: Agreement | null = null,
  ): void => {
    try {
      recordDecision(deps.db, {
        id, ts, question: question.name, content_class: question.contentClass, provider,
        decision, confidence, reason_code: reasonCode, latency_ms: latencyMs,
        input_digest: crypto.createHash("sha256").update(JSON.stringify(digestInput)).digest("hex").slice(0, 16),
        undone_at: null, chain_position: chainPosition, skipped, outcome,
      });
      recordDetails(digestInput, probabilities, null, agreement);
    } catch {
      // Storage fails open: the outcome still stands, and the miss itself is
      // a judge failure a healthy system should surface (spec: Visibility).
      try {
        recordFailure(deps.db, { ts, question: question.name, provider, reason_code: "decision-write-failed" });
      } catch {
        /* nothing left to fall back to; the outcome is still returned below */
      }
    }
  };

  const escalate = (
    reasonCode: string, outcome: "escalated" | "failed", skipped: SkippedProvider[] = [],
    probabilities: Record<string, number> | null = null,
  ): { escalate: true; reason_code: string } => {
    recordRow(outcome, reasonCode, "none", null, 0, 0, skipped.length, skipped, rawInput, probabilities);
    return { escalate: true, reason_code: reasonCode };
  };

  const qConfig = deps.config.questions[question.name];
  if (qConfig !== undefined && !qConfig.enabled) return escalate("question-disabled", "escalated");
  const threshold = qConfig?.threshold ?? question.threshold;

  const parsed = question.inputSchema.safeParse(rawInput);
  if (!parsed.success) return escalate("invalid-input", "escalated");
  const input = parsed.data;

  const settle = (
    decision: O, confidence: number, model: Provider["name"], reasonCode: string,
    latencyMs: number, chainPosition: number, skipped: SkippedProvider[], extra?: Record<string, unknown>,
    probabilities?: Record<string, number>, agreement?: Agreement,
  ): Decision<O> => {
    recordRow("decided", reasonCode, model, decision, confidence, latencyMs, chainPosition, skipped, input, probabilities ?? null, agreement ?? null);
    return extra === undefined ? { decision, confidence, model, reason_code: reasonCode, id } : { decision, confidence, model, reason_code: reasonCode, id, extra };
  };

  const preRuleResult = question.preRules?.(input) ?? null;
  if (preRuleResult !== null) return settle(preRuleResult, 1, "rules", "pre-rule", 0, 0, []);

  const chain = deps.chain ?? DEFAULT_CHAIN;
  let candidates = providersFor(chain, question.contentClass, deps.providers);
  // A question's own provider list restricts and orders the chain; names not
  // in the chain are dropped.
  if (qConfig?.providers !== undefined) {
    const available = candidates;
    candidates = qConfig.providers.flatMap((n) => available.filter((p) => p.name === n));
  }
  const ref = toRef(question, input);

  const skipped: SkippedProvider[] = [];
  let lastReason: "no-provider-decided" | "below-threshold" = "no-provider-decided";
  // The most recent below-threshold answer's probabilities: kept visible in
  // decision_details whichever way the question is finally settled.
  let undecidedProbs: Record<string, number> | undefined;

  for (let i = 0; i < candidates.length; i++) {
    const provider = candidates[i];
    const start = Date.now();
    let result;
    try {
      result = await provider.decide<O>(ref, input, question.timeBudgetMs);
    } catch {
      recordFailureSafe(deps.db, question.name, provider.name, "provider-threw");
      skipped.push({ provider: provider.name, reason: "failed" });
      continue;
    }
    const latencyMs = Date.now() - start;
    if (result.status === "unavailable") {
      const isTimeout = result.reason_code === "timeout";
      // A real timeout is a loud failure (judge health must count it), unlike
      // a benign "no API key configured yet" — that stays uncounted (RF-2).
      if (isTimeout) recordFailureSafe(deps.db, question.name, provider.name, "timeout");
      skipped.push({ provider: provider.name, reason: isTimeout ? "timeout" : "unavailable" });
      continue;
    }
    if (result.status === "error") {
      recordFailureSafe(deps.db, question.name, provider.name, result.reason_code);
      skipped.push({ provider: provider.name, reason: "failed" });
      continue;
    }
    if (!question.outputs.includes(result.decision)) {
      recordFailureSafe(deps.db, question.name, provider.name, "out-of-enum");
      skipped.push({ provider: provider.name, reason: "failed" });
      continue;
    }
    if (result.confidence < threshold) {
      // Unsure is a normal answer, not a failure: no failures row.
      skipped.push({ provider: provider.name, reason: "below_threshold" });
      lastReason = "below-threshold";
      undecidedProbs = result.probabilities ?? undecidedProbs;
      const fallback = question.fallbackRules?.(input) ?? null;
      if (fallback !== null) return settle(fallback, 1, "rules", "fallback-after-undecided", latencyMs, i, skipped, undefined, undecidedProbs, "undecided");
      continue;
    }
    return settle(result.decision, result.confidence, provider.name, result.reason_code, latencyMs, i, skipped, result.extra, result.probabilities ?? undecidedProbs);
  }

  return escalate(lastReason, hadRealFailure(skipped) ? "failed" : "escalated", skipped, undecidedProbs ?? null);
}
