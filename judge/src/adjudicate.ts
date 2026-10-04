// A stronger model than anything in the judge chain labels stored inputs:
// 3 samples, keep only a strict-majority in-enum answer (RF-4: a split,
// an off-enum or an unparseable answer stores nothing). It sees the
// question's own prompt, every option's criteria description and the input,
// never the decision being scored.
import { QUESTIONS } from "./commands.js";
import { nextUnlabeled, recordLabel, type Db } from "./db.js";
import { toRef, type QuestionModule } from "./question.js";
import type { Provider } from "./types.js";

export async function adjudicate(
  db: Db, question: string,
  opts: { provider: Provider; limit: number; samples?: number; now: () => Date; questions?: Readonly<Record<string, QuestionModule<unknown, string>>> },
): Promise<{ labeled: number; split: number; failed: number }> {
  const q = (opts.questions ?? QUESTIONS)[question];
  if (q === undefined) throw new Error(`unknown question: ${question}`);
  const samples = opts.samples ?? 3;
  const outputs = q.outputs as readonly string[];
  const rubric = Object.entries(q.criteria ?? {}).map(([o, d]) => `- ${o}: ${d}`).join("\n");
  const skipped = new Set<string>();
  let labeled = 0;
  let split = 0;
  let failed = 0;
  for (let n = 0; n < opts.limit; n++) {
    const item = nextUnlabeled(db, question, "adjudicator", skipped);
    if (item === undefined) break;
    skipped.add(item.id);
    const parsed = parseInput(q, item.input_json);
    if (parsed === undefined) {
      failed++;
      continue;
    }
    const ref = toRef(q, parsed.input);
    const adjRef = { ...ref, prompt: rubric === "" ? ref.prompt : `${ref.prompt}\n\nOptions:\n${rubric}` };
    const counts = new Map<string, number>();
    let errors = 0;
    for (let s = 0; s < samples; s++) {
      const r = await opts.provider.decide(adjRef, parsed.input, Math.max(q.timeBudgetMs, 60000));
      if (r.status !== "decided") errors++;
      else if (outputs.includes(r.decision)) counts.set(r.decision, (counts.get(r.decision) ?? 0) + 1);
    }
    const [top, topCount] = [...counts.entries()].sort((a, b) => b[1] - a[1])[0] ?? ["", 0];
    if (topCount * 2 > samples) {
      recordLabel(db, item.id, top, opts.now().toISOString(), "adjudicator");
      labeled++;
    } else if (errors === samples) failed++;
    else split++;
  }
  return { labeled, split, failed };
}

function parseInput(q: QuestionModule<unknown, string>, json: string): { input: unknown } | undefined {
  let raw: unknown;
  try {
    raw = JSON.parse(json);
  } catch {
    return undefined;
  }
  const r = q.inputSchema.safeParse(raw);
  return r.success ? { input: r.data } : undefined;
}
