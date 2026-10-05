// Offline re-scoring of one provider against automatic labels (Navigator
// scripts/judge_eval.py, adapted). Calls provider.decide directly: no chain,
// no evaluate(), no decisions rows. --record/--replay lets a threshold or
// report change re-run without network.
import fs from "node:fs";
import path from "node:path";

import { QUESTIONS } from "./commands.js";
import { labeledItems, sharedLabels, type Db, type LabelSource } from "./db.js";
import { toRef } from "./question.js";
import type { Provider, ProviderResult } from "./types.js";

export interface EvalResult { itemId: string; label: string; result: ProviderResult<string>; latencyMs: number; inputTokens?: number }
export interface SweepRow { threshold: number; coverage: number; accuracyCovered: number }
export interface SourceAgreement { shared: number; agreed: number; rate: number }
export interface EvalReport {
  question: string; provider: string; variant: string;
  labelSource: LabelSource | "any";
  n: number; decided: number;
  accuracy: number; decisiveRate: number; accuracyDecisive: number; p50LatencyMs: number; sweep: SweepRow[];
  // Present only when the question has both outcome and adjudicator labels on
  // shared items: results scored against each source separately.
  perSource?: { outcome: EvalReport; adjudicator: EvalReport; agreement: SourceAgreement };
  // Set when the two sources disagree on more than 30% of shared items.
  sourceWarning?: string;
}
export type Variant = (input: unknown) => unknown;

export const SWEEP = [0.5, 0.6, 0.7, 0.8, 0.9] as const;
export const DISAGREEMENT_LIMIT = 0.3;
export const SOURCE_WARNING = "label sources disagree on >30% of shared items — neither is ground truth";

// question -> variant name -> transform of the stored input. Later plans fill
// this in by key (Plan C: prompt-sort axes; Plan B: resolution-check and
// turn-progress); "as-is" is always available.
export const VARIANTS: Record<string, Record<string, Variant>> = {};

const ratio = (a: number, b: number): number => (b === 0 ? 0 : a / b);

export function scoreEval(
  question: string, provider: string, variant: string, threshold: number, results: readonly EvalResult[], labelSource: LabelSource | "any" = "any",
): EvalReport {
  const n = results.length;
  const decidedRows = results.filter((r) => r.result.status === "decided") as Array<EvalResult & { result: { status: "decided"; decision: string; confidence: number } }>;
  const correct = (r: (typeof decidedRows)[number]): boolean => r.result.decision === r.label;
  const covered = (t: number) => decidedRows.filter((r) => r.result.confidence >= t);
  const decisive = covered(threshold);
  const latencies = results.map((r) => r.latencyMs).sort((a, b) => a - b);
  return {
    question, provider, variant, labelSource, n, decided: decidedRows.length,
    accuracy: ratio(decidedRows.filter(correct).length, n),
    decisiveRate: ratio(decisive.length, n),
    accuracyDecisive: ratio(decisive.filter(correct).length, decisive.length),
    p50LatencyMs: latencies.length === 0 ? 0 : (latencies[Math.floor((latencies.length - 1) / 2)] as number),
    sweep: SWEEP.map((t) => {
      const c = covered(t);
      return { threshold: t, coverage: ratio(c.length, n), accuracyCovered: ratio(c.filter(correct).length, c.length) };
    }),
  };
}

const pct = (x: number): string => `${(x * 100).toFixed(1)}%`;

function summaryLine(r: EvalReport): string {
  return `n=${r.n} decided=${r.decided} accuracy=${pct(r.accuracy)} decisive=${pct(r.decisiveRate)} accuracy-on-decisive=${pct(r.accuracyDecisive)} p50=${r.p50LatencyMs}ms`;
}

export function renderEvalReport(r: EvalReport): string {
  const lines = [
    `# judge eval: ${r.question} / ${r.provider} / ${r.variant}`,
    "",
    `Label source: ${r.labelSource}`,
    "",
    summaryLine(r),
    "",
    "| Threshold | Coverage | Accuracy (covered) |",
    "|---|---|---|",
    ...r.sweep.map((s) => `| ${s.threshold} | ${pct(s.coverage)} | ${pct(s.accuracyCovered)} |`),
    "",
  ];
  if (r.perSource !== undefined) {
    const { outcome, adjudicator, agreement } = r.perSource;
    lines.push(
      "## Per label source (shared items)",
      "",
      `Outcome vs adjudicator agreement: ${agreement.agreed}/${agreement.shared} (${pct(agreement.rate)})`,
      "",
      `- outcome: ${summaryLine(outcome)}`,
      `- adjudicator: ${summaryLine(adjudicator)}`,
      "",
    );
  }
  if (r.sourceWarning !== undefined) lines.push(`WARNING: ${r.sourceWarning}`, "");
  return lines.join("\n");
}

function withSources(db: Db, base: EvalReport, results: readonly EvalResult[], threshold: number): EvalReport {
  const shared = sharedLabels(db, base.question);
  const byId = new Map(shared.map((s) => [s.itemId, s]));
  const scored = results.filter((r) => byId.has(r.itemId));
  if (scored.length === 0) return base;
  const against = (source: "outcome" | "adjudicator"): EvalReport =>
    scoreEval(base.question, base.provider, base.variant, threshold, scored.map((r) => ({ ...r, label: (byId.get(r.itemId) as (typeof shared)[number])[source] })), source);
  const agreed = shared.filter((s) => s.outcome === s.adjudicator).length;
  const agreement: SourceAgreement = { shared: shared.length, agreed, rate: ratio(agreed, shared.length) };
  const out: EvalReport = { ...base, perSource: { outcome: against("outcome"), adjudicator: against("adjudicator"), agreement } };
  if ((shared.length - agreed) / shared.length > DISAGREEMENT_LIMIT) out.sourceWarning = SOURCE_WARNING;
  return out;
}

export async function runEval(
  db: Db,
  opts: { question: string; provider: Provider; variant: string; labels?: LabelSource | "any"; record?: (line: string) => void; replay?: readonly EvalResult[] },
): Promise<{ exitCode: number; stdout: string; stderr?: string; report?: EvalReport }> {
  const question = QUESTIONS[opts.question];
  if (question === undefined) return { exitCode: 1, stdout: "", stderr: `unknown question: ${opts.question}` };
  const variant: Variant | undefined = opts.variant === "as-is" ? (i) => i : VARIANTS[opts.question]?.[opts.variant];
  if (variant === undefined) return { exitCode: 1, stdout: "", stderr: `unknown variant for ${opts.question}: ${opts.variant}` };
  const labels = opts.labels ?? "any";
  const items = labeledItems(db, opts.question, labels);
  if (items.length === 0) return { exitCode: 1, stdout: "", stderr: `no labeled items for ${opts.question} (run: judge label import, judge label outcomes, judge adjudicate ${opts.question})` };

  let results: EvalResult[];
  if (opts.replay !== undefined) {
    // Last row per item wins; labels always come from the db for the current source.
    const latest = new Map(opts.replay.map((r) => [r.itemId, r] as const));
    results = items.flatMap((item) => {
      const row = latest.get(item.id);
      return row === undefined ? [] : [{ ...row, label: item.label }];
    });
  } else {
    results = [];
    for (const item of items) {
      const parsed = question.inputSchema.safeParse(variant(JSON.parse(item.input_json)));
      const start = Date.now();
      const result: ProviderResult<string> = parsed.success
        ? await opts.provider.decide(toRef(question, parsed.data), parsed.data, question.timeBudgetMs)
        : { status: "error", reason_code: "invalid-input" };
      const row: EvalResult = { itemId: item.id, label: item.label, result, latencyMs: Date.now() - start };
      results.push(row);
      opts.record?.(JSON.stringify(row));
    }
  }
  const base = scoreEval(opts.question, opts.provider.name, opts.variant, question.threshold, results, labels);
  const report = withSources(db, base, results, question.threshold);
  return { exitCode: 0, stdout: JSON.stringify(report), report };
}

// File helpers (kept here, not in the coverage-excluded cli.ts).
export function makeRecorder(file: string): (line: string) => void {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  return (line) => fs.appendFileSync(file, line + "\n");
}

export function readReplay(file: string): EvalResult[] {
  return fs.readFileSync(file, "utf8").split("\n").filter((l) => l.trim() !== "").map((l) => JSON.parse(l) as EvalResult);
}

export function writeEvalReport(dir: string, report: EvalReport, ts: number): string {
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, `${report.question}-${report.provider}-${report.variant}-${ts}.md`);
  fs.writeFileSync(file, renderEvalReport(report));
  return file;
}
