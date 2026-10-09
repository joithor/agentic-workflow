import { z } from "zod";

import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import type { ProcessRunner } from "../index/io.js";
import { makeScrubber } from "../scrub/scrub.js";

const scrubber = makeScrubber();

export function parseRemote(url: string): string | null {
  const m = /^(?:git@github\.com:|https:\/\/github\.com\/|ssh:\/\/git@github\.com\/)([A-Za-z0-9_.-]+)\/([A-Za-z0-9_.-]+?)(?:\.git)?\/?$/.exec(url.trim());
  return m === null ? null : `${m[1]}/${m[2]}`;
}

// gh resolves its repo from the cwd and from GH_REPO/GH_HOST; every call here passes --repo from
// the origin remote instead, so an environment override can't point it at another repo.
export async function ghRepoOf(git: GitRunner, repo: string): Promise<string> {
  const r = await git.run(["remote", "get-url", "origin"], repo);
  const slug = r.ok ? parseRemote(r.stdout) : null;
  if (slug === null) throw new SindriError("SND-EVOLVE-011", "the toolkit repo has no GitHub remote called origin");
  return slug;
}

export async function ghJson<T>(run: ProcessRunner, argv: string[], cwd: string, schema: z.ZodType<T, z.ZodTypeDef, unknown>): Promise<T> {
  const what = argv.slice(0, 3).join(" ");
  const r = await run.run(argv, { cwd, timeoutMs: 60_000 });
  if (r.code !== 0) throw new SindriError("SND-EVOLVE-003", `${what} failed: ${scrubber.scrub(r.stderr.split("\n")[0]).text}`);
  let raw: unknown;
  try {
    raw = JSON.parse(r.stdout);
  } catch {
    throw new SindriError("SND-EVOLVE-013", `${what} returned output that isn't JSON`);
  }
  const parsed = schema.safeParse(raw);
  if (!parsed.success) throw new SindriError("SND-EVOLVE-013", `${what} returned an unexpected shape: ${parsed.error.issues[0].message}`);
  return parsed.data;
}

const PrView = z.object({
  title: z.string(),
  body: z.string().nullable().transform((b) => b ?? ""),
  headRefName: z.string(),
  files: z.array(z.object({ path: z.string() })),
  state: z.string(),
  mergedAt: z.string().nullable(),
  author: z.object({ login: z.string() }),
});

export async function allowedAuthors(run: ProcessRunner, repo: string, configured: readonly string[]): Promise<string[]> {
  if (configured.length > 0) return [...configured];
  const me = await ghJson(run, ["gh", "api", "user"], repo, z.object({ login: z.string() }));
  return [me.login];
}

export async function prContext(run: ProcessRunner, repo: string, ghRepo: string, pr: number, allowed: readonly string[]) {
  const v = await ghJson(run, ["gh", "pr", "view", String(pr), "--repo", ghRepo, "--json", "title,body,headRefName,files,state,mergedAt,author"], repo, PrView);
  if (v.state !== "MERGED" || v.mergedAt === null) throw new SindriError("SND-EVOLVE-012", `PR #${pr} isn't merged`);
  if (!allowed.includes(v.author.login)) throw new SindriError("SND-EVOLVE-012", `PR #${pr} was written by ${v.author.login}, who isn't an allowed author (evolve.prAuthors)`);
  const diff = await run.run(["gh", "pr", "diff", String(pr), "--repo", ghRepo], { cwd: repo, timeoutMs: 60_000 });
  if (diff.code !== 0) throw new SindriError("SND-EVOLVE-003", `couldn't read PR #${pr}'s diff`);
  return {
    title: scrubber.scrub(v.title).text,
    body: scrubber.scrub(v.body).text,
    branch: v.headRefName,
    files: v.files.map((f) => f.path),
    author: v.author.login,
    // Scrub first, cut after: a secret straddling the cap must not leave a partial, unredacted prefix.
    diff: scrubber.scrub(diff.stdout).text.slice(0, 60_000),
  };
}
