import { z } from "zod";

import { ScriptStepSchema } from "./script-schema.js";

const SelectorSchema = z.union([z.object({ testId: z.string().min(1) }).strict(), z.object({ text: z.string().min(1) }).strict()]);
const BoxSchema = z.object({ x: z.number().int().nonnegative(), y: z.number().int().nonnegative(), w: z.number().int().positive(), h: z.number().int().positive() });
const PointSchema = z.object({ x: z.number(), y: z.number() });

const BoxExpectationSchema = z.object({
  target: SelectorSchema,
  relativeTo: SelectorSchema.optional(),
  expected: z.object({ w: z.number().optional(), h: z.number().optional(), x: z.number().optional(), y: z.number().optional() }).refine((e) => Object.keys(e).length > 0, "expected needs at least one of w/h/x/y"),
  tolerance: z.number().nonnegative().optional(),
});

// Typed into the form so the page matches the design's sample data. Never saved:
// there is no submit step in a frame.
const FormValueSchema = z.union([
  z.object({ testId: z.string().min(1), value: z.string() }).strict(),
  z.object({ css: z.string().min(1), value: z.string() }).strict(),
]);

const FrameSchema = z.object({
  /** File-name safe: becomes `<name>-design.png` etc. */
  name: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9_-]*$/),
  /** PNG exported at 1x (Figma MCP `get_screenshot` at frame size, or any export). */
  designPng: z.string().min(1),
  designRegion: BoxSchema,
  /** Element whose bbox origin locates the implementation crop. */
  anchor: SelectorSchema,
  anchorOffset: PointSchema.optional(),
  /** Wait for this to be visible before the steps run (a loading spinner would pass text-absent checks vacuously). */
  waitFor: SelectorSchema.optional(),
  steps: z.array(ScriptStepSchema).optional(),
  formValues: z.array(FormValueSchema).optional(),
  expectBoxes: z.array(BoxExpectationSchema).optional(),
  knownDifferences: z.array(z.string().min(1)).optional(),
  /** Figma node id or URL, quoted in Linear subtitles. */
  designNode: z.string().optional(),
});

export const DesignManifestSchema = z.object({
  route: z.string().startsWith("/"),
  /** Must already be visible once the route has loaded — proves the page is past its spinner. */
  ready: SelectorSchema,
  viewport: z.object({ w: z.number().int().positive(), h: z.number().int().positive() }).default({ w: 1512, h: 982 }),
  /** How long to wait for `ready`, a frame's `waitFor` and its anchor. */
  timeoutMs: z.number().int().positive().default(60_000),
  settleMs: z.number().int().nonnegative().default(600),
  threshold: z.number().min(0).max(1).default(0.1),
  includeAA: z.boolean().default(false),
  /** Where to click to drop focus. Default: 22px in from the viewport's bottom-right corner. `false` skips the click. */
  neutralClick: z.union([PointSchema, z.literal(false)]).optional(),
  designNode: z.string().optional(),
  /** CSS selectors for the sign-in form; the credentials themselves come only from the named env vars. */
  login: z
    .object({
      emailEnv: z.string().regex(/^[A-Z][A-Z0-9_]*$/),
      passwordEnv: z.string().regex(/^[A-Z][A-Z0-9_]*$/),
      emailSelector: z.string().min(1),
      passwordSelector: z.string().min(1),
      /** Clicked after the email (two-step forms) — optional. */
      nextSelector: z.string().optional(),
      submitSelector: z.string().min(1),
      /** Sign-in is done once the URL path no longer contains this. */
      loginPath: z.string().default("/login"),
    })
    .optional(),
  frames: z.array(FrameSchema).min(1),
});
export type DesignManifest = z.infer<typeof DesignManifestSchema>;
export type DesignFrame = DesignManifest["frames"][number];

export function parseDesignManifest(raw: unknown): DesignManifest | { error: string } {
  const parsed = DesignManifestSchema.safeParse(raw);
  if (!parsed.success) return { error: parsed.error.message };
  const names = parsed.data.frames.map((f) => f.name);
  const dupe = names.find((n, i) => names.indexOf(n) !== i);
  return dupe === undefined ? parsed.data : { error: `duplicate frame name "${dupe}"` };
}
