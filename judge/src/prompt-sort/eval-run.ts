// judge/src/prompt-sort/eval-run.ts
// For every axis and label source: score the heuristic-only policy and the
// production blend on the labeled prompts, via Plan A's runEval (so scoring,
// collapse and replay semantics are identical to `judge eval`). The sorter
// eval provider shares one Jev request per distinct prompt across all axes,
// sources and both policies.
import { labeledItems, type Db } from "../db.js";
import { runEval, type EvalReport, type EvalResult } from "../eval.js";
import { ALL_AXES, collapseLabel, positiveClass, sortQuestionName, type AxisName } from "./axes.js";
import { makeSortEvalProvider } from "./eval-provider.js";
import type { SortDeps } from "./sort.js";

export type LabelSourceName = "outcome" | "adjudicator";
export interface PolicyScore { accuracy: number; ppv: number | null }
export interface AxisEvalSummary { axis: AxisName; source: LabelSourceName; n: number; heuristic: PolicyScore; blend: PolicyScore }
export interface AgreementSummary { axis: AxisName; shared: number; agreed: number }
export interface SortEvalResult { ranAt: string; summaries: AxisEvalSummary[]; agreement: AgreementSummary[] }

function ppvOf(axis: AxisName, question: string, results: readonly EvalResult[]): number | null {
  const positive = positiveClass(axis);
  const predicted = results.filter((r) => r.result.status === "decided" && collapseLabel(question, r.result.decision) === positive);
  if (predicted.length === 0) return null;
  return predicted.filter((r) => collapseLabel(question, r.label) === positive).length / predicted.length;
}

export async function runSortEval(db: Db, opts: { sortDeps: SortDeps; now: () => Date }): Promise<SortEvalResult> {
  const provider = makeSortEvalProvider(opts.sortDeps);
  const summaries: AxisEvalSummary[] = [];
  const agreement: AgreementSummary[] = [];
  for (const axis of ALL_AXES) {
    const question = sortQuestionName(axis);
    for (const source of ["adjudicator", "outcome"] as const) {
      if (labeledItems(db, question, source).length === 0) continue;
      const score = async (variant: "heuristic" | "blend"): Promise<{ accuracy: number; ppv: number | null; n: number }> => {
        const recorded: EvalResult[] = [];
        const out = await runEval(db, { question, provider, variant, labels: source, record: (line) => recorded.push(JSON.parse(line) as EvalResult) });
        const report = out.report as EvalReport; // labeledItems is non-empty here, so runEval always reports
        return { accuracy: report.accuracy, ppv: ppvOf(axis, question, recorded), n: report.n };
      };
      const h = await score("heuristic");
      const b = await score("blend");
      summaries.push({ axis, source, n: b.n, heuristic: { accuracy: h.accuracy, ppv: h.ppv }, blend: { accuracy: b.accuracy, ppv: b.ppv } });
    }
    const outcome = new Map(labeledItems(db, question, "outcome").map((r) => [r.id, collapseLabel(question, r.label)]));
    const shared = labeledItems(db, question, "adjudicator").filter((r) => outcome.has(r.id));
    if (shared.length > 0) {
      agreement.push({ axis, shared: shared.length, agreed: shared.filter((r) => outcome.get(r.id) === collapseLabel(question, r.label)).length });
    }
  }
  return { ranAt: opts.now().toISOString(), summaries, agreement };
}

const pct = (x: number): string => `${(x * 100).toFixed(1)}%`;
const ppvText = (x: number | null): string => (x === null ? "n/a" : pct(x));

export function renderSortEval(result: SortEvalResult): string {
  return [
    `# judge prompt-sort eval (${result.ranAt})`,
    "",
    "| Axis | Source | n | Heuristic acc | Blend acc | Blend precision |",
    "|---|---|---|---|---|---|",
    ...result.summaries.map((s) => `| ${s.axis} | ${s.source} | ${s.n} | ${pct(s.heuristic.accuracy)} | ${pct(s.blend.accuracy)} | ${ppvText(s.blend.ppv)} |`),
    "",
    "## Label-source agreement",
    "",
    "| Axis | Shared | Agreed |",
    "|---|---|---|",
    ...result.agreement.map((a) => `| ${a.axis} | ${a.shared} | ${a.agreed} |`),
    "",
  ].join("\n");
}
