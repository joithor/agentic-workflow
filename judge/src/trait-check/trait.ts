// A trait is one reusable code rule in a Markdown file: YAML frontmatter for the
// fields below, the body as optional context (examples, BAD -> GOOD diffs). The
// format follows crosshatch/traits (Apache-2.0), plus two fields of our own:
// `paths` scopes a trait to files without a model call, and `severity` decides
// whether the Stop gate holds the agent back over it.
import { parse as parseYaml } from "yaml";
import { z } from "zod";

import { hasUnclosedBrace } from "./glob.js";

const text = z.string().regex(/\S/);

export const TraitFrontmatter = z
  .object({
    name: z.string().regex(/^(?!trait-)[a-z0-9]+(?:-[a-z0-9]+)*$/).max(80),
    subject: text,
    requirement: text,
    applicability: text.optional(),
    purpose: text,
    guidance: text.optional(),
    paths: z.array(z.string().min(1).refine((g) => !hasUnclosedBrace(g), "unclosed { in glob")).min(1).optional(),
    severity: z.enum(["block", "warn"]).default("block"),
  })
  .strict();

export type Trait = z.infer<typeof TraitFrontmatter> & { context?: string; file: string };

export type TraitParse = { ok: true; trait: Trait } | { ok: false; file: string; error: string };

const FRONTMATTER = /^---\r?\n([\s\S]*?)\r?\n---[ \t]*(?:\r?\n([\s\S]*))?$/;

export function parseTrait(source: string, file: string): TraitParse {
  const match = FRONTMATTER.exec(source);
  if (match === null) return { ok: false, file, error: "missing or unterminated --- frontmatter" };
  let raw: unknown;
  try {
    raw = parseYaml(match[1] as string);
  } catch (e) {
    return { ok: false, file, error: `invalid YAML: ${(e as Error).message.split("\n")[0]}` };
  }
  const parsed = TraitFrontmatter.safeParse(raw);
  if (!parsed.success) {
    const issue = parsed.error.issues[0] as z.ZodIssue;
    return { ok: false, file, error: `${issue.path.join(".") || "frontmatter"}: ${issue.message}` };
  }
  const stem = file.replace(/^.*\//, "").replace(/\.md$/, "");
  if (stem !== parsed.data.name) return { ok: false, file, error: `name "${parsed.data.name}" must match the file name "${stem}.md"` };
  const context = (match[2] ?? "").trim();
  return { ok: true, trait: { ...parsed.data, ...(context === "" ? {} : { context }), file } };
}
