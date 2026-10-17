#!/usr/bin/env node
// scripts/check-scoping-schemas.mjs — checks the scoping station's output schemas (scoping spec 2026-10-15, SC-Y):
//
//   node scripts/check-scoping-schemas.mjs [--root DIR]
//
// 1. Every schemas/scoping/*.json except seeds.json compiles under Ajv 8's Ajv2020.
// 2. schemas/scoping/examples/manifest.json lists every example file, each with the verdict its schema must give.
// 3. Every payload in recipes/sindri/evals/workflow.scoping and workflow.idea-to-brief whose fact has a schema (start inputs and trigger
//    outputs) satisfies it; a string for such a fact is a failure, since the facts are JSON values.
// 4. seeds.json holds exactly the facts recipes/sindri/workflow.scoping.yaml seeds at intake, and each seed that has a schema satisfies it.
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");
const DIR = "schemas/scoping";
const SEED_SCHEMAS = {
  "fact:scopeMap": "scope-map.json",
  "fact:acceptance": "acceptance.json",
  "fact:challenge": "challenge.json",
  "fact:questions": "questions.json",
  "fact:openQuestions": "open-questions.json",
};

const FACT_SCHEMAS = {
  "fact:scopeInput": "scope-input.json",
  "fact:brief": "brief.json",
  "fact:scopeMap": "scope-map.json",
  "fact:acceptance": "acceptance.json",
  "fact:questions": "questions.json",
  "fact:openQuestions": "open-questions.json",
  "fact:challenge": "challenge.json",
  "fact:challengeRound": "challenge-round.json",
  "fact:coverage": "coverage.json",
};

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);
const readJson = (rel) => JSON.parse(fs.readFileSync(path.join(ROOT, rel), "utf8"));

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-scoping-schemas: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

try {
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validators = new Map();
  const files = fs.readdirSync(path.join(ROOT, DIR)).filter((f) => f.endsWith(".json") && f !== "seeds.json");
  for (const f of files) {
    try {
      validators.set(f, ajv.compile(readJson(`${DIR}/${f}`)));
    } catch (e) {
      fail(`${DIR}/${f}`, `does not compile: ${e.message}`);
    }
  }

  // 2. examples
  const edir = `${DIR}/examples`;
  const manifest = readJson(`${edir}/manifest.json`);
  const listed = new Set();
  for (const ex of manifest.examples) {
    const where = `${edir}/${ex.file}`;
    if (listed.has(ex.file)) fail(where, "listed twice in manifest.json");
    listed.add(ex.file);
    const validate = validators.get(ex.schema);
    if (!validate) { fail(where, `names ${ex.schema}, which is not a schema here`); continue; }
    if (!fs.existsSync(path.join(ROOT, where))) { fail(where, "listed in manifest.json but missing"); continue; }
    const ok = validate(readJson(where));
    if (ok !== ex.valid) fail(where, `expected ${ex.valid ? "valid" : "invalid"} against ${ex.schema}, schema says ${ok ? "valid" : "invalid"}${ok ? "" : ` (${ajv.errorsText(validate.errors)})`}`);
  }
  for (const f of fs.readdirSync(path.join(ROOT, ...edir.split("/")))) if (f !== "manifest.json" && !listed.has(f)) fail(`${edir}/${f}`, "not listed in manifest.json");
  for (const ex of manifest.examples) if (ex.valid && !ex.source) fail(`${edir}/${ex.file}`, "a valid example names its source in the spec");
  for (const f of files) if (!manifest.examples.some((e) => e.schema === f && e.valid)) fail(`${DIR}/${f}`, "has no valid example");

  // 3. eval payloads
  for (const recipeId of ["workflow.scoping", "workflow.idea-to-brief"]) {
    const edir2 = path.join(ROOT, "recipes/sindri/evals", recipeId);
    if (!fs.existsSync(edir2)) { fail(`recipes/sindri/evals/${recipeId}`, "missing"); continue; }
    for (const f of fs.readdirSync(edir2).filter((n) => n.endsWith(".jsonl"))) {
      const where = `recipes/sindri/evals/${recipeId}/${f}`;
      fs.readFileSync(path.join(edir2, f), "utf8").split("\n").filter(Boolean).forEach((text, i) => {
        let line;
        try { line = JSON.parse(text); } catch { fail(`${where}:${i + 1}`, "not JSON"); return; }
        const payloads = Object.entries(line.start?.inputs ?? line.trigger?.outputs ?? {});
        for (const [fact, value] of payloads) {
          const validate = validators.get(FACT_SCHEMAS[fact]);
          if (validate && !validate(value)) fail(`${where}:${i + 1}`, `${fact} does not satisfy ${FACT_SCHEMAS[fact]} (${ajv.errorsText(validate.errors)})`);
        }
      });
    }
  }

  // 4. seeds
  const seeds = readJson(`${DIR}/seeds.json`);
  const recipe = fs.readFileSync(path.join(ROOT, "recipes/sindri/workflow.scoping.yaml"), "utf8");
  const intake = /from: intake, to: read-brief[^\n]*produces: \[([^\]]*)\]/.exec(recipe);
  if (!intake) fail("recipes/sindri/workflow.scoping.yaml", "no intake transition with produces found");
  else {
    const seeded = intake[1].split(",").map((s) => s.trim()).filter(Boolean).sort();
    const have = Object.keys(seeds).sort();
    if (JSON.stringify(seeded) !== JSON.stringify(have)) fail(`${DIR}/seeds.json`, `holds ${have.join(", ")}; the recipe's intake seeds ${seeded.join(", ")}`);
  }
  for (const [fact, schemaFile] of Object.entries(SEED_SCHEMAS)) {
    const validate = validators.get(schemaFile);
    if (!(fact in seeds)) fail(`${DIR}/seeds.json`, `lacks ${fact}`);
    else if (validate && !validate(seeds[fact])) fail(`${DIR}/seeds.json`, `${fact} does not satisfy ${schemaFile} (${ajv.errorsText(validate.errors)})`);
  }
} catch (e) {
  console.error(`check-scoping-schemas: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-scoping-schemas: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-scoping-schemas: ok");
