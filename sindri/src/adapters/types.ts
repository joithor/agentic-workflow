import { SindriError, type ErrorCode } from "../errors.js";

// Spec §11.2. Adapters never throw for expected failures; they return a typed error.
export type AdapterError = { kind: "retryable" | "rate-limited" | "fatal" | "not-found"; code: ErrorCode; message: string; retryAfterMs?: number };
export type Result<T> = { ok: true; value: T } | { ok: false; error: AdapterError };

export const ok = <T>(value: T): Result<T> => ({ ok: true, value });
export const err = <T>(error: AdapterError): Result<T> => ({ ok: false, error });

export function unwrap<T>(r: Result<T>): T {
  if (r.ok) return r.value;
  throw new SindriError(r.error.code, r.error.message);
}

export interface WorkItemRef {
  id: string;
  updatedAt: string;
}

export interface Author {
  id: string;
  role: "creator" | "editor" | "commenter";
}

export interface WorkItem {
  id: string;
  title: string;
  body: string;
  url: string;
  state: "open" | "done";
  authors: Author[];
  updatedAt: string;
  // Optional, typed fields observe uses when a tracker has them (spec §11.2).
  // Missing: items sort after ordered ones, steps count 0/0, and observe hashes
  // title and body as the content hash.
  order?: number;
  steps?: { done: number; total: number };
  contentHash?: string;
  // Tracker-specific facts (plan-file: plan, task, files, codeLines, hasFilesBlock).
  meta: Record<string, string | number>;
}

export interface ScopeQuery {
  includeDone: boolean;
}

export type StatusKind = "in-progress" | "in-review" | "done";

export interface Tracker {
  scan(scope: ScopeQuery, cursor?: string): Promise<Result<{ items: WorkItemRef[]; cursor: string }>>;
  read(id: string): Promise<Result<WorkItem>>;
  comment(id: string, body: string, key: string): Promise<Result<void>>;
  setStatus(id: string, status: StatusKind): Promise<Result<void>>;
  assign(id: string, who: "self"): Promise<Result<void>>;
  attach(id: string, url: string, title: string, key: string): Promise<Result<void>>;
}
