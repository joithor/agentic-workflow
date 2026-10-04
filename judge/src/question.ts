import type { ZodType } from "zod";

import type { ContentClass, JsonSchemaFragment, QuestionRef } from "./types.js";

export interface QuestionModule<I, O extends string> {
  name: string;
  inputSchema: ZodType<I>;
  outputs: readonly O[];
  prompt: (input: I) => string;
  threshold: number;
  contentClass: ContentClass;
  timeBudgetMs: number;
  preRules?: (input: I) => O | null;
  // Cheap rule-based answer used only when a model answered below threshold
  // (distinct from preRules, which run before any model).
  fallbackRules?: (input: I) => O | null;
  extraProperties?: Readonly<Record<string, JsonSchemaFragment>>;
  // What each output means, for the adjudicator (and, from Task 5, Jev).
  criteria?: Readonly<Record<O, string>>;
}

export function toRef<I, O extends string>(q: QuestionModule<I, O>, input: I): QuestionRef<O> {
  const ref: QuestionRef<O> = { name: q.name, outputs: q.outputs, prompt: q.prompt(input), contentClass: q.contentClass };
  const withExtra = q.extraProperties === undefined ? ref : { ...ref, extraProperties: q.extraProperties };
  return q.criteria === undefined ? withExtra : { ...withExtra, criteria: q.criteria };
}
