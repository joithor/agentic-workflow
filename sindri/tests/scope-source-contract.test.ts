import fs from "node:fs";
import path from "node:path";

import { stateDir } from "../src/deps.js";
import { codeSource } from "../src/scope/sources/code.js";
import { fileSource } from "../src/scope/sources/file.js";
import { fetchLinearProject, linearSource, type GraphqlFetch } from "../src/scope/sources/linear.js";
import { notesSource } from "../src/scope/sources/notes.js";
import { transcriptsSource } from "../src/scope/sources/transcripts.js";
import { CONTRACT_HIDDEN, CONTRACT_SECRET, sourceContractTests } from "./contract/source-contract.js";
import { makeDeps, tempDir } from "./helpers.js";
import { buildIndexForTest } from "./scope-fixtures.js";

const KEYWORDS = ["shift", "times"];
const dirty = (s: string): string => `${s} shift times key ${CONTRACT_SECRET} ${CONTRACT_HIDDEN}`;
const AS_OF = new Date("2026-02-01T00:00:00Z");

sourceContractTests("file", async () => {
  const dir = tempDir();
  const file = path.join(dir, "brief.md");
  fs.writeFileSync(file, `# Shift times\n${dirty("brief")}\n`);
  return { source: fileSource(file), keywords: KEYWORDS, asOf: { mode: "ignores", at: AS_OF }, roots: [dir] };
});

sourceContractTests("notes", async () => {
  const dir = tempDir();
  fs.writeFileSync(path.join(dir, "a.md"), dirty("note a"));
  fs.mkdirSync(path.join(dir, "sub"));
  fs.writeFileSync(path.join(dir, "sub", "b.md"), dirty("note b"));
  return { source: notesSource(dir), keywords: KEYWORDS, asOf: { mode: "excludes", at: AS_OF }, roots: [dir] };
});

sourceContractTests("transcripts", async () => {
  const dir = tempDir();
  const turn = (text: string, ts: string): string => JSON.stringify({ type: "user", timestamp: ts, message: { role: "user", content: text } });
  fs.writeFileSync(path.join(dir, "s.jsonl"), `${turn(dirty("early"), "2026-01-01T00:00:00Z")}\n${turn(dirty("late"), "2026-03-01T00:00:00Z")}\n`);
  return { source: transcriptsSource(dir), keywords: KEYWORDS, asOf: { mode: "filters", at: AS_OF }, roots: [dir] };
});

sourceContractTests("code", async () => {
  const d = makeDeps();
  await buildIndexForTest(d, "r", {
    "src/shift.ts": [
      `export function saveShiftTimes(t: string[]) { /* ${dirty("save")} */ return t.length; }`,
      `export function loadShiftTimes() { /* ${dirty("load")} */ return []; }`,
    ].join("\n"),
  });
  return { source: codeSource(d, ["r"]), keywords: KEYWORDS, asOf: { mode: "excludes", at: AS_OF }, roots: [stateDir(d)] };
});

sourceContractTests("linear (fake fetch)", async () => {
  let calls = 0;
  const issue = (n: number, createdAt: string) => ({
    identifier: `ABC-${n}`, title: `Shift times ${n}`, description: dirty(`issue ${n}`), createdAt, url: `https://linear.example/issue/ABC-${n}`,
    creator: { name: "Pat" }, comments: { nodes: [{ body: dirty(`comment ${n}`), createdAt, user: null }] },
  });
  const fetch: GraphqlFetch = async (_url, init) => {
    calls++;
    const q = JSON.parse(init.body) as { query: string };
    const data = q.query.includes("projects(")
      ? { projects: { nodes: [{ id: "p1", name: "Shift times", description: "brief", createdAt: "2026-01-01T00:00:00Z", url: "https://linear.example/project/p1" }] } }
      : { project: { issues: { pageInfo: { hasNextPage: false, endCursor: null }, nodes: [issue(1, "2026-01-10T00:00:00Z"), issue(2, "2026-03-01T00:00:00Z")] } } };
    return { ok: true, status: 200, json: async () => ({ data }) };
  };
  const project = await fetchLinearProject({ apiUrl: "https://linear.example/graphql", token: "t", fetch, ref: "p1" });
  if (!project.ok) throw new Error(project.error.message);
  return { source: linearSource(project.value), keywords: KEYWORDS, asOf: { mode: "filters", at: AS_OF }, roots: [], effects: () => calls };
});
