import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import type { GitRunner } from "../../git.js";
import { err, ok, type Author, type Result, type Tracker, type WorkItem } from "../types.js";
import { parsePlan } from "./parse.js";

export function planItemId(file: string, task: number): string {
  return `${path.basename(file, ".md")}.t${task}`;
}

const READ_ONLY = <T>(): Result<T> =>
  err({ kind: "fatal", code: "SND-TRACKER-405", message: "the plan-file tracker is read-only; edit the plan file instead" });

function decodeCursor(cursor: string | undefined): Record<string, string> {
  if (cursor === undefined) return {};
  try {
    const v: unknown = JSON.parse(Buffer.from(cursor, "base64url").toString("utf8"));
    return v !== null && typeof v === "object" ? (v as Record<string, string>) : {};
  } catch {
    return {};
  }
}

// Glob-style filename match: `*` is the only wildcard.
export function wildcard(pattern: string): RegExp {
  return new RegExp(`^${pattern.replace(/[.+?^${}()|[\]\\]/g, "\\$&").replace(/\*/g, ".*")}$`);
}

// `git log --format=%x1e%ae%x09%cI --name-only` → per file: authors (newest
// first) and the date of its newest commit.
export function parseGitHistory(out: string): Map<string, { authors: string[]; date: string }> {
  const files = new Map<string, { authors: string[]; date: string }>();
  for (const record of out.split("\x1e").slice(1)) {
    const [header, ...names] = record.split("\n");
    const [email, date] = header.split("\t");
    for (const name of names.filter((n) => n !== "")) {
      const entry = files.get(name) ?? { authors: [], date };
      if (!entry.authors.includes(email)) entry.authors.push(email);
      files.set(name, entry);
    }
  }
  return files;
}

const MAX_PLAN_BYTES = 2 * 1024 * 1024;

type Entry = { item: WorkItem; hash: string };

// Plan files as work items (spec §11.2): one item per "### Task N:" heading.
// The cursor maps item id → content hash, so a rescan returns only changed tasks.
export function makePlanFileTracker(o: { repoPath: string; glob: string; include: string[]; git: GitRunner }): Tracker {
  const relDir = path.dirname(o.glob);
  const dir = path.join(o.repoPath, relDir);
  const matchers = o.include.map(wildcard);
  let cache: { key: string; items: Entry[] } | null = null;

  function planFiles(): { name: string; key: string }[] {
    return fs
      .readdirSync(dir, { withFileTypes: true })
      .filter((e) => e.isFile() && e.name.endsWith(".md") && matchers.some((m) => m.test(e.name)))
      .map((e) => ({ name: e.name, st: fs.statSync(path.join(dir, e.name)) }))
      .filter((f) => f.st.size <= MAX_PLAN_BYTES)
      .map((f) => ({ name: f.name, key: `${f.name}:${f.st.size}:${f.st.mtimeMs}` }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  // A missing repo path or plan dir is an error, never an empty backlog: an empty
  // scan would make observe mark every item removed.
  async function all(): Promise<Result<Entry[]>> {
    if (!fs.existsSync(dir)) {
      const what = fs.existsSync(o.repoPath) ? `plan dir not found: ${dir}` : `repo path not found: ${o.repoPath}`;
      return err({ kind: "fatal", code: "SND-TRACKER-001", message: what });
    }
    const files = planFiles();
    const key = files.map((f) => f.key).join("|");
    if (cache !== null && cache.key === key) return ok(cache.items);
    const log = await o.git.run(["log", "--format=%x1e%ae%x09%cI", "--name-only", "--", relDir], o.repoPath);
    const history = log.ok ? parseGitHistory(log.stdout) : new Map<string, { authors: string[]; date: string }>();
    const out: Entry[] = [];
    for (const [fileIndex, { name }] of files.entries()) {
      const rel = path.join(relDir, name);
      const plan = parsePlan(fs.readFileSync(path.join(dir, name), "utf8"));
      const h = history.get(rel);
      const authors: Author[] = (h?.authors ?? []).map((id, i, all) => ({ id, role: i === all.length - 1 ? "creator" : "editor" }));
      const date = h?.date ?? fs.statSync(path.join(dir, name)).mtime.toISOString();
      for (const t of plan.tasks) {
        const hash = createHash("sha256").update(`${t.title}\0${t.body}`).digest("hex");
        out.push({
          hash,
          item: {
            id: planItemId(name, t.number),
            title: `Task ${t.number}: ${t.title} (${plan.title})`,
            body: t.body,
            url: `${rel}#task-${t.number}`,
            state: t.stepsTotal > 0 && t.stepsDone === t.stepsTotal ? "done" : "open",
            authors,
            updatedAt: date,
            meta: {
              plan: path.basename(name, ".md"),
              task: t.number,
              order: fileIndex * 1000 + t.number,
              stepsDone: t.stepsDone,
              stepsTotal: t.stepsTotal,
              files: t.files.length,
              codeLines: t.codeLines,
              hasFilesBlock: t.hasFilesBlock ? 1 : 0,
              contentHash: hash,
            },
          },
        });
      }
    }
    cache = { key, items: out };
    return ok(out);
  }

  return {
    async scan(scope, cursor) {
      const prev = decodeCursor(cursor);
      const r = await all();
      if (!r.ok) return err(r.error);
      const items = r.value;
      const changed = items
        .filter((x) => (scope.includeDone || x.item.state === "open") && prev[x.item.id] !== x.hash)
        .map((x) => ({ id: x.item.id, updatedAt: x.item.updatedAt }));
      const next = Buffer.from(JSON.stringify(Object.fromEntries(items.map((x) => [x.item.id, x.hash])))).toString("base64url");
      return ok({ items: changed, cursor: next });
    },
    async read(id) {
      const r = await all();
      if (!r.ok) return err(r.error);
      const found = r.value.find((x) => x.item.id === id);
      return found === undefined ? err({ kind: "not-found", code: "SND-TRACKER-404", message: `no plan task ${id}` }) : ok(found.item);
    },
    comment: async () => READ_ONLY(),
    setStatus: async () => READ_ONLY(),
    assign: async () => READ_ONLY(),
    attach: async () => READ_ONLY(),
  };
}
