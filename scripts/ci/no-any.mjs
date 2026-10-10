#!/usr/bin/env node
// scripts/ci/no-any.mjs — merge gate 6 (AGENTS.md): no `any` type outside the Fastify
// integration boundary. Parses every tracked .ts/.tsx file with the TypeScript compiler
// (pinned in .github/ci-tools), so `any` inside strings and comments never counts.
// Existing violations live in a ratchet baseline (path -> count) that may only shrink.
//
// Usage: node scripts/ci/no-any.mjs [--root DIR] [--baseline FILE]
// Exit:  0 pass · 1 a file has more `any` than its baseline · 2 tool error
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, "../..");
const RULE = "AW-GATE-6";
// The Fastify integration boundary: the bridge's server, routes and transport.
const BOUNDARY = ["mcp-bridge/src/server.ts", "mcp-bridge/src/routes/", "mcp-bridge/src/transport/"];

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const root = path.resolve(arg("--root", REPO));
const baselinePath = path.resolve(arg("--baseline", path.join(REPO, "scripts/ci/no-any-baseline.json")));

let ts;
try {
  ts = createRequire(path.join(REPO, ".github/ci-tools/package.json"))("typescript");
} catch {
  console.error(`${RULE}: typescript not installed — run: npm ci --prefix .github/ci-tools --ignore-scripts`);
  process.exit(2);
}

const baseline = JSON.parse(readFileSync(baselinePath, "utf8"));
const files = execFileSync("git", ["-C", root, "ls-files", "-z", "--", "*.ts", "*.tsx"], { encoding: "utf8" })
  .split("\0")
  .filter((f) => f && !f.endsWith(".d.ts") && !f.includes("node_modules/"))
  .filter((f) => !BOUNDARY.some((b) => (b.endsWith("/") ? f.startsWith(b) : f === b)));

const over = [];
const shrunk = [];
for (const file of files) {
  const text = readFileSync(path.join(root, file), "utf8");
  const kind = file.endsWith(".tsx") ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sf = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, false, kind);
  const hits = [];
  const visit = (node) => {
    if (node.kind === ts.SyntaxKind.AnyKeyword) {
      const { line, character } = sf.getLineAndCharacterOfPosition(node.getStart(sf));
      hits.push(`${file}:${line + 1}:${character + 1}`);
    }
    ts.forEachChild(node, visit);
  };
  visit(sf);
  const allowed = baseline[file] ?? 0;
  if (hits.length > allowed) over.push({ file, hits, allowed });
  else if (hits.length < allowed) shrunk.push(`${file}: ${hits.length} now, baseline ${allowed}`);
}

for (const s of shrunk) console.log(`${RULE}: ${s} — lower the baseline in ${path.relative(REPO, baselinePath)}`);
if (over.length === 0) {
  console.log(`${RULE}: ok — ${files.length} files, no any outside the Fastify boundary beyond the baseline`);
  process.exit(0);
}
for (const { file, hits, allowed } of over) {
  for (const h of hits) console.log(`${h}: any type (${RULE}: ${hits.length} in file, baseline ${allowed})`);
  console.log(`  fix: use unknown or a real type in ${file}; any is allowed only in ${BOUNDARY.join(", ")}`);
}
process.exit(1);
