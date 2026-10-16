#!/usr/bin/env node
// scripts/check-fleet.mjs — checks the fleet state file contract (spec 2026-10-16-sindri-session-collector-design.md §5.1, CO-0):
//
//   node scripts/check-fleet.mjs [--root DIR]
//
// 1. schemas/fleet-state.schema.json compiles under Ajv 8's Ajv2020.
// 2. Every .json file under tests/fixtures/fleet/ (except manifest.json) is listed in manifest.json and has the expected
//    schema verdict; a file that does not parse is invalid. A valid fixture sits in the directory named by its own
//    `provider` (the AgentId, so claude-code/, never claude/).
// 3. tests/fixtures/sessions/ (the /proc-shaped tree): manifest.json lists each process directory, every listed
//    directory exists with its stat, cmdline, environ and status files, and no process directory is unlisted.
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);
const readJson = (rel) => JSON.parse(fs.readFileSync(path.join(ROOT, rel), "utf8"));

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-fleet: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

/** Every .json file under dir (relative paths, forward slashes), manifest.json at the top excluded. */
function jsonFiles(dir) {
  const out = [];
  const walk = (rel) => {
    for (const e of fs.readdirSync(path.join(ROOT, dir, rel), { withFileTypes: true })) {
      const r = rel ? `${rel}/${e.name}` : e.name;
      if (e.isDirectory()) walk(r);
      else if (e.name.endsWith(".json") && r !== "manifest.json") out.push(r);
    }
  };
  walk("");
  return out;
}

try {
  const schema = readJson("schemas/fleet-state.schema.json");
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validate = ajv.compile(schema);

  // 2. fleet fixtures
  const dir = "tests/fixtures/fleet";
  const manifest = readJson(`${dir}/manifest.json`);
  const listed = new Set();
  for (const fx of manifest.fixtures) {
    const where = `${dir}/${fx.file}`;
    if (listed.has(fx.file)) fail(where, "listed twice in manifest.json");
    listed.add(fx.file);
    if (!["valid", "invalid"].includes(fx.schema)) fail(where, `schema tag must be valid or invalid, not ${fx.schema}`);
    if (!fs.existsSync(path.join(ROOT, where))) { fail(where, "listed in manifest.json but missing"); continue; }
    let verdict;
    let doc;
    try {
      doc = JSON.parse(fs.readFileSync(path.join(ROOT, where), "utf8"));
      verdict = validate(doc) ? "valid" : "invalid";
    } catch {
      verdict = "invalid";
    }
    if (verdict !== fx.schema) fail(where, `expected ${fx.schema}, schema says ${verdict}${verdict === "invalid" && validate.errors ? ` (${ajv.errorsText(validate.errors)})` : ""}`);
    if (verdict === "valid" && fx.schema === "valid" && fx.file.split("/")[0] !== doc.provider) fail(where, `a valid fixture lives in the directory named by its provider (${doc.provider})`);
  }
  for (const f of jsonFiles(dir)) if (!listed.has(f)) fail(`${dir}/${f}`, "not listed in manifest.json");

  // 3. the /proc-shaped sessions tree
  const sdir = "tests/fixtures/sessions";
  const sm = readJson(`${sdir}/manifest.json`);
  const procs = new Set();
  for (const p of sm.processes ?? []) {
    const where = `${sdir}/proc/${p.pid}`;
    if (procs.has(p.pid)) fail(where, "listed twice in manifest.json");
    procs.add(p.pid);
    for (const f of ["stat", "cmdline", "environ", "status"]) {
      if (!fs.existsSync(path.join(ROOT, where, f))) fail(where, `no ${f} file`);
    }
    if (!p.role) fail(where, "no role in manifest.json");
  }
  const procDir = path.join(ROOT, sdir, "proc");
  for (const e of fs.existsSync(procDir) ? fs.readdirSync(procDir) : []) {
    if (/^[0-9]+$/.test(e) && !procs.has(Number(e))) fail(`${sdir}/proc/${e}`, "not listed in manifest.json");
  }
  if (!sm.processes?.length) fail(`${sdir}/manifest.json`, "lists no processes");
} catch (e) {
  console.error(`check-fleet: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-fleet: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-fleet: ok");
