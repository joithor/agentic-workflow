import { z } from "zod";

import type { QuestionModule } from "../question.js";

// Deterministic deny list (repo-wide rule: a model never overrides the
// safety-relevant layer). Any next-step text mentioning one of these is
// always "ask", regardless of what a model provider would have said.
// A bare "push" is deliberately NOT listed: pushing a feature branch is routine
// and the model decides whether it was authorized; pushes to the base branch
// stay blocked by block-push-main.sh. Force pushes still always "ask", in
// prose ("force push", "force-pushed") and in flag form (-f, --force,
// --force-with-lease, +refspec), which no other hook covers completely.
const DENY_KEYWORDS =
  /\b(?:merge|delete|force[- ]?push(?:ed|es|ing)?)\b|\bpush\b[^\n]*?(?:\s-f\b|\s--force(?:-with-lease)?\b|\s\+[\w./-]+)/i;

export const AskCheckInputSchema = z.object({
  transcriptTail: z.string(),
});
export type AskCheckInput = z.infer<typeof AskCheckInputSchema>;

export const askCheck: QuestionModule<AskCheckInput, "continue" | "ask"> = {
  name: "ask-check",
  inputSchema: AskCheckInputSchema,
  outputs: ["continue", "ask"],
  criteria: {
    continue: "the next step was already authorized by the dispatch brief or plan",
    ask: "the next step needs the user's input first",
  },
  contentClass: "transcript",
  timeBudgetMs: 10000,
  threshold: 0.6,
  preRules: (input) => {
    if (DENY_KEYWORDS.test(input.transcriptTail)) return "ask";
    return null;
  },
  // Unsure: stopping to ask is always safe.
  fallbackRules: () => "ask",
  prompt: (input) =>
    [
      "Transcript tail (the agent's last turn):",
      input.transcriptTail,
      'Was the next step already authorized by the dispatch brief or plan? Reply {"decision":"continue"} if so, or {"decision":"ask"} if it needs the user\'s input first.',
    ].join("\n"),
};
