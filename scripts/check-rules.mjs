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
import { Tree, existsCheck } from "./rules/exists.mjs";

class ToolError extends Error {}

const HERE = path.dirname(fileURLToPath(import.meta.url));
const RERUN = "node scripts/check-rules.mjs";
const INVENTORY = "rules/engineering/csharp.inventory.yaml";
const RECORDS = "rules/engineering/csharp";
const SCHEMA = "schemas/rule-record.schema.json";
const RULES_DOCS = [".agents/rules/csharp.md", ".agents/rules/csharp-testing.md"];
// The check-id families the rules docs name: analyzer, compiler, trim, NuGet and xUnit diagnostics, build-target
// ids, and the check-deps, ArchUnitNET, lane and rule-record ids.
const DOC_ID = /\b(?:CA|MA|RS|RCS|IDE|AW|CS|IL|NU|SYSLIB)[0-9]{4}\b|\bxUnit[0-9]{4}\b|\b(?:DEPS|ARCH|LANE|RULE)-[A-Z]+-[0-9]{3}\b/g;

// A review check, or an analyzer at suggestion (it never fails a build), doesn't enforce anything.
const deterministic = (c) => c.type !== "review" && !(c.type === "analyzer" && c.severity === "suggestion");

function readText(file) {
  try { return fs.readFileSync(file, "utf8"); } catch { return null; }
}

// The YAML parser is shared with check-deps and prefixes its messages with "deps.yaml:<line>:".
const yamlMessage = (e) => e.message.replace(/^deps\.yaml:/, `line `);

function inventoryErrors(inv) {
  const errs = [];
  const isObj = (v) => v !== null && typeof v === "object" && !Array.isArray(v);
  if (!isObj(inv)) return ["not a mapping"];
  if (inv.version !== 1) errs.push("version must be 1");
  if (typeof inv.spec !== "string" || !inv.spec) errs.push("missing spec (the spec file path)");
  if (!Array.isArray(inv.records)) errs.push("missing records (a list)");
  else inv.records.forEach((r, i) => {
    if (!isObj(r) || typeof r.id !== "string" || typeof r.section !== "string") errs.push(`records[${i}] needs id and section strings`);
  });
  return errs;
}

function load(root, rel) {
  const text = readText(path.join(root, rel));
  if (text === null) throw new ToolError(`can't read ${rel}`);
  return text;
}

export function check(root, { strict = false } = {}) {
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
  const invErrs = inventoryErrors(inventory);
  for (const e of invErrs) fail("RULE-INV-001", "inventory is malformed", `${INVENTORY}: ${e}`, "version 1, spec: <path>, records: a list of { id, section } strings", `fix ${INVENTORY}`);
  if (invErrs.length) return { fails, warns, records: [] };
  const specText = load(root, inventory.spec);
  const sections = new Set([...specText.matchAll(/^#{2,4} (\d+[a-z]?(?:\.\d+[a-z]?)*)[ .]/gm)].map((m) => m[1]));
  const wanted = new Map();
  for (const r of inventory.records) {
    if (wanted.has(r.id)) fail("RULE-COVER-005", "duplicate inventory id", `${r.id} (sections ${wanted.get(r.id)} and ${r.section})`, "each rule once in the inventory", `delete one of the ${r.id} lines in ${INVENTORY}`);
    else wanted.set(r.id, r.section);
  }

  const records = [];
  const tree = new Tree(root);
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
    const wantId = `${rec.kind}.csharp.${slug}`;
    if (rec.id !== wantId) fail("RULE-ID-001", "record id doesn't match its kind and file name", `${rel}: id ${rec.id}, kind ${rec.kind}`, wantId, `set id: ${wantId} (or rename the file)`);
    if (wanted.has(rec.id) && wanted.get(rec.id) !== rec.section) {
      fail("RULE-SPEC-001", "record section differs from the inventory", `${rec.id}: record says ${rec.section}, inventory says ${wanted.get(rec.id)}`, "the same section in both", `set section: "${wanted.get(rec.id)}" in ${rel} (or fix the inventory)`);
    }
    for (const c of rec.checks) {
      const { found, where } = existsCheck(tree, c);
      if (c.status === "active" && !found) fail("RULE-CHECK-001", "active check not found", `${rec.id}: ${c.type} ${c.id} (searched ${where})`, "every active check exists in the tree", `add the check, fix its id, or mark it status: pending with pendingPr`);
      if (c.status === "pending" && found && deterministic(c)) {
        const msg = `pending check now exists: ${rec.id}: ${c.type} ${c.id} (PR #${c.pendingPr})`;
        const fix = `set status: active, drop pendingPr, and update gap in ${rel}${c.note ? ` (the check's note says what it doesn't cover)` : ""}`;
        if (strict) fail("RULE-CHECK-002", "pending check now exists", msg, "--strict: no pending check that already exists", fix);
        else warns.push(`WARN RULE-CHECK-002 ${msg} — fix: ${fix}`);
      }
    }
    const active = rec.checks.filter((c) => c.status === "active" && deterministic(c));
    const pending = rec.checks.filter((c) => c.status === "pending" && deterministic(c));
    const gapErr = {
      closed: (active.length === 0 || pending.length > 0) && "closed needs an active deterministic check and no pending one (it's partial or pending until that lands)",
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
  let strict = false;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--root") {
      const v = argv[++i];
      if (!v || v.startsWith("--")) throw new ToolError("--root needs a directory");
      root = path.resolve(v);
    }
    else if (a === "--strict") strict = true;
    else if (a === "--json") json = true;
    else if (a === "-h" || a === "--help") { console.log("usage: node scripts/check-rules.mjs [--root DIR] [--json] [--strict]"); return 0; }
    else throw new ToolError(`unknown argument ${a}`);
  }
  const { fails, warns, records } = check(root, { strict });
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
