import { createHash } from "node:crypto";

import { SindriError } from "../errors.js";
import { makeScrubber } from "../scrub/scrub.js";
import { draftPrompt } from "../scope/gather.js";
import { checkMap, ScopeMapSchema, scopeMapJsonSchema } from "../scope/map.js";
import type { Budget, ModelRunner } from "../scope/model.js";
import { RefTable } from "../scope/source.js";
import { askModel } from "./ask.js";
import { judgePair, lintLeaks, lintVariant, type Preference } from "./blind.js";
import { split, type ReplayItem } from "./corpus.js";
import { defaultPrompt, hasSafetyClause } from "./prompts.js";
import { wilsonLower } from "./stats.js";

export type CompareStatus = "won" | "lost" | "inconclusive" | "insufficient-corpus" | "leaky-variant" | "missing-safety-clause" | "incomplete";
// `variant-leaked` is a loss for the variant arm (a variant must not be able to turn a lost item into a
// tie by leaking). `current-leaked` is a tie: the variant cannot cause it, so it must not earn a win.
export type ItemVerdict = Preference | "variant-failed-checks" | "current-failed-checks" | "variant-leaked" | "current-leaked";

export interface CompareResult {
  status: CompareStatus;
  n: number;
  wins: number;
  losses: number;
  ties: number;
  errors: number;
  winRate: number;
  lower: number;
  leaks: string[];
  perItem: { id: string; verdict: ItemVerdict; reason?: string }[];
}

export const MIN_ITEMS = 20;
export const MIN_DECIDED = 10;
const MIN_TITLE = 12;

interface Gen {
  runner: ModelRunner;
  model: string;
  budget: Budget;
  maxPackChars: number;
}

interface Mapped {
  kind: "map";
  ok: boolean;
  json: string;
}
interface Failed {
  kind: "failed";
  why: string;
}

const scrubber = makeScrubber();
const sha = (s: string): string => createHash("sha256").update(s).digest("hex");

// Every draft goes through askModel: the budget is checked before the call and charged after it,
// failed answers included.
async function draftWith(item: ReplayItem, system: string, o: Gen): Promise<Mapped | Failed> {
  const refs = new RefTable();
  refs.add(item.brief);
  for (const r of item.records) refs.add(r);
  // The replay records are already gathered, so there are no counts, notes or keywords to carry.
  const p = draftPrompt({ brief: item.brief, refs, keywords: [], notes: [], counts: {}, scrubber }, o.maxPackChars, undefined, { system });
  const a = await askModel(o.runner, o.budget, {
    role: "draft", model: o.model, system: p.system, input: p.input, schema: scopeMapJsonSchema(), parse: (v) => ScopeMapSchema.parse(v), timeoutMs: 600_000,
  });
  if (!a.ok) return { kind: "failed", why: a.why };
  return { kind: "map", ok: checkMap(a.value, refs).length === 0, json: JSON.stringify(a.value) };
}

async function draftBoth(item: ReplayItem, o: { current: string; variant: string }, gen: Gen): Promise<Failed | { kind: "pair"; cur: Mapped; vari: Mapped }> {
  const cur = await draftWith(item, o.current, gen);
  if (cur.kind === "failed") return cur;
  const vari = await draftWith(item, o.variant, gen);
  return vari.kind === "failed" ? vari : { kind: "pair", cur, vari };
}

const empty = (status: CompareStatus, leaks: string[], n = 0): CompareResult => ({ status, n, wins: 0, losses: 0, ties: 0, errors: 0, winRate: 0, lower: 0, leaks, perItem: [] });

// Meta words a scope map says that nothing it was given said first.
function outputLeaks(json: string, given: Set<string>): string[] {
  return lintLeaks(json).filter((f) => !given.has(f));
}

export async function compareScopeDraft(o: {
  items: ReplayItem[]; current: string; variant: string; runner: ModelRunner; models: { scoping: string; judge: string };
  budget: Budget; maxPackChars: number; onProgress?: (done: number, total: number) => void;
}): Promise<CompareResult> {
  if (o.models.judge === o.models.scoping) throw new SindriError("SND-EVOLVE-009", "the judge model must differ from the model that drafts the maps");
  const { holdout } = split(o.items);
  if (!hasSafetyClause("scope.draft", o.variant)) return empty("missing-safety-clause", []);
  const titles = holdout.map((i) => i.brief.title).filter((t) => t.length >= MIN_TITLE);
  const leaks = lintVariant(o.variant, defaultPrompt("scope.draft"), titles);
  if (leaks.length > 0) return empty("leaky-variant", leaks);
  if (holdout.length < MIN_ITEMS) return empty("insufficient-corpus", [], holdout.length);
  const variantHash = sha(o.variant);
  const perItem: CompareResult["perItem"] = [];
  const gen: Gen = { runner: o.runner, model: o.models.scoping, budget: o.budget, maxPackChars: o.maxPackChars };
  let incomplete = false;
  for (const item of holdout) {
    if (o.budget.exhausted()) {
      incomplete = true;
      break;
    }
    try {
      const d = await draftBoth(item, o, gen);
      if (d.kind === "failed") {
        if (o.budget.exhausted()) {
          incomplete = true;
          break;
        }
        perItem.push({ id: item.id, verdict: "tie", reason: d.why });
      } else {
        const { cur, vari } = d;
        const given = new Set(lintLeaks([item.brief, ...item.records].map((r) => `${r.title}\n${r.text}`).join("\n")));
        if (outputLeaks(vari.json, given).length > 0) perItem.push({ id: item.id, verdict: "variant-leaked" });
        else if (!vari.ok) perItem.push({ id: item.id, verdict: "variant-failed-checks" });
        else if (!cur.ok) perItem.push({ id: item.id, verdict: "current-failed-checks" });
        else if (outputLeaks(cur.json, given).length > 0) perItem.push({ id: item.id, verdict: "current-leaked" });
        else {
          // The seed binds the label order to the item and the variant, so a variant can't be tuned to an item's order.
          const j = await judgePair({ runner: o.runner, model: o.models.judge, budget: o.budget, task: item.brief.text, current: cur.json, variant: vari.json, seed: `${item.id}:${variantHash}` });
          if (j.incomplete && o.budget.exhausted()) {
            incomplete = true;
            break;
          }
          if (j.incomplete) perItem.push({ id: item.id, verdict: "tie", reason: j.reasons[0] });
          else perItem.push({ id: item.id, verdict: j.preference });
        }
      }
    } catch (e) {
      perItem.push({ id: item.id, verdict: "tie", reason: e instanceof Error ? e.message : String(e) });
    }
    o.onProgress?.(perItem.length, holdout.length);
  }
  const wins = perItem.filter((p) => p.verdict === "variant" || p.verdict === "current-failed-checks").length;
  const losses = perItem.filter((p) => p.verdict === "current" || p.verdict === "variant-failed-checks" || p.verdict === "variant-leaked").length;
  const ties = perItem.length - wins - losses;
  const decided = wins + losses;
  const winRate = decided === 0 ? 0 : wins / decided;
  const lower = wilsonLower(wins, decided);
  const errors = perItem.filter((p) => p.reason !== undefined).length;
  let status: CompareStatus = "lost";
  if (incomplete) status = "incomplete";
  else if (decided < MIN_DECIDED) status = "inconclusive";
  else if (winRate >= 0.6 && lower > 0.5) status = "won";
  return { status, n: perItem.length, wins, losses, ties, errors, winRate, lower, leaks: [], perItem };
}
