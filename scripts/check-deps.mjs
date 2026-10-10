#!/usr/bin/env node
// scripts/check-deps.mjs — checks deps.yaml, the declared package graph (spec §4b.1a, §4e B10).
//
//   node scripts/check-deps.mjs [--root DIR] [--mermaid [--base-file FILE]]
//
// Validates deps.yaml against schemas/deps.schema.json, checks the tier, kind, cycle and fan-out rules,
// scans the TypeScript packages and shell hooks for cli/db/file edges and fails on any undeclared one,
// and checks C# <ProjectReference>s once src/ has projects. --mermaid also prints the graph and the
// edge list (added/removed against --base-file) for a PR summary.
// Exit codes: 0 ok, 1 a rule failed, 2 a tool error. Node builtins only: no install step, no network.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SCHEMA = path.join(HERE, "..", "schemas", "deps.schema.json");
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage", "tests", "test", "__tests__", "fixtures", ".git", "bin", "obj"]);
const TEST_FILE = /\.(test|spec)\.[cm]?[jt]sx?$/;
const TS_FILE = /\.([cm]?[jt]sx?)$/;

class ToolError extends Error {}
class YamlError extends Error { constructor(line, msg) { super(`deps.yaml:${line}: ${msg}`); } }

// ---------- YAML subset ----------
// Block mappings and sequences, plain/'single'/"double" scalars, [flow, sequences] of scalars, # comments.
// Rejected: anchors, aliases, tags, block scalars (| >), flow mappings, multiple documents, tabs, duplicate keys.

function stripComment(text) {
  let q = null;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) { if (c === q && !(q === '"' && text[i - 1] === "\\")) q = null; continue; }
    if (c === '"' || c === "'") { if (i === 0 || /[\s[,:-]/.test(text[i - 1])) q = c; continue; }
    if (c === "#" && (i === 0 || /\s/.test(text[i - 1]))) return text.slice(0, i).trimEnd();
  }
  return text.trimEnd();
}

function parseScalar(raw, line) {
  const s = raw.trim();
  const banned = { "&": "an anchor", "*": "an alias", "!": "a tag", "|": "a block scalar", ">": "a block scalar", "{": "a flow mapping", "%": "a directive", "@": "a reserved indicator", "`": "a reserved indicator" };
  if (banned[s[0]]) throw new YamlError(line, `${banned[s[0]]} is outside the supported YAML subset`);
  if (s.startsWith("[")) {
    if (!s.endsWith("]")) throw new YamlError(line, "unterminated flow sequence");
    const inner = s.slice(1, -1).trim();
    if (inner === "") return [];
    const parts = []; let cur = ""; let q = null;
    for (const c of inner) {
      if (q) { cur += c; if (c === q) q = null; continue; }
      if (c === '"' || c === "'") q = c;
      if (c === "[" || c === "{") throw new YamlError(line, "nested flow collections are outside the supported YAML subset");
      if (c === ",") { parts.push(cur); cur = ""; } else cur += c;
    }
    parts.push(cur);
    return parts.map((p) => parseScalar(p, line));
  }
  if (s.startsWith('"')) {
    if (!/^"(?:[^"\\]|\\.)*"$/.test(s)) throw new YamlError(line, "bad double-quoted string");
    return JSON.parse(s);
  }
  if (s.startsWith("'")) {
    if (!/^'(?:[^']|'')*'$/.test(s)) throw new YamlError(line, "bad single-quoted string");
    return s.slice(1, -1).replace(/''/g, "'");
  }
  if (s === "" || s === "null" || s === "~") return null;
  if (s === "true" || s === "false") return s === "true";
  if (/^-?[0-9]+$/.test(s)) return Number(s);
  if (/:\s|:$/.test(s)) throw new YamlError(line, `ambiguous plain scalar "${s}" (quote it)`);
  return s;
}

export function parseYaml(text) {
  const lines = [];
  text.split("\n").forEach((rawLine, i) => {
    const lead = /^[ \t]*/.exec(rawLine)[0];
    if (lead.includes("\t")) throw new YamlError(i + 1, "tab indentation");
    const body = stripComment(rawLine.slice(lead.length));
    if (body === "") return;
    if (body === "---" || body === "...") throw new YamlError(i + 1, "multiple documents are outside the supported YAML subset");
    lines.push({ indent: lead.length, text: body, line: i + 1 });
  });
  let i = 0;
  const block = (indent) => (lines[i].text === "-" || lines[i].text.startsWith("- ") ? seq(indent) : map(indent));
  function seq(indent) {
    const out = [];
    while (i < lines.length && lines[i].indent === indent && (lines[i].text === "-" || lines[i].text.startsWith("- "))) {
      const l = lines[i]; const rest = l.text.slice(1).trimStart();
      if (rest === "") { i++; out.push(i < lines.length && lines[i].indent > indent ? block(lines[i].indent) : null); continue; }
      if (/^[&*!]/.test(rest)) parseScalar(rest, l.line);
      if (/^[A-Za-z_][A-Za-z0-9_-]*:(\s|$)/.test(rest)) { lines[i] = { indent: indent + (l.text.length - rest.length), text: rest, line: l.line }; out.push(map(lines[i].indent)); continue; }
      out.push(parseScalar(rest, l.line)); i++;
    }
    return out;
  }
  function map(indent) {
    const out = {};
    while (i < lines.length && lines[i].indent === indent && !lines[i].text.startsWith("- ")) {
      const l = lines[i];
      const m = /^([A-Za-z_][A-Za-z0-9_-]*):(?:\s+(.*))?$/.exec(l.text);
      if (!m) throw new YamlError(l.line, `expected "key: value", got "${l.text}"`);
      if (Object.hasOwn(out, m[1])) throw new YamlError(l.line, `duplicate key "${m[1]}"`);
      i++;
      if (m[2] !== undefined && m[2] !== "") { out[m[1]] = parseScalar(m[2], l.line); continue; }
      const next = lines[i];
      if (next && (next.indent > indent || (next.indent === indent && next.text.startsWith("- ")))) out[m[1]] = block(next.indent);
      else out[m[1]] = null;
    }
    if (i < lines.length && lines[i].indent > indent) throw new YamlError(lines[i].line, "unexpected indentation");
    return out;
  }
  if (lines.length === 0) return null;
  const doc = block(lines[0].indent);
  if (i < lines.length) throw new YamlError(lines[i].line, "unexpected indentation");
  return doc;
}

// ---------- JSON Schema (the keywords schemas/deps.schema.json uses) ----------

const typeOf = (v) => (v === null ? "null" : Array.isArray(v) ? "array" : Number.isInteger(v) ? "integer" : typeof v);
const IGNORED = new Set(["$schema", "$id", "$defs", "title", "description"]);

export function validate(schema, value, at = "", root = schema) {
  const errs = [];
  const err = (msg) => errs.push(`${at || "/"}: ${msg}`);
  for (const [k, s] of Object.entries(schema)) {
    if (IGNORED.has(k) || k === "then") continue;
    switch (k) {
      case "$ref": errs.push(...validate(s.replace(/^#\//, "").split("/").reduce((o, p) => o[p], root), value, at, root)); break;
      case "const": if (value !== s) err(`must be ${JSON.stringify(s)}`); break;
      case "enum": if (!s.includes(value)) err(`must be one of ${s.join(", ")} (got ${JSON.stringify(value)})`); break;
      case "type": { const t = typeOf(value); const ok = [s].flat().some((x) => x === t || (x === "number" && t === "integer")); if (!ok) err(`must be ${[s].flat().join(" or ")} (got ${t})`); break; }
      case "required": if (typeOf(value) === "object") for (const r of s) if (!Object.hasOwn(value, r)) err(`missing required property ${r}`); break;
      case "properties": if (typeOf(value) === "object") for (const [p, ps] of Object.entries(s)) if (Object.hasOwn(value, p)) errs.push(...validate(ps, value[p], `${at}/${p}`, root)); break;
      case "additionalProperties": if (s === false && typeOf(value) === "object") for (const p of Object.keys(value)) if (!Object.hasOwn(schema.properties ?? {}, p)) err(`unknown property ${p}`); break;
      case "items": if (Array.isArray(value)) value.forEach((v, n) => errs.push(...validate(s, v, `${at}/${n}`, root))); break;
      case "minItems": if (Array.isArray(value) && value.length < s) err(`needs at least ${s} items`); break;
      case "uniqueItems": if (s && Array.isArray(value) && new Set(value.map((v) => JSON.stringify(v))).size !== value.length) err("items must be unique"); break;
      case "pattern": if (typeof value === "string" && !new RegExp(s, "u").test(value)) err(`${JSON.stringify(value)} does not match ${s}`); break;
      case "minLength": if (typeof value === "string" && value.length < s) err(`must be at least ${s} characters`); break;
      case "minimum": if (typeof value === "number" && value < s) err(`${value} is below minimum ${s}`); break;
      case "maximum": if (typeof value === "number" && value > s) err(`${value} is above maximum ${s}`); break;
      case "allOf": for (const sub of s) errs.push(...validate(sub, value, at, root)); break;
      case "if": if (validate(s, value, at, root).length === 0 && schema.then) errs.push(...validate(schema.then, value, at, root)); break;
      default: throw new ToolError(`schema keyword "${k}" is not supported by check-deps.mjs`);
    }
  }
  return errs;
}

// ---------- source scan ----------

const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const Q = "[\"'`]";

function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!SKIP_DIRS.has(e.name)) walk(p, out); } else if (!TEST_FILE.test(e.name) && (TS_FILE.test(e.name) || e.name.endsWith(".sh"))) out.push(p);
  }
  return out;
}

function pathPatterns(rel) {
  const segs = rel.split("/");
  const slash = `(?:^|[/}"'\`\\s])${segs.map(esc).join("/")}(?![\\w.-])`;
  if (segs.length === 1) return { ts: new RegExp(`${Q}${esc(rel)}${Q}`), sh: new RegExp(`/${esc(rel)}(?![\\w.-])`) };
  return { ts: new RegExp(`${segs.map((s) => `${Q}${esc(s)}${Q}`).join("\\s*,\\s*")}|${slash}`), sh: new RegExp(slash) };
}

function matchersFor(node) {
  const m = [];
  for (const b of node.bins ?? []) {
    const e = esc(b);
    m.push({ kind: "cli", what: `launches \`${b}\``,
      ts: new RegExp(`\\b(?:spawn|spawnSync|execFile|execFileSync|exec|execSync|execa)\\(\\s*${Q}${e}${Q}|\\b\\w*[Bb]in\\w*\\s*[:=]\\s*${Q}${e}${Q}|\\b\\w*[Bb]in\\w*\\([^\\n]*${Q}${e}${Q}`),
      sh: new RegExp(`(?:^\\s*|[;&|({]\\s*|\\$\\(\\s*|\\b(?:command -v|exec|if|then|do|else|time|nice|timeout\\s+\\S+)\\s+)${e}(?=\\s|$|\\))`) });
  }
  for (const p of node.binPaths ?? []) m.push({ kind: "cli", what: `launches ${p}`, ...pathPatterns(p) });
  for (const p of node.db ?? []) m.push({ kind: "db", what: `opens ${p}`, ...pathPatterns(p) });
  for (const p of node.files ?? []) m.push({ kind: "file", what: `uses ${p}`, ...pathPatterns(p) });
  return m;
}

function scan(root, nodes) {
  const found = new Map();
  const targets = nodes.map((n) => ({ id: n.id, matchers: matchersFor(n) }));
  for (const from of nodes) {
    for (const dir of from.scan ?? []) {
      for (const file of walk(path.join(root, dir))) {
        const sh = file.endsWith(".sh");
        const rel = path.relative(root, file).split(path.sep).join("/");
        fs.readFileSync(file, "utf8").split("\n").forEach((text, n) => {
          const t = text.trim();
          if (sh ? t.startsWith("#") : /^(\/\/|\/\*|\*)/.test(t)) return;
          for (const to of targets) {
            if (to.id === from.id) continue;
            for (const m of to.matchers) {
              if (!(sh ? m.sh : m.ts).test(text)) continue;
              const key = `${from.id}|${to.id}|${m.kind}`;
              if (!found.has(key)) found.set(key, { from: from.id, to: to.id, kind: m.kind, where: `${rel}:${n + 1}`, what: m.what });
            }
          }
        });
      }
    }
  }
  return found;
}

// ---------- C# project references ----------

function csprojs(dir, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!SKIP_DIRS.has(e.name)) csprojs(p, out); } else if (e.name.endsWith(".csproj")) out.push(p);
  }
  return out;
}

function ownerOf(name, nodes) {
  let best = null;
  for (const n of nodes) {
    const pre = n.projectPrefix;
    if (pre && (name === pre || name.startsWith(`${pre}.`)) && (!best || pre.length > best.projectPrefix.length)) best = n;
  }
  return best;
}

// ---------- rules ----------

const tierLabel = (n) => (n.tier === undefined ? "no tier" : `tier ${n.tier}${n.sublevel ? `.${n.sublevel}` : ""}`);
const rank = (n) => n.tier * 10 + (n.sublevel ?? 0);
const edgeKey = (e) => `${e.from}|${e.to}|${e.kind}`;
const edgeText = (e) => `${e.from} -> ${e.to} (${e.kind})`;

function findCycles(edges) {
  const adj = new Map();
  for (const e of edges) { if (!adj.has(e.from)) adj.set(e.from, new Set()); adj.get(e.from).add(e.to); }
  const cycles = new Map();
  const visit = (start, node, trail) => {
    for (const next of [...(adj.get(node) ?? [])].sort()) {
      if (next === start) {
        const cyc = [...trail];
        const k = cyc.indexOf([...cyc].sort()[0]);
        const canon = [...cyc.slice(k), ...cyc.slice(0, k)];
        cycles.set(canon.join(">"), [...canon, canon[0]]);
      } else if (!trail.includes(next) && next > start) visit(start, next, [...trail, next]);
    }
  };
  for (const n of [...adj.keys()].sort()) visit(n, n, [n]);
  return [...cycles.values()];
}

export function check(root, today = new Date().toISOString().slice(0, 10)) {
  const file = path.join(root, "deps.yaml");
  if (!fs.existsSync(file)) throw new ToolError(`deps.yaml not found at ${file}`);
  const fails = [];
  const info = [];
  const fail = (rule, title, measured, limit, fix) => fails.push({ rule, title, measured, limit, fix });
  let doc;
  try { doc = parseYaml(fs.readFileSync(file, "utf8")); } catch (e) {
    if (!(e instanceof YamlError)) throw e;
    fail("DEPS-YAML-001", "deps.yaml is outside the supported YAML subset", e.message, "the YAML subset described at the top of deps.yaml", "rewrite the line in block style with plain or quoted scalars");
    return { fails, info, doc: null };
  }
  const schemaErrs = validate(JSON.parse(fs.readFileSync(SCHEMA, "utf8")), doc);
  for (const m of schemaErrs) fail("DEPS-SCHEMA-001", "deps.yaml does not match its schema", `deps.yaml ${m}`, "schemas/deps.schema.json", "fix the field named in the measured line");
  if (schemaErrs.length) return { fails, info, doc };

  const byId = new Map();
  for (const p of doc.packages) {
    if (byId.has(p.id)) fail("DEPS-REF-002", "duplicate package id", p.id, "package ids are unique", `rename one of the ${p.id} entries`);
    byId.set(p.id, p);
  }
  const edges = doc.edges.filter((e) => {
    const missing = [e.from, e.to].filter((id) => !byId.has(id));
    for (const id of missing) fail("DEPS-REF-001", "edge names an unknown package", `${edgeText(e)}: ${id} is not under packages`, "edges connect declared packages", `declare ${id} under packages, or fix the edge`);
    return missing.length === 0;
  });
  for (const x of doc.exceptions) if (!byId.has(x.subject)) fail("DEPS-REF-001", "exception names an unknown package", `${x.rule} for ${x.subject}`, "exceptions name declared packages", `fix the subject of the ${x.rule} exception`);
  const active = doc.exceptions.filter((x) => {
    if (x.expires < today) { fail("DEPS-EXC-001", "expired exception", `${x.rule} for ${x.subject} expired ${x.expires} (today ${today})`, "an exception is valid until its expires date", "fix the violation, or renew the exception in a reviewed PR with a new reason and date"); return false; }
    info.push(`exception ${x.rule} ${x.subject} (expires ${x.expires}): ${x.reason}`);
    return true;
  });
  const excepted = (rule, subject) => active.some((x) => x.rule === rule && x.subject === subject);

  for (const e of edges) {
    const a = byId.get(e.from); const b = byId.get(e.to);
    if (e.kind === "project" && (a.kind !== "package" || b.kind !== "package")) {
      fail("DEPS-KIND-001", "project edge outside the C# packages", edgeText(e), `project edges connect kind: package nodes (${e.from} is ${a.kind}, ${e.to} is ${b.kind})`, "use a cli or file edge to reach a shell or mod node");
      continue;
    }
    if (a.kind !== "package" || b.kind !== "package") continue;
    if (e.kind === "project" && rank(a) <= rank(b)) fail("DEPS-TIER-001", "project edge does not point to a lower tier", `${e.from} (${tierLabel(a)}) -> ${e.to} (${tierLabel(b)})`, "a project edge points strictly down the tiers (§4b.1a)", `call ${e.to} through its CLI (kind: cli), or move the shared code down a tier`);
    if ((e.kind === "db" || e.kind === "file") && rank(a) < rank(b)) fail("DEPS-TIER-002", `${e.kind} edge points to a higher tier`, `${e.from} (${tierLabel(a)}) -> ${e.to} (${tierLabel(b)})`, "db and file edges point down or across, never up", `ask ${e.to} through its CLI instead`);
  }
  for (const c of findCycles(edges)) fail("DEPS-CYCLE-001", "the declared graph has a cycle", c.join(" -> "), "the graph is acyclic across every edge kind (§4b.1a)", "remove one edge of the cycle, or move the shared part into a lower tier");

  const testPkg = new Set(doc.packages.filter((p) => p.test).map((p) => p.id));
  for (const p of doc.packages.filter((x) => x.kind === "package" && !x.test)) {
    const targets = [...new Set(edges.filter((e) => e.from === p.id && e.kind === "project" && !testPkg.has(e.to)).map((e) => e.to))].sort();
    if (targets.length > doc.limits.projectFanOut && !excepted("DEPS-FANOUT-001", p.id))
      fail("DEPS-FANOUT-001", "project fan-out over the limit", `${p.id} has ${targets.length} project edges (${targets.join(", ")})`, `${doc.limits.projectFanOut} project edges per package (limits.projectFanOut; test packages exempt)`, `split ${p.id}, or record a DEPS-FANOUT-001 exception with a reason and an expiry`);
  }

  const declared = new Map(edges.map((e) => [edgeKey(e), e]));
  const found = scan(root, doc.packages);
  for (const [k, h] of found) {
    if (!declared.has(k)) fail("DEPS-EDGE-001", "undeclared edge in source", `${h.from} -> ${h.to} (${h.kind}): ${h.where} ${h.what}`, "every cli, db and file edge is declared in deps.yaml", `declare it under edges (from: ${h.from}, to: ${h.to}, kind: ${h.kind}, status: current) in a reviewed PR, or remove the call`);
  }
  for (const e of edges.filter((x) => x.status === "current" && x.kind !== "project")) {
    if (!found.has(edgeKey(e))) fail("DEPS-STALE-001", "declared current edge not found in source", `${edgeText(e)}: no match under ${byId.get(e.from).scan?.join(", ") ?? "(no scan dirs)"}`, "a status: current edge exists in the source today", "set status: planned if it is still intended, or delete the edge");
  }
  info.push(`scan: ${found.size} cli/db/file edges found in source, all declared` + (fails.some((f) => f.rule === "DEPS-EDGE-001") ? " except the failures below" : ""));

  const projects = csprojs(path.join(root, "src"));
  if (projects.length === 0) info.push("project references: no C# projects yet — skipped, ok");
  else {
    for (const pj of projects) {
      const name = path.basename(pj, ".csproj");
      const owner = ownerOf(name, doc.packages);
      if (!owner) { fail("DEPS-PROJ-002", "C# project no package owns", `${path.relative(root, pj)} (${name})`, "every project name starts with a package's projectPrefix", "rename the project, or declare its package in deps.yaml"); continue; }
      for (const m of fs.readFileSync(pj, "utf8").matchAll(/<ProjectReference\s+Include="([^"]+)"/g)) {
        const ref = path.basename(m[1].replace(/\\/g, "/"), ".csproj");
        const refOwner = ownerOf(ref, doc.packages);
        if (!refOwner) { fail("DEPS-PROJ-002", "C# project no package owns", `${name} -> ${ref}`, "every project name starts with a package's projectPrefix", "rename the project, or declare its package in deps.yaml"); continue; }
        if (refOwner.id !== owner.id && !declared.has(`${owner.id}|${refOwner.id}|project`))
          fail("DEPS-PROJ-001", "undeclared ProjectReference", `${name} -> ${ref} (${owner.id} -> ${refOwner.id})`, "every cross-package ProjectReference is a declared project edge", `declare (from: ${owner.id}, to: ${refOwner.id}, kind: project) in a reviewed PR, or call ${refOwner.id} through its CLI`);
      }
    }
    info.push(`project references: checked ${projects.length} C# projects`);
  }
  return { fails, info, doc };
}

// ---------- output ----------

const ARROW = { project: "-->", cli: "-.->", db: "==>", file: "--o" };

export function mermaid(doc, baseDoc) {
  const out = ["```mermaid", "flowchart TB"];
  const groups = new Map();
  for (const p of doc.packages) { const g = p.kind === "package" ? `T${p.tier}` : "X"; if (!groups.has(g)) groups.set(g, []); groups.get(g).push(p.id); }
  for (const g of [...groups.keys()].sort()) {
    out.push(`  subgraph ${g}["${g === "X" ? "outside the tiers (shell, mod)" : `tier ${g.slice(1)}`}"]`, ...groups.get(g).map((id) => `    ${id}`), "  end");
  }
  for (const e of doc.edges) out.push(`  ${e.from} ${ARROW[e.kind]}|${e.kind}| ${e.to}`);
  out.push("```", "");
  const line = (e) => `${e.from} -> ${e.to} (${e.kind}, ${e.status})`;
  if (!baseDoc) {
    out.push(`edges (${doc.edges.length}):`, ...doc.edges.map((e) => `  ${line(e)}`));
  } else {
    const before = new Set(baseDoc.edges.map(line)); const after = new Set(doc.edges.map(line));
    const added = doc.edges.filter((e) => !before.has(line(e))); const removed = baseDoc.edges.filter((e) => !after.has(line(e)));
    out.push(`Added edges (${added.length}):`, ...added.map((e) => `+ ${line(e)}`), `Removed edges (${removed.length}):`, ...removed.map((e) => `- ${line(e)}`));
  }
  return out.join("\n");
}

function main(argv) {
  let root = path.join(HERE, ".."); let wantMermaid = false; let baseFile = null;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--root") root = argv[++i] ?? "";
    else if (a === "--mermaid") wantMermaid = true;
    else if (a === "--base-file") baseFile = argv[++i] ?? "";
    else if (a === "-h" || a === "--help") { console.log("usage: node scripts/check-deps.mjs [--root DIR] [--mermaid [--base-file FILE]]"); return 0; }
    else throw new ToolError(`unknown argument ${a}`);
  }
  const { fails, info, doc } = check(path.resolve(root));
  for (const f of fails) console.log(`FAIL ${f.rule} ${f.title}\n  measured: ${f.measured}\n  limit: ${f.limit}\n  fix: ${f.fix}`);
  for (const l of info) console.log(l);
  if (wantMermaid && doc && Array.isArray(doc.edges)) {
    const base = baseFile ? parseYaml(fs.readFileSync(baseFile, "utf8")) : null;
    console.log(`\n${mermaid(doc, base)}`);
  }
  if (fails.length) { console.log(`deps: ${fails.length} failure(s)`); return 1; }
  console.log(`deps: ok (${doc.packages.length} packages, ${doc.edges.length} edges)`);
  return 0;
}

try { process.exitCode = main(process.argv.slice(2)); } catch (e) {
  console.error(`check-deps: error: ${e instanceof ToolError || e instanceof YamlError ? e.message : e.stack}`);
  process.exitCode = 2;
}
