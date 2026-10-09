import { z } from "zod";

import { isLoopbackUrl } from "../index/loopback.js";

// Spec §11.1. Strict: an unknown key is an error, so typos fail validate.
// Later plans add optional keys in schemaVersion 1; renames bump the version.
export const SIZES = ["XS", "S", "M", "L", "XL"] as const;
export type Size = (typeof SIZES)[number];
export const SizeSchema = z.enum(SIZES);
export const sizeRank = (s: Size): number => SIZES.indexOf(s);

export const PROFILE_SCHEMA_VERSION = 1;

const NAME = /^[a-z0-9][a-z0-9-]{0,38}$/;
const name = (what: string) => z.string().regex(NAME, `${what} must be lowercase letters, digits and dashes (max 39)`);

const PlanFileTracker = z
  .object({
    type: z.literal("plan-file"),
    repo: name("tracker.repo").describe("Repo (from repos) whose plan files are the backlog"),
    glob: z
      .string()
      .regex(/^[^*]+\/\*\.md$/, "glob must look like <dir>/*.md")
      .refine((g) => !g.split("/").includes(".."), "glob must stay inside the repo (no ..)")
      .default("docs/superpowers/plans/*.md"),
    include: z
      .array(z.string().regex(/^[A-Za-z0-9*._-]+$/, "include patterns are file names with * wildcards"))
      .min(1)
      .default(["*"])
      .describe("Plan file names to read, with * wildcards, e.g. *-sindri-plan-*"),
  })
  .strict();

const unit = z.number().min(0).max(1);
const count = z.number().int().positive();
const bySize = (d: Record<Size, number>) =>
  z.object({ XS: count.default(d.XS), S: count.default(d.S), M: count.default(d.M), L: count.default(d.L), XL: count.default(d.XL) }).strict().default({});

// Always denied: index.denyPaths (profile and repo) add to these and never remove one, the way
// scrub.extraPatterns adds to the built-in patterns.
export const DEFAULT_DENY_PATHS: readonly string[] = [
  ".env*", "**/.env*", "**/*.pem", "**/*.key", "**/*.p12", "**/*.pfx", "**/id_rsa*", "**/*.tfstate", "**/*.tfvars",
  "**/credentials*", "**/.npmrc", "**/.netrc", "**/secrets/**",
];

const IndexSchema = z
  .object({
    denyPaths: z.array(z.string().min(1)).default([])
      .describe("Path globs the index never reads, case-insensitive (sensitive fixtures, generated code), added to the built-in secret globs (.env*, *.pem, **/secrets/** and the like)"),
    utilityGlobs: z.array(z.string().min(1)).default([]).describe("Globs of internal utility modules; their exports are reinvention candidates"),
    maxFileKB: count.default(512),
    maxTotalMB: count.default(200),
    maxAgeHours: count.default(24).describe("An index older than this is stale (index status and doctor warn)"),
    embeddings: z
      .object({
        enabled: z.boolean().default(true),
        url: z.string().refine(isLoopbackUrl, "must be a loopback URL (127.0.0.1 or [::1], no credentials): the index never sends code off the machine").default("http://127.0.0.1:11434"),
        model: z.string().min(1).refine((m) => !/cloud/i.test(m), "cloud models send code off the machine").default("nomic-embed-text"),
      })
      .strict()
      .default({}),
    graph: z.enum(["graphify", "none"]).default("graphify"),
    graphMaxMB: z.number().int().min(1).max(512).default(256).describe("Largest graphify graph.json the index reads, in MB (max 512, the V8 string limit); a bigger one leaves the graph layer unavailable"),
  })
  .strict()
  .default({})
  .describe("Code index (spec §6.2)");

// Every glob the index skips for one repo: the built-ins, then the profile's, then the repo's.
export const denyPathsFor = (ix: { denyPaths: readonly string[] }, repo: { denyPaths: readonly string[] }): string[] => [
  ...DEFAULT_DENY_PATHS, ...ix.denyPaths, ...repo.denyPaths,
];

const ShapeSchema = z
  .object({
    record: z.boolean().default(true).describe("Record shape signals at commit (record-only until rollout step 3b); false turns the hook step off"),
    budgetMs: count.default(2000),
    outcomeDays: count.default(14).describe("Days after a commit before its signals get an outcome label (kept, acted-on, dropped)"),
    defaultSize: SizeSchema.default("S").describe("Size class used for diff budgets when a commit has no item"),
    thresholds: z
      .object({
        nameSimilarity: unit.default(0.85),
        embedding: unit.default(0.9),
        embeddingAst: unit.default(0.6),
        nearCloneTokens: count.default(60),
        nearCloneJaccard: unit.default(0.8),
        callOverlap: unit.default(0.5),
        complexityDelta: count.default(10),
      })
      .strict()
      .default({}),
    sizeBudget: bySize({ XS: 80, S: 250, M: 600, L: 1200, XL: 2400 }).describe("Changed-line budget per size class"),
    exportAllowance: bySize({ XS: 1, S: 3, M: 6, L: 10, XL: 20 }).describe("New exports allowed per size class"),
  })
  .strict()
  .default({})
  .describe("Shape signals (spec §6.2)");

export const ProfileSchema = z
  .object({
    schemaVersion: z.literal(PROFILE_SCHEMA_VERSION).describe("Profile format version"),
    mode: z.enum(["shadow", "assist", "auto-small"]).default("shadow").describe("What sindri may do on its own (spec §7.1)"),
    user: name("user").describe("Namespace for claim refs sindri/<user>/<item> (spec §9.4)"),
    hosts: z.object({ active: z.string().min(1) }).strict().describe("hosts.active: the one host allowed to run ticks"),
    tracker: z.discriminatedUnion("type", [PlanFileTracker]).describe("Where work items come from. Plan 2 ships type: plan-file"),
    repos: z.array(name("repo")).min(1).describe("Repo names; each needs repos/<name>.yaml"),
    trustedAuthors: z
      .array(z.string().min(1))
      .default([])
      .describe("Author ids whose items may auto-start (spec §8.3). For plan-file these are git author emails, which anyone can forge; signed commits are required before auto-small"),
    trustedBots: z.array(z.string().min(1)).default([]).describe("Bot ids whose review comments feed fix rounds"),
    providers: z
      .object({ allowed: z.array(z.enum(["anthropic", "jev", "openai", "cursor"])).min(1).default(["anthropic", "jev"]) })
      .strict()
      .default({})
      .describe("providers.allowed: model providers sindri may call (spec §6.1)"),
    budget: z
      .object({ perItem: z.number().int().positive().optional(), perDay: z.number().int().positive().optional() })
      .strict()
      .default({})
      .describe("Token budgets; unset until rollout step 3a enforces them"),
    autoStartMaxSize: SizeSchema.default("XS").describe("Largest size auto-small may start"),
    selfMerge: z.enum(["human", "auto"]).default("human").describe("Who merges toolkit PRs (spec §7.7); protected modules always wait for the human"),
    scrub: z
      .object({
        extraPatterns: z
          .array(z.object({ kind: z.string().regex(/^[a-z][a-z0-9-]{1,30}$/), regex: z.string().min(1).max(200) }).strict())
          .default([]),
      })
      .strict()
      .default({})
      .describe("scrub.extraPatterns: extra secret shapes, added to the built-ins (never removes one)"),
    index: IndexSchema,
    shape: ShapeSchema,
  })
  .strict();

export type Profile = z.infer<typeof ProfileSchema>;

export const RepoSchema = z
  .object({
    schemaVersion: z.literal(PROFILE_SCHEMA_VERSION).describe("Profile format version"),
    name: name("name").describe("Must match the file name repos/<name>.yaml"),
    path: z.string().refine((p) => p.startsWith("/"), "must be an absolute path").describe("Absolute path of the local checkout"),
    defaultBranch: z.string().min(1).default("main").describe("Base branch for claims and indexes"),
    protectedPaths: z.array(z.string().min(1)).default([]).describe("Globs; a diff touching one parks for approval (spec §8.5)"),
    overrides: z.object({ autoStartMaxSize: SizeSchema.optional() }).strict().default({}).describe("Per-repo values that win over profile.yaml"),
    index: z.object({ denyPaths: z.array(z.string().min(1)).default([]) }).strict().default({}).describe("index.denyPaths for this repo, added to the profile's"),
  })
  .strict();

export type RepoConfig = z.infer<typeof RepoSchema>;
