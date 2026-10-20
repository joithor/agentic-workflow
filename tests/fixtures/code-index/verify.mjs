#!/usr/bin/env node
// verify.mjs: checks expected.json against the fixture sources, so a moved line or a typo cannot rot the golden.
//   node verify.mjs        exit 0 holds, 1 a check fails (each failure on stderr), 2 unreadable input
// It checks bookkeeping only (a symbol's lines hold its name, an edge's line holds the callee name and lies in the
// caller's span, every target exists, counts add up); resolving calls is the CallResolver's job, not this script's.
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), 'project');
const doc = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'expected.json'), 'utf8'));
const errors = [];
const fail = (m) => errors.push(m);
const lines = new Map();
const text = (file) => {
  if (!lines.has(file)) lines.set(file, existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8').split('\n') : null);
  return lines.get(file);
};

const byKey = new Map();
for (const s of doc.symbols) {
  if (byKey.has(s.key)) fail(`duplicate symbol key ${s.key}`);
  byKey.set(s.key, s);
  const src = text(s.file);
  if (!src) { fail(`${s.key}: file ${s.file} missing`); continue; }
  if (s.key !== `${s.file}:${s.startLine}`) fail(`${s.key}: key is not file:startLine`);
  if (s.startLine < 1 || s.endLine < s.startLine || s.endLine > src.length) fail(`${s.key}: span ${s.startLine}-${s.endLine} outside the file`);
  const head = src[s.startLine - 1] ?? '';
  const needle = s.kind === 'accessor' ? s.signature : s.name;
  if (!head.includes(needle)) fail(`${s.key}: line ${s.startLine} does not hold "${needle}"`);
}

let rows = 0;
for (const e of doc.edges) {
  const src = byKey.get(e.src);
  if (!src) { fail(`edge from unknown symbol ${e.src}`); continue; }
  if (e.line < src.startLine || e.line > src.endLine) fail(`${e.src} -> ${e.name}: line ${e.line} outside ${src.startLine}-${src.endLine}`);
  const line = text(src.file)[e.line - 1] ?? '';
  if (!line.includes(e.name)) fail(`${e.src} -> ${e.name}: line ${e.line} does not hold the name`);
  if (e.confidence === 'unresolved' ? e.dst.length !== 0 : e.dst.length === 0) fail(`${e.src} -> ${e.name}: dst does not fit ${e.confidence}`);
  if ((e.confidence === 'certain' || e.confidence === 'likely') && e.dst.length !== 1) fail(`${e.src} -> ${e.name}: ${e.confidence} takes one target`);
  if (e.confidence === 'ambiguous' && e.dst.length < 2) fail(`${e.src} -> ${e.name}: ambiguous takes two or more targets`);
  if (e.confidence !== 'unresolved' && e.candidates !== e.dst.length) fail(`${e.src} -> ${e.name}: candidates ${e.candidates} != ${e.dst.length}`);
  for (const d of e.dst) if (!byKey.has(d)) fail(`${e.src} -> ${e.name}: unknown target ${d}`);
  rows += Math.max(1, e.dst.length);
}

const c = doc.counts;
if (c.symbols !== doc.symbols.length) fail(`counts.symbols ${c.symbols} != ${doc.symbols.length}`);
if (c.edges !== doc.edges.length) fail(`counts.edges ${c.edges} != ${doc.edges.length}`);
for (const [k, n] of Object.entries(c.byConfidence)) {
  const have = doc.edges.filter((e) => e.confidence === k).length;
  if (have !== n) fail(`counts.byConfidence.${k} ${n} != ${have}`);
}

if (errors.length) { for (const m of errors) console.error(`FAIL ${m}`); process.exit(1); }
console.log(`ok: ${doc.symbols.length} symbols, ${doc.edges.length} call groups, ${rows} edge rows`);
