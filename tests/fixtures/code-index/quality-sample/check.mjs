#!/usr/bin/env node
// check.mjs: the shape of pairs.json, and (when its commit is in this clone) that every pair's lines are what it says.
//   node check.mjs        exit 0 holds, 1 a check fails, 2 unreadable input
// The line numbers belong to pairs.json's `commit`, not to today's trunk, so the text check reads `git show <commit>:<file>`
// and is skipped with a note when that commit is not here (a shallow clone). Resolving the pairs is the probe's job.
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const doc = JSON.parse(readFileSync(join(here, 'pairs.json'), 'utf8'));
const errors = [];
const fail = (m) => errors.push(m);
const git = (...args) => execFileSync('git', ['-C', here, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });

if (doc.pairs.length !== 50) fail(`${doc.pairs.length} pairs, expected 50`);
const ids = new Set();
for (const p of doc.pairs) {
  if (ids.has(p.id)) fail(`duplicate id ${p.id}`);
  ids.add(p.id);
  if (typeof p.label !== 'boolean') fail(`${p.id}: label is not a boolean`);
  if (!['static', 'dispatch'].includes(p.kind)) fail(`${p.id}: kind ${p.kind}`);
  if (p.kind === 'dispatch' && p.label !== true) fail(`${p.id}: a dispatch pair is a true pair`);
  if (!p.how || !p.weakSpot || typeof p.reviewed !== 'boolean') fail(`${p.id}: how, weakSpot or reviewed missing`);
  for (const end of [p.caller, p.callee]) if (!end.file.startsWith('src/') || !end.file.endsWith('.cs') || !(end.line >= 1)) fail(`${p.id}: bad end ${JSON.stringify(end)}`);
}
const trues = doc.pairs.filter((p) => p.label).length;
if (trues < 15 || trues > 35) fail(`${trues} true pairs: the sample must hold both labels in numbers`);

let checked = false;
try { git('cat-file', '-e', `${doc.commit}^{commit}`); checked = true; } catch { /* commit absent: skip the text check */ }
if (checked) {
  for (const p of doc.pairs) {
    for (const [what, end, needle] of [['caller', p.caller, p.callee.name], ['callee', p.callee, p.callee.name]]) {
      let line;
      try { line = git('show', `${doc.commit}:${end.file}`).split('\n')[end.line - 1]; } catch { fail(`${p.id}: ${end.file} not in ${doc.commit}`); continue; }
      if (line?.trim().slice(0, 200) !== end.text) fail(`${p.id}: ${what} text differs at ${end.file}:${end.line}`);
      if (!line?.includes(needle)) fail(`${p.id}: ${what} line does not hold ${needle}`);
    }
  }
}

if (errors.length) { for (const m of errors) console.error(`FAIL ${m}`); process.exit(1); }
console.log(`ok: ${doc.pairs.length} pairs (${trues} true), ${checked ? `texts match ${doc.commit.slice(0, 12)}` : `commit ${doc.commit.slice(0, 12)} not in this clone: texts not checked`}`);
