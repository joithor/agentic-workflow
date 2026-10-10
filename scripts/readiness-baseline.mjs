#!/usr/bin/env node
// THROWAWAY — R0 readiness baseline (docs/superpowers/specs/2026-10-09-agent-readiness-design.md §10, R0).
// Scores the nine agent-readiness pillars per criterion from what this repo shows, and writes a markdown report.
// Replaced by Aw.Readiness in R1b, which becomes the score of record (decision R4). Node >= 20 stdlib only.
//
// Usage: node scripts/readiness-baseline.mjs [--root DIR] [--repo OWNER/NAME] [--no-remote] [--date YYYY-MM-DD] [--out FILE]
// Exit codes: 0 report written, 2 tool error (bad flag or root). Remote reads are `gh api` GETs only.

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';

const APPS = [
  'mcp-bridge', 'scorer', 'judge', 'sindri',
  'skills/ui-evidence', 'skills/bugFixOrchestrator', 'mods/aw-live',
];

const PILLARS = [
  'Style & Validation', 'Build System', 'Testing', 'Documentation', 'Development Environment',
  'Debugging & Observability', 'Security', 'Task Discovery', 'Product & Experimentation',
];

const SOURCES = {
  'F-pillar': 'https://docs.factory.com/agent-readiness/overview — criteria examples per pillar',
  'F-level': 'https://docs.factory.com/agent-readiness/overview — example criteria per level',
  'S5': 'docs/superpowers/specs/2026-10-09-agent-readiness-design.md §5 — level inferred (Factory gives none)',
  'inferred': 'no published level; placed by this script (see "How it scores")',
};

// GitHub's default label set; a repo with only these has no labeling scheme of its own.
const DEFAULT_LABELS = new Set([
  'bug', 'documentation', 'duplicate', 'enhancement', 'good first issue',
  'help wanted', 'invalid', 'question', 'wontfix',
]);

function die(msg) {
  process.stderr.write(`readiness-baseline: ${msg}\n`);
  process.exit(2);
}

function parseArgs(argv) {
  const opts = { root: null, repo: null, remote: true, date: null, out: null };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const val = () => { const v = argv[++i]; if (v === undefined) die(`${a} needs a value`); return v; };
    if (a === '--root') opts.root = val();
    else if (a === '--repo') opts.repo = val();
    else if (a === '--no-remote') opts.remote = false;
    else if (a === '--date') opts.date = val();
    else if (a === '--out') opts.out = val();
    else die(`unknown flag ${a}`);
  }
  return opts;
}

// ---------- filesystem helpers ----------

const SKIP_DIRS = new Set(['node_modules', '.git', 'dist', 'coverage', '.claude', 'bin', 'obj']);

function walk(dir, out = []) {
  if (!existsSync(dir)) return out;
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const p = join(dir, name);
    const st = statSync(p);
    if (st.isDirectory()) walk(p, out); else out.push(p);
  }
  return out;
}

const read = (p) => (existsSync(p) ? readFileSync(p, 'utf8') : '');
const firstExisting = (root, paths) => paths.find((p) => existsSync(join(root, p))) ?? null;

function pkg(appDir) {
  try { return JSON.parse(readFileSync(join(appDir, 'package.json'), 'utf8')); } catch { return null; }
}
const allDeps = (p) => Object.keys({ ...(p?.dependencies ?? {}), ...(p?.devDependencies ?? {}) });

// ---------- remote (gh api GET only) ----------

function ghGet(path) {
  try {
    const out = execFileSync('gh', ['api', path], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    return { ok: true, json: JSON.parse(out) };
  } catch (e) {
    const msg = String(e.stderr || e.message || e).trim().split('\n').pop();
    return { ok: false, error: msg };
  }
}

// ---------- criteria ----------
// repo criteria: check(ctx) -> { state, evidence }
// app criteria:  app(ctx, appDir, rel) -> { pass: boolean, why: string }

function workflows(root) {
  const dir = join(root, '.github', 'workflows');
  return existsSync(dir) ? readdirSync(dir).filter((f) => /\.ya?ml$/.test(f)).map((f) => join(dir, f)) : [];
}

const remoteUnknown = (ctx) => ({ state: 'unknown', evidence: ctx.remoteWhy });

const CRITERIA = [
  // Style & Validation
  { id: 'SV-1', pillar: 0, level: 1, src: 'F-level', name: 'Linter configured', app: (ctx, d) => {
    const f = firstExisting(d, ['biome.json', 'biome.jsonc', 'eslint.config.js', 'eslint.config.mjs', '.eslintrc.json', '.eslintrc.cjs'])
      ?? firstExisting(ctx.root, ['biome.json', 'biome.jsonc', 'eslint.config.js', 'eslint.config.mjs', '.eslintrc.json']);
    const dep = allDeps(pkg(d)).find((x) => /^(eslint|@biomejs\/biome)$/.test(x));
    return f || dep ? { pass: true, why: f ?? dep } : { pass: false, why: 'no lint config or linter dependency' };
  } },
  { id: 'SV-2', pillar: 0, level: 1, src: 'F-level', name: 'Type checker (strict)', app: (ctx, d) => {
    const ts = read(join(d, 'tsconfig.json'));
    if (!ts) return { pass: false, why: 'no tsconfig.json' };
    return /"strict"\s*:\s*true/.test(ts) ? { pass: true, why: 'tsconfig strict: true' } : { pass: false, why: 'tsconfig without strict: true' };
  } },
  { id: 'SV-3', pillar: 0, level: 1, src: 'S5', name: 'Code formatter', app: (ctx, d) => {
    const names = ['biome.json', 'biome.jsonc', '.prettierrc', '.prettierrc.json', 'prettier.config.js', 'dprint.json'];
    const f = firstExisting(d, names) ?? firstExisting(ctx.root, names);
    const dep = allDeps(pkg(d)).find((x) => /^(prettier|@biomejs\/biome|dprint)$/.test(x));
    return f || dep ? { pass: true, why: f ?? dep } : { pass: false, why: 'no formatter config or dependency' };
  } },
  { id: 'SV-4', pillar: 0, level: 2, src: 'F-level', name: 'Pre-commit hooks (tracked config)', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.pre-commit-config.yaml', '.husky', 'lefthook.yml', '.lefthook.yml', '.githooks']);
    return f ? { state: 'pass', evidence: f }
      : { state: 'fail', evidence: 'no tracked hook config (.pre-commit-config.yaml, .husky, lefthook, .githooks); hooks installed per clone are not reproducible' };
  } },

  // Build System
  { id: 'BS-1', pillar: 1, level: 2, src: 'S5', name: 'Build command documented', check: (ctx) => {
    for (const f of ['AGENTS.md', 'README.md']) {
      const m = read(join(ctx.root, f)).match(/\b(npm run build|dotnet build|make build|cargo build|go build)\b/);
      if (m) return { state: 'pass', evidence: `${f} names \`${m[1]}\`` };
    }
    return { state: 'fail', evidence: 'no build command line in AGENTS.md or README.md (packages list their npm scripts; no single build command)' };
  } },
  { id: 'BS-2', pillar: 1, level: 2, src: 'S5', name: 'Dependencies pinned (lockfile)', app: (ctx, d) => {
    const f = firstExisting(d, ['package-lock.json', 'pnpm-lock.yaml', 'yarn.lock', 'packages.lock.json']);
    return f ? { pass: true, why: f } : { pass: false, why: 'no lockfile' };
  } },
  { id: 'BS-3', pillar: 1, level: 2, src: 'inferred', name: 'VCS CLI tools documented', check: (ctx) => {
    const hit = ['AGENTS.md', 'README.md'].find((f) => /`gh[ `]|\bgh (pr|api|repo|issue)\b/.test(read(join(ctx.root, f))));
    return hit ? { state: 'pass', evidence: `${hit} documents the gh CLI` } : { state: 'fail', evidence: 'gh CLI not documented' };
  } },
  { id: 'BS-4', pillar: 1, level: 4, src: 'F-level', name: 'Regular deployment frequency (≥3 releases in 90 days)', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const r = ghGet(`repos/${ctx.repo}/releases?per_page=100`);
    if (!r.ok) return { state: 'unknown', evidence: `gh api releases: ${r.error}` };
    const cutoff = ctx.now - 90 * 864e5;
    const n = r.json.filter((x) => Date.parse(x.published_at ?? x.created_at) >= cutoff).length;
    return { state: n >= 3 ? 'pass' : 'fail', evidence: `${n} releases published in the 90 days before ${ctx.date}` };
  } },

  // Testing
  { id: 'TS-1', pillar: 2, level: 1, src: 'F-level', name: 'Unit tests exist', app: (ctx, d) => {
    const t = walk(d).filter((p) => /\.(test|spec)\.[cm]?[jt]sx?$|Tests?\.cs$/.test(p));
    return t.length ? { pass: true, why: `${t.length} test files` } : { pass: false, why: 'no test files' };
  } },
  { id: 'TS-2', pillar: 2, level: 3, src: 'F-level', name: 'Integration tests exist', app: (ctx, d) => {
    const re = /(^|\/)(integration|e2e|contract|browser|routes)(\/|[.-])/;
    const t = walk(d).filter((p) => /\.(test|spec)\.[cm]?[jt]sx?$/.test(p) && re.test(relative(d, p)));
    const script = Object.keys(pkg(d)?.scripts ?? {}).find((s) => /^test:(integration|e2e|heavy)$/.test(s));
    if (t.length || script) return { pass: true, why: script ? `script ${script}` : `${t.length} files under integration/e2e/contract/browser/routes` };
    return { pass: false, why: 'no integration/e2e/contract/browser/routes tests or test:integration|e2e|heavy script' };
  } },
  { id: 'TS-3', pillar: 2, level: 3, src: 'S5', name: 'Tests runnable locally (one command)', app: (ctx, d, rel) => {
    if (pkg(d)?.scripts?.test) return { pass: true, why: 'npm test' };
    const agents = read(join(ctx.root, 'AGENTS.md'));
    const line = agents.split('\n').find((l) => l.includes(rel) && /\btest\b/.test(l));
    return line ? { pass: true, why: 'test command in AGENTS.md' } : { pass: false, why: 'no test script or documented test command' };
  } },
  { id: 'TS-4', pillar: 2, level: 4, src: 'F-level', name: 'Fast CI feedback', check: (ctx) => {
    const wf = workflows(ctx.root);
    if (!wf.length) return { state: 'fail', evidence: 'no .github/workflows — no CI at all' };
    return { state: 'unknown', evidence: `${wf.length} workflows; speed needs run durations (not measured by R0)` };
  } },
  { id: 'TS-5', pillar: 2, level: 4, src: 'F-level', name: 'Flaky test detection', check: (ctx) => {
    const wf = workflows(ctx.root);
    const hit = wf.find((f) => /flak|quarantin/i.test(read(f)));
    if (hit) return { state: 'pass', evidence: relative(ctx.root, hit) };
    return { state: 'fail', evidence: wf.length ? 'no flaky/quarantine job in workflows' : 'no .github/workflows — no CI at all' };
  } },

  // Documentation
  { id: 'DOC-1', pillar: 3, level: 2, src: 'F-level', name: 'AGENTS.md', check: (ctx) =>
    read(join(ctx.root, 'AGENTS.md')).trim() ? { state: 'pass', evidence: 'AGENTS.md present' } : { state: 'fail', evidence: 'no AGENTS.md' } },
  { id: 'DOC-2', pillar: 3, level: 1, src: 'F-level', name: 'README', check: (ctx) =>
    read(join(ctx.root, 'README.md')).trim() ? { state: 'pass', evidence: 'README.md present' } : { state: 'fail', evidence: 'no README.md' } },
  { id: 'DOC-3', pillar: 3, level: 4, src: 'S5', name: 'Documentation freshness checked automatically', check: (ctx) => {
    const wf = workflows(ctx.root);
    const hit = wf.find((f) => /sync-rules\.sh --check|docs?-fresh|lychee|markdown-link-check/.test(read(f)));
    if (hit) return { state: 'pass', evidence: relative(ctx.root, hit) };
    return { state: 'fail', evidence: wf.length ? 'no doc-freshness step in workflows' : 'no .github/workflows; doc checks (sync-rules --check) run by hand only' };
  } },

  // Development Environment
  { id: 'DE-1', pillar: 4, level: 2, src: 'F-level', name: 'Devcontainer', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.devcontainer/devcontainer.json', '.devcontainer.json']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no .devcontainer' };
  } },
  { id: 'DE-2', pillar: 4, level: 2, src: 'S5', name: 'Environment template', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.env.example', '.env.template', '.env.sample']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no .env.example' };
  } },
  { id: 'DE-3', pillar: 4, level: 2, src: 'inferred', name: 'Local services setup', check: (ctx) => {
    const f = firstExisting(ctx.root, ['setup.sh', 'docker-compose.yml', 'docker-compose.yaml', 'compose.yml', 'compose.yaml', 'devbox.json']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no setup script or compose file' };
  } },

  // Debugging & Observability
  { id: 'OBS-1', pillar: 5, level: 3, src: 'S5', name: 'Structured logging', app: (ctx, d) => {
    const dep = allDeps(pkg(d)).find((x) => /^(pino|winston|bunyan)$/.test(x));
    if (dep) return { pass: true, why: dep };
    const src = walk(join(d, 'src')).find((p) => /Fastify\(\s*\{[^}]*logger:\s*(true|\{)/.test(read(p)));
    return src ? { pass: true, why: `Fastify pino logger (${relative(d, src)})` } : { pass: false, why: 'no structured logger' };
  } },
  { id: 'OBS-2', pillar: 5, level: 3, src: 'F-level', name: 'Distributed tracing', app: (ctx, d) => {
    const dep = allDeps(pkg(d)).find((x) => x.startsWith('@opentelemetry/'));
    return dep ? { pass: true, why: dep } : { pass: false, why: 'no OpenTelemetry' };
  } },
  { id: 'OBS-3', pillar: 5, level: 3, src: 'F-level', name: 'Metrics collection', app: (ctx, d) => {
    const dep = allDeps(pkg(d)).find((x) => /^(prom-client|@opentelemetry\/sdk-metrics)$/.test(x));
    return dep ? { pass: true, why: dep } : { pass: false, why: 'no metrics library' };
  } },

  // Security
  { id: 'SEC-1', pillar: 6, level: 2, src: 'F-level', name: 'Branch protection on the default branch', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const prot = ghGet(`repos/${ctx.repo}/branches/main/protection`);
    if (prot.ok) return { state: 'pass', evidence: 'classic branch protection on main' };
    const rules = ghGet(`repos/${ctx.repo}/rules/branches/main`);
    if (rules.ok && rules.json.length) return { state: 'pass', evidence: `${rules.json.length} ruleset rules apply to main` };
    if (rules.ok && /not protected/i.test(prot.error)) return { state: 'fail', evidence: `protection: "${prot.error}"; rulesets on main: none` };
    return { state: 'unknown', evidence: `protection: ${prot.error}; rules: ${rules.ok ? 'none' : rules.error}` };
  } },
  { id: 'SEC-2', pillar: 6, level: 3, src: 'F-level', name: 'Secret scanning', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const r = ghGet(`repos/${ctx.repo}`);
    const s = r.ok ? r.json.security_and_analysis?.secret_scanning?.status : null;
    if (!s) return { state: 'unknown', evidence: r.ok ? 'security_and_analysis not readable' : `gh api: ${r.error}` };
    const pp = r.json.security_and_analysis?.secret_scanning_push_protection?.status ?? 'n/a';
    return { state: s === 'enabled' ? 'pass' : 'fail', evidence: `secret_scanning ${s}, push_protection ${pp}` };
  } },
  { id: 'SEC-3', pillar: 6, level: 3, src: 'F-pillar', name: 'CODEOWNERS', check: (ctx) => {
    const f = firstExisting(ctx.root, ['CODEOWNERS', '.github/CODEOWNERS', 'docs/CODEOWNERS']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no CODEOWNERS' };
  } },

  // Task Discovery
  { id: 'TD-1', pillar: 7, level: 3, src: 'S5', name: 'Issue templates', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.github/ISSUE_TEMPLATE', '.github/ISSUE_TEMPLATE.md', 'ISSUE_TEMPLATE.md']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no issue templates' };
  } },
  { id: 'TD-2', pillar: 7, level: 3, src: 'S5', name: 'Issue labeling system', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const r = ghGet(`repos/${ctx.repo}/labels?per_page=100`);
    if (!r.ok) return { state: 'unknown', evidence: `gh api labels: ${r.error}` };
    const own = r.json.map((l) => l.name).filter((n) => !DEFAULT_LABELS.has(n));
    return own.length ? { state: 'pass', evidence: `${own.length} labels beyond GitHub defaults` }
      : { state: 'fail', evidence: `${r.json.length} labels, all GitHub defaults` };
  } },
  { id: 'TD-3', pillar: 7, level: 3, src: 'S5', name: 'PR template', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.github/pull_request_template.md', '.github/PULL_REQUEST_TEMPLATE.md', '.github/PULL_REQUEST_TEMPLATE', 'pull_request_template.md', 'docs/pull_request_template.md']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no PR template' };
  } },

  // Product & Experimentation
  { id: 'PE-1', pillar: 8, level: 4, src: 'S5', name: 'Product analytics instrumentation', check: () => ({
    state: 'unknown', evidence: "Factory's check is not public; scorer telemetry exists but is not framed as product analytics (spec §3)" }) },
  { id: 'PE-2', pillar: 8, level: 4, src: 'S5', name: 'Experiment infrastructure', check: () => ({
    state: 'unknown', evidence: "Factory's check is not public; judge shadow mode and evolve offline comparisons exist (spec §3)" }) },
  { id: 'PE-3', pillar: 8, level: 5, src: 'F-level', name: 'Self-improving systems (pillar placement inferred)', check: () => ({
    state: 'unknown', evidence: "Factory's check is not public; sindri evolve exists (Plan 5)" }) },
];

// ---------- scoring ----------

function evaluate(ctx) {
  return CRITERIA.map((c) => {
    if (c.check) {
      const r = c.check(ctx);
      return { ...c, scope: 'repo', state: r.state, evidence: r.evidence,
        credit: r.state === 'pass' ? 1 : 0, score: r.state === 'pass' ? '1 / 1' : r.state === 'fail' ? '0 / 1' : '—' };
    }
    const per = APPS.map((rel) => {
      const d = join(ctx.root, rel);
      if (!existsSync(d)) return { rel, pass: false, why: 'absent' };
      return { rel, ...c.app(ctx, d, rel) };
    });
    const k = per.filter((p) => p.pass).length;
    const fails = per.filter((p) => !p.pass).map((p) => `${p.rel} (${p.why})`);
    const passes = per.filter((p) => p.pass).map((p) => `${p.rel} (${p.why})`);
    const evidence = [passes.length && `pass: ${passes.join(', ')}`, fails.length && `fail: ${fails.join(', ')}`].filter(Boolean).join('; ');
    return { ...c, scope: 'app', state: k === APPS.length ? 'pass' : 'fail', evidence,
      credit: k / APPS.length, score: `${k} / ${APPS.length}` };
  });
}

// Levels: every repo starts at L1. Walking the levels that have scored criteria in order, each one that reaches
// 80% (fractional credit for app criteria; n/a and unknown are not scored) unlocks the level above it; the first
// one below 80% stops the walk. Levels with nothing scored are skipped, so they neither unlock nor block.
function levelOf(rows) {
  const byLevel = [1, 2, 3, 4, 5].map((L) => {
    const scored = rows.filter((r) => r.level === L && (r.state === 'pass' || r.state === 'fail'));
    const credit = scored.reduce((s, r) => s + r.credit, 0);
    return { L, n: scored.length, credit, ratio: scored.length ? credit / scored.length : null };
  });
  let level = 1;
  for (const b of byLevel) {
    if (b.ratio === null) continue;
    if (b.ratio < 0.8) break;
    level = Math.min(5, b.L + 1);
  }
  return { level, byLevel };
}

// ---------- render ----------

const pct = (b) => (b.ratio === null ? '—' : `${b.credit.toFixed(2).replace(/\.00$/, '')} / ${b.n} (${Math.round(b.ratio * 100)}%)`);
const esc = (s) => String(s).replace(/\|/g, '\\|');

function render(ctx, rows) {
  const out = [];
  out.push('# Agent readiness baseline — 2026-10 (R0)', '');
  out.push('> **Throwaway R0 baseline** (readiness spec §10). Generated by `scripts/readiness-baseline.mjs`, a',
    '> manual-pass-equivalent script that will be deleted when `Aw.Readiness` lands in R1b. `Aw.Readiness` becomes',
    '> the score of record (decision R4); no paid Factory report or Droid was used.', '');
  out.push(`Repository: \`${ctx.repo ?? 'unknown'}\` · Apps (${APPS.length}): ${APPS.map((a) => `\`${a}\``).join(', ')}`, '');
  out.push(`Generated: ${ctx.stamp}`, '');

  const overall = levelOf(rows);
  out.push('## Summary', '');
  out.push(`**Overall level: ${overall.level}** (of 5). Cells show passed credit / scored criteria at that level.`, '');
  out.push('| Pillar | L1 | L2 | L3 | L4 | L5 | Level |', '|---|---|---|---|---|---|---|');
  PILLARS.forEach((name, i) => {
    const l = levelOf(rows.filter((r) => r.pillar === i));
    out.push(`| ${name} | ${l.byLevel.map(pct).join(' | ')} | ${l.level} |`);
  });
  out.push(`| **Overall** | ${overall.byLevel.map(pct).join(' | ')} | **${overall.level}** |`, '');
  const counts = ['pass', 'fail', 'n/a', 'unknown'].map((s) => `${s} ${rows.filter((r) => r.state === s).length}`).join(' · ');
  out.push(`Criteria: ${rows.length} (${counts}).`, '');

  out.push('## How it scores', '',
    '- **Criteria** are the examples Factory publishes per pillar and per level (Sources). Where Factory gives no level,',
    '  the level comes from spec §5 (`S5`) or is placed by this script (`inferred`).',
    '- **States:** `pass`, `fail`, `n/a` (with a reason) or `unknown` (Factory\'s check is not public, or the data was',
    '  not readable). `unknown` and `n/a` are not scored.',
    `- **Scope:** \`repo\` criteria are checked once. \`app\` criteria are checked in each of the ${APPS.length} apps (spec §2) and`,
    '  score as a fraction; they count as fractional credit and show `pass` only when every app passes.',
    '- **Levels:** every repo starts at Level 1. Level N+1 unlocks when at least 80% of the scored Level N criteria',
    '  pass and every lower level with scored criteria also reached 80%. Levels with nothing scored (in a pillar, or',
    '  overall) are skipped: they neither unlock nor block.',
    '- **Remote data** comes from read-only `gh api` GETs (branch protection, rulesets, secret scanning, labels,',
    '  releases).', '');

  out.push('## Criteria by pillar', '');
  PILLARS.forEach((name, i) => {
    out.push(`### ${name}`, '');
    out.push('| ID | Criterion | Level | Scope | State | Score | Evidence | Source |', '|---|---|---|---|---|---|---|---|');
    for (const r of rows.filter((x) => x.pillar === i)) {
      out.push(`| ${r.id} | ${esc(r.name)} | ${r.level} | ${r.scope} | ${r.state} | ${r.score} | ${esc(r.evidence)} | ${r.src} |`);
    }
    out.push('');
  });

  out.push('## Sources', '');
  for (const [k, v] of Object.entries(SOURCES)) out.push(`- \`${k}\`: ${v}`);
  out.push('');
  return out.join('\n');
}

// ---------- main ----------

function main() {
  const opts = parseArgs(process.argv.slice(2));
  let root = opts.root;
  if (!root) {
    try { root = execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim(); } catch { die('not in a git repo; pass --root'); }
  }
  if (!existsSync(root) || !statSync(root).isDirectory()) die(`root is not a directory: ${root}`);

  let repo = opts.repo;
  if (!repo) {
    try {
      const url = execFileSync('git', ['-C', root, 'remote', 'get-url', 'origin'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
      repo = url.replace(/\.git$/, '').match(/[:/]([^/:]+\/[^/]+)$/)?.[1] ?? null;
    } catch { repo = null; }
  }
  const remote = opts.remote && Boolean(repo);
  const remoteWhy = !opts.remote ? 'not read (--no-remote)' : 'no origin remote; pass --repo';
  const date = opts.date ?? new Date().toISOString().slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) die(`--date must be YYYY-MM-DD, got ${date}`);
  const ctx = {
    root, repo, remote, remoteWhy, date,
    now: Date.parse(`${date}T23:59:59Z`),
    stamp: opts.date ? date : new Date().toISOString(),
  };

  const md = render(ctx, evaluate(ctx));
  if (opts.out) writeFileSync(opts.out, md); else process.stdout.write(md);
}

main();
