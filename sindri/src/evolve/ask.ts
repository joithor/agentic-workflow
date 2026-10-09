import { SindriError } from "../errors.js";
import type { Budget, ModelCall, ModelRunner, ModelUsage } from "../scope/model.js";

export type Asked<T> = { ok: true; value: T } | { ok: false; why: string };

// The one way evolve code calls a model: check the budget first, spend usage after, and turn a
// failed or malformed answer into a result the loop can report instead of an exception that
// aborts the run.
export async function askModel<T>(runner: ModelRunner, budget: Budget, call: ModelCall<T>): Promise<Asked<T>> {
  if (budget.exhausted()) return { ok: false, why: "token budget exhausted" };
  try {
    const r = await runner.run(call);
    budget.spend(r.usage);
    return { ok: true, value: r.value };
  } catch (e) {
    if (typeof e === "object" && e !== null && "usage" in e) budget.spend((e as { usage: ModelUsage }).usage);
    if (e instanceof SindriError && (e.code === "SND-SCOPE-002" || e.code === "SND-SCOPE-004")) return { ok: false, why: e.message };
    throw e;
  }
}
