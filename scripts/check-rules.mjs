#!/usr/bin/env node
// scripts/check-rules.mjs — checks the rule records (factory addendum §17.6; readiness spec §4b: "every rule
// becomes a rule record with the check named in its row").
//
//   node scripts/check-rules.mjs [--root DIR] [--json]
//
// Reads rules/engineering/csharp.inventory.yaml (the §4b rule list) and rules/engineering/csharp/*.yaml (one
// record per rule), validates each record against schemas/rule-record.schema.json, and checks that:
// every check a record calls active really exists in the tree; a pending check (in an open PR) doesn't yet
// (it warns once it does, so the record gets flipped); the gap status agrees with the checks; every inventory
// rule has a record and every record is in the inventory; every inventory section is a heading in the spec;
// and every check id the C# rules docs name is named by some record. docs/csharp/rule-records.md explains
// the format. Exit codes: 0 ok (warnings allowed), 1 a rule failed, 2 a tool error. Node builtins only.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { YamlError, parseYaml } from "./deps/yaml.mjs";
import { SchemaError, validate } from "./deps/schema.mjs";

class ToolError extends Error {}

const HERE = path.dirname(fileURLToPath(import.meta.url));
const RERUN = "node scripts/check-rules.mjs";
const INVENTORY = "rules/engineering/csharp.inventory.yaml";
const RECORDS = "rules/engineering/csharp";
const SCHEMA = "schemas/rule-record.schema.json";
const RULES_DOCS = [".agents/rules/csharp.md", ".agents/rules/csharp-testing.md"];
// The check-id families the rules docs name (analyzer, build-target, check-deps, ArchUnitNET, lane, rule ids).
const DOC_ID = /\b(?:CA|MA|RS|IDE|AW)[0-9]{4}\b|\b(?:DEPS|ARCH|LANE|RULE)-[A-Z]+-[0-9]{3}\b/g;

// Where each check type's id has to appear (relative paths; a directory is searched recursively).
const SEARCH = {
  analyzer: [".editorconfig", "tests/.editorconfig", "Directory.Build.props", "Directory.Build.targets", "CodeMetricsConfig.txt", "src/Aw.Analyzers"],
  archunit: ["tests/Aw.Architecture.Tests"],
  "check-deps": ["scripts/check-deps.mjs", "scripts/deps"],
  "lane-cap": ["tests"],
  "check-rules": ["scripts/check-rules.mjs"],
  "build-setting": ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", ".editorconfig", "nuget.config"],
};
const TEXT_EXT = /\.(cs|mjs|js|md|props|targets|txt|json|yaml|yml|editorconfig)$|(^|\/)\.editorconfig$/;
const deterministic = (c) => c.type !== "review";

function readText(file) {
  try { return fs.readFileSync(file, "utf8"); } catch { return null; }
}

function filesUnder(root, rel) {
  const abs = path.join(root, rel);
  let st;
  try { st = fs.statSync(abs); } catch { return []; }
  if (st.isFile()) return [abs];
  const out = [];
  for (const e of fs.readdirSync(abs, { withFileTypes: true })) {
    if (e.name === "bin" || e.name === "obj" || e.name === "node_modules") continue;
    const child = path.join(rel, e.name);
    if (e.isDirectory()) out.push(...filesUnder(root, child));
    else if (TEXT_EXT.test(e.name)) out.push(path.join(root, child));
  }
  return out;
}

// existsCheck(root, check) -> { found, where } — whether the check is present in the tree, and what was searched
function existsCheck(root, c, cache) {
  const textOf = (f) => (cache.has(f) ? cache.get(f) : (cache.set(f, readText(f)), cache.get(f)));
  const inFiles = (rels, needle) => rels.flatMap((r) => filesUnder(root, r)).some((f) => (textOf(f) ?? "").includes(needle));
  switch (c.type) {
    case "compiler": return { found: /^(CS|IL)[0-9]{4}$/.test(c.id), where: "the C# compiler and trim analyzers (CS/IL ids)" };
    case "banned-symbols": {
      const banned = readText(path.join(root, "BannedSymbols.txt"));
      const wired = (readText(path.join(root, "Directory.Build.props")) ?? "").includes("BannedSymbols.txt");
      const listed = new Set((banned ?? "").split("\n").map((l) => l.split(";")[0].trim()).filter(Boolean));
      const missing = (c.entries ?? []).filter((e) => !listed.has(e));
      const found = c.id === "RS0030" && banned !== null && wired && missing.length === 0;
      return { found, where: missing.length ? `BannedSymbols.txt (missing: ${missing.join(", ")})` : "BannedSymbols.txt wired in Directory.Build.props" };
    }
    case "test": case "script": return { found: fs.existsSync(path.join(root, c.ref)) && (c.type === "script" || inFiles([c.ref], c.id)), where: c.ref };
    case "ci": return { found: inFiles([c.ref], c.id), where: c.ref };
    case "review": return { found: true, where: "review" };
    default: {
      const rels = SEARCH[c.type];
      return { found: inFiles(rels, c.id), where: rels.join(", ") };
    }
  }
}

// The YAML parser is shared with check-deps and prefixes its messages with "deps.yaml:<line>:".
const yamlMessage = (e) => e.message.replace(/^deps\.yaml:/, `line `);

function load(root, rel) {
  const text = readText(path.join(root, rel));
  if (text === null) throw new ToolError(`can't read ${rel}`);
  return text;
}

export function check(root) {
  const fails = [];
  const warns = [];
  const fail = (rule, title, measured, limit, fix) => fails.push({ rule, title, measured, limit, fix });
  if (!fs.existsSync(path.join(root, RECORDS))) throw new ToolError(`no ${RECORDS}/ under ${root}`);
  const schema = JSON.parse(load(root, SCHEMA));
  let inventory;
  try { inventory = parseYaml(load(root, INVENTORY)); } catch (e) {
    if (!(e instanceof YamlError)) throw e;
    fail("RULE-YAML-001", "inventory is outside the YAML subset", `${INVENTORY}: ${yamlMessage(e)}`, "the YAML subset deps.yaml uses", "rewrite the line in plain block YAML");
    return { fails, warns, records: [] };
  }
  const specText = load(root, inventory.spec);
  const sections = new Set([...specText.matchAll(/^#{2,4} (\d+[a-z]?(?:\.\d+[a-z]?)*)[ .]/gm)].map((m) => m[1]));
  const wanted = new Map(inventory.records.map((r) => [r.id, r.section]));

  const records = [];
  const cache = new Map();
  // Ids named by records that failed to parse or validate: still counted for RULE-COVER-002, so one broken
  // record reports its own failure instead of a cascade of "no record names <id>".
  const brokenNamed = new Set();
  for (const name of fs.readdirSync(path.join(root, RECORDS)).filter((n) => n.endsWith(".yaml")).sort()) {
    const rel = `${RECORDS}/${name}`;
    let rec;
    const raw = load(root, rel);
    try { rec = parseYaml(raw); } catch (e) {
      if (!(e instanceof YamlError)) throw e;
      for (const id of raw.match(DOC_ID) ?? []) brokenNamed.add(id);
      fail("RULE-YAML-001", "record is outside the YAML subset", `${rel}: ${yamlMessage(e)}`, "the YAML subset deps.yaml uses (no flow mappings, anchors or block scalars)", "rewrite the record in plain block YAML");
      continue;
    }
    const errs = validate(schema, rec);
    if (errs.length) {
      for (const id of raw.match(DOC_ID) ?? []) brokenNamed.add(id);
      for (const e of errs) fail("RULE-SCHEMA-001", "record does not match its schema", `${rel} ${e}`, SCHEMA, "fix the field (docs/csharp/rule-records.md lists every field)");
      continue;
    }
    records.push({ rel, rec });
    const slug = name.replace(/\.yaml$/, "");
    if (!rec.id.endsWith(`.${slug}`)) fail("RULE-ID-001", "record id doesn't match its file name", `${rel}: id ${rec.id}`, `<kind>.<language>.${slug}`, `rename the file or set id: ...${slug}`);
    if (wanted.has(rec.id) && wanted.get(rec.id) !== rec.section) {
      fail("RULE-SPEC-001", "record section differs from the inventory", `${rec.id}: record says ${rec.section}, inventory says ${wanted.get(rec.id)}`, "the same section in both", `set section: "${wanted.get(rec.id)}" in ${rel} (or fix the inventory)`);
    }
    for (const c of rec.checks) {
      const { found, where } = existsCheck(root, c, cache);
      if (c.status === "active" && !found) fail("RULE-CHECK-001", "active check not found", `${rec.id}: ${c.type} ${c.id} (searched ${where})`, "every active check exists in the tree", `add the check, fix its id, or mark it status: pending with pendingPr`);
      if (c.status === "pending" && found && c.type !== "review") warns.push(`WARN RULE-CHECK-002 pending check now exists: ${rec.id}: ${c.type} ${c.id} (PR #${c.pendingPr}) — fix: set status: active, drop pendingPr, and update gap in ${rel}`);
    }
    const active = rec.checks.filter((c) => c.status === "active" && deterministic(c));
    const pending = rec.checks.filter((c) => c.status === "pending");
    const gapErr = {
      closed: active.length === 0 && "closed needs at least one active deterministic check",
      partial: (active.length === 0 || !rec.gapNote) && "partial needs an active deterministic check and a gapNote saying what isn't covered",
      pending: pending.length === 0 && "pending needs a pending check with its pendingPr",
      open: (active.length > 0 || pending.length > 0 || !rec.gapNote) && "open means no active or pending deterministic check, and needs a gapNote",
    }[rec.gap];
    if (gapErr) fail("RULE-GAP-001", "gap status disagrees with the checks", `${rec.id}: gap ${rec.gap}, ${active.length} active deterministic, ${pending.length} pending`, gapErr, `set gap to match the checks in ${rel}`);
  }

  const have = new Set(records.map((r) => r.rec.id));
  for (const [id, section] of wanted) {
    if (!have.has(id) && !fails.some((f) => f.measured.includes(`${RECORDS}/${id.split(".").pop()}.yaml`))) {
      fail("RULE-COVER-001", "inventory rule has no record", `${id} (§${section})`, "one record per inventory rule", `add ${RECORDS}/${id.split(".").pop()}.yaml`);
    }
    if (!sections.has(section)) fail("RULE-COVER-004", "inventory section isn't a heading in the spec", `${id}: §${section}`, `a heading in ${inventory.spec}`, "fix the section number");
  }
  for (const { rec, rel } of records) if (!wanted.has(rec.id)) fail("RULE-COVER-003", "record isn't in the inventory", `${rec.id} (${rel})`, `listed in ${INVENTORY}`, `add it to ${INVENTORY} with its section, or delete the record`);
  const named = new Set([...brokenNamed, ...records.flatMap(({ rec }) => [...rec.checks.map((c) => c.id), ...(rec.relatedIds ?? [])])]);
  for (const doc of RULES_DOCS) {
    const text = readText(path.join(root, doc));
    if (text === null) continue;
    for (const id of new Set(text.match(DOC_ID) ?? [])) {
      if (!named.has(id)) fail("RULE-COVER-002", "check id in the rules docs has no record", `${id} (${doc})`, "every check id the rules docs name is a record's check or relatedId", `add ${id} to the record of the rule it enforces`);
    }
  }
  return { fails, warns, records: records.map((r) => r.rec) };
}

function summary(records) {
  const gaps = { closed: 0, partial: 0, pending: 0, open: 0 };
  for (const r of records) gaps[r.gap]++;
  const pending = records.flatMap((r) => r.checks.filter((c) => c.status === "pending").map((c) => `${c.id} (PR #${c.pendingPr})`));
  return { gaps, pending };
}

function main(argv) {
  let root = path.resolve(HERE, "..");
  let json = false;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--root") root = path.resolve(argv[++i] ?? "");
    else if (a === "--json") json = true;
    else if (a === "-h" || a === "--help") { console.log("usage: node scripts/check-rules.mjs [--root DIR] [--json]"); return 0; }
    else throw new ToolError(`unknown argument ${a}`);
  }
  const { fails, warns, records } = check(root);
  const { gaps, pending } = summary(records);
  if (json) {
    console.log(JSON.stringify({ schemaVersion: 1, records: records.length, gaps, pending, warnings: warns, failures: fails }, null, 2));
    return fails.length ? 1 : 0;
  }
  for (const f of fails) console.log(`FAIL ${f.rule} ${f.title}\n  measured: ${f.measured}\n  limit: ${f.limit}\n  fix: ${f.fix}\n  rerun: ${RERUN}`);
  for (const w of warns) console.log(w);
  if (fails.length) { console.log(`rules: ${fails.length} failure(s)`); return 1; }
  console.log(`rules: ok (${records.length} records; closed ${gaps.closed}, partial ${gaps.partial}, pending ${gaps.pending}, open ${gaps.open})`);
  if (pending.length) console.log(`pending checks: ${pending.join(", ")}`);
  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try { process.exitCode = main(process.argv.slice(2)); } catch (e) {
    const known = e instanceof ToolError || e instanceof SchemaError || e instanceof SyntaxError;
    console.error(`check-rules: error: ${known ? e.message : e.stack}`);
    process.exitCode = 2;
  }
}
