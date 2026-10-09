import { z } from "zod";

import { err, ok, type Result } from "../../adapters/types.js";
import type { Scrubber } from "../../scrub/scrub.js";
import { clean, keywordHits, type Source, type SourceRecord } from "../source.js";

export type GraphqlFetch = (
  url: string,
  init: { method: "POST"; headers: Record<string, string>; body: string; signal: AbortSignal; redirect: "error" },
) => Promise<{ ok: boolean; status: number; json(): Promise<unknown> }>;

export interface LinearIssue {
  identifier: string;
  title: string;
  description: string;
  createdAt: string;
  url: string;
  creator: string | null;
  comments: { body: string; createdAt: string; author: string | null }[];
}

export interface LinearProject {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  url: string;
  issues: LinearIssue[];
}


export function projectSlug(ref: string): string {
  const bare = ref.replace(/^linear:/, "");
  const m = /\/project\/[^/?#]*-([0-9a-z]+)(?:[/?#]|$)/i.exec(bare);
  return m !== null ? m[1] : bare;
}

const PAGE = 50;
const SMALL_PAGE = 25;

const PROJECT = `query P($slug: String!) { projects(filter: { slugId: { eq: $slug } }) { nodes { id name description createdAt url } } }`;
const issuesQuery = (first: number): string => `query I($id: String!, $after: String) { project(id: $id) { issues(first: ${first}, after: $after, includeArchived: true) {
  pageInfo { hasNextPage endCursor }
  nodes { identifier title description createdAt url creator { name } comments(first: 20) { nodes { body createdAt user { name } } } } } } }`;

const ProjectAnswer = z.object({ data: z.object({ projects: z.object({ nodes: z.array(z.object({ id: z.string(), name: z.string(), description: z.string().nullable(), createdAt: z.string(), url: z.string() })) }) }) });
const IssueNode = z.object({
  identifier: z.string(), title: z.string(), description: z.string().nullable(), createdAt: z.string(), url: z.string(),
  creator: z.object({ name: z.string() }).nullable(),
  comments: z.object({ nodes: z.array(z.object({ body: z.string(), createdAt: z.string(), user: z.object({ name: z.string() }).nullable() })) }),
});
const IssuesAnswer = z.object({ data: z.object({ project: z.object({ issues: z.object({ pageInfo: z.object({ hasNextPage: z.boolean(), endCursor: z.string().nullable() }), nodes: z.array(IssueNode) }) }) }) });

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === "object" && v !== null && !Array.isArray(v);

type Ctx = { apiUrl: string; token: string; fetch: GraphqlFetch; scrubber?: Scrubber };

async function gql<T>(o: Ctx, query: string, variables: object, schema: z.ZodType<T>): Promise<Result<T>> {
  // Error text is scrubbed and the token is masked in it, so a server or proxy that echoes it back cannot leak it.
  const s = (v: string): string => (o.token.length >= 8 ? clean(v, o.scrubber).split(o.token).join("[REDACTED:token]") : clean(v, o.scrubber));
  let res: Awaited<ReturnType<GraphqlFetch>>;
  try {
    res = await o.fetch(o.apiUrl, {
      method: "POST",
      headers: { "content-type": "application/json", authorization: o.token },
      body: JSON.stringify({ query, variables }),
      signal: AbortSignal.timeout(30_000),
      redirect: "error",
    });
  } catch (e) {
    return err({ kind: "retryable", code: "SND-SCOPE-011", message: `Linear unreachable: ${s((e as Error).message).slice(0, 200)}` });
  }
  if (res.status === 401 || res.status === 403) return err({ kind: "fatal", code: "SND-SCOPE-010", message: "Linear rejected the token" });
  let body: unknown;
  try {
    body = await res.json();
  } catch {
    body = null;
  }
  if (isRecord(body) && Array.isArray(body.errors) && body.errors.length > 0) {
    const first: unknown = body.errors[0];
    const text = s(isRecord(first) && typeof first.message === "string" ? first.message : "no message").slice(0, 200);
    const code = isRecord(first) && isRecord(first.extensions) ? first.extensions.code : undefined;
    if (code === "RATELIMITED") return err({ kind: "rate-limited", code: "SND-SCOPE-011", message: `Linear rate limit: ${text}` });
    return err({ kind: "fatal", code: "SND-SCOPE-011", message: `Linear returned GraphQL errors: ${text}` });
  }
  if (!res.ok) return err({ kind: "retryable", code: "SND-SCOPE-011", message: `Linear answered HTTP ${res.status}` });
  const parsed = schema.safeParse(body);
  return parsed.success ? ok(parsed.data) : err({ kind: "fatal", code: "SND-SCOPE-011", message: "Linear answered in an unexpected shape" });
}

export async function fetchLinearProject(o: { apiUrl: string; token: string; fetch: GraphqlFetch; ref: string; scrubber?: Scrubber }): Promise<Result<LinearProject>> {
  const s = (v: string | null | undefined): string => clean(v ?? "", o.scrubber);
  const slug = projectSlug(o.ref);
  const p = await gql(o, PROJECT, { slug }, ProjectAnswer);
  if (!p.ok) return p;
  const node = p.value.data.projects.nodes[0];
  if (node === undefined) return err({ kind: "not-found", code: "SND-SCOPE-011", message: `no Linear project with slug ${slug}` });
  const issues: LinearIssue[] = [];
  let after: string | null = null;
  let first = PAGE;
  for (;;) {
    const page: Result<z.infer<typeof IssuesAnswer>> = await gql(o, issuesQuery(first), { id: node.id, after }, IssuesAnswer);
    if (!page.ok) {
      // Linear caps a query's complexity: halve the page once, then give up.
      if (first === PAGE && /complex/i.test(page.error.message)) {
        first = SMALL_PAGE;
        continue;
      }
      return page;
    }
    for (const n of page.value.data.project.issues.nodes) {
      issues.push({
        identifier: n.identifier, title: s(n.title), description: s(n.description), createdAt: n.createdAt, url: n.url, creator: n.creator === null ? null : s(n.creator.name),
        comments: n.comments.nodes.map((c) => ({ body: s(c.body), createdAt: c.createdAt, author: c.user === null ? null : s(c.user.name) })),
      });
    }
    const info: { hasNextPage: boolean; endCursor: string | null } = page.value.data.project.issues.pageInfo;
    if (!info.hasNextPage) break;
    after = info.endCursor;
  }
  issues.sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.identifier.localeCompare(b.identifier));
  return ok({ id: node.id, name: s(node.name), description: s(node.description), createdAt: node.createdAt, url: node.url, issues });
}

export function linearSource(project: LinearProject): Source {
  return {
    name: "linear",
    async find(q) {
      const records: SourceRecord[] = [];
      const keep = (text: string, createdAt: string): boolean =>
        (q.asOf === null || Date.parse(createdAt) <= q.asOf.getTime()) && (q.keywords.length === 0 || keywordHits(text, q.keywords) >= 1);
      for (const i of project.issues) {
        if (keep(`${i.title}\n${i.description}`, i.createdAt)) {
          records.push({ ref: `linear:${i.identifier}`, kind: "issue", title: i.title, text: `${i.title}\n\n${i.description}`, author: i.creator, createdAt: i.createdAt, trust: "untrusted" });
        }
        i.comments.forEach((c, n) => {
          if (keep(c.body, c.createdAt)) {
            records.push({ ref: `linear:${i.identifier}#c${n + 1}`, kind: "comment", title: `${i.identifier} comment ${n + 1}`, text: c.body, author: c.author, createdAt: c.createdAt, trust: "untrusted" });
          }
        });
      }
      return ok(records.slice(0, q.limit));
    },
  };
}
