// Unit table for the deps.yaml parser and schema validator in scripts/check-deps.mjs.
// Run by scripts/tests/check-deps.test.sh; prints "<n> passed" and exits 1 on any failure.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseYaml, validate, SUPPORTED_KEYWORDS } from "../check-deps.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SCHEMA = JSON.parse(fs.readFileSync(path.join(HERE, "..", "..", "schemas", "deps.schema.json"), "utf8"));
let passed = 0;
const failures = [];
const t = (name, fn) => { try { fn(); passed++; } catch (e) { failures.push(`${name}: ${e.message}`); } };
const yamlError = (text, re) => assert.throws(() => parseYaml(text), (e) => e.constructor.name === "YamlError" && re.test(e.message));

// ---- parser ----
t("CRLF line endings", () => assert.deepEqual(parseYaml("a: 1\r\nb: x\r\n"), { a: 1, b: "x" }));
t("# inside quotes is not a comment", () => assert.deepEqual(parseYaml("a: 'x # y'\nb: \"p # q\" # c\n"), { a: "x # y", b: "p # q" }));
t("'' escape in single quotes", () => assert.deepEqual(parseYaml("a: 'it''s'\n"), { a: "it's" }));
t("flow sequence with a quoted comma", () => assert.deepEqual(parseYaml("a: [x, 'y, z', \"w\"]\n"), { a: ["x", "y, z", "w"] }));
t("empty flow sequence", () => assert.deepEqual(parseYaml("a: []\n"), { a: [] }));
t("empty value is null", () => assert.deepEqual(parseYaml("a:\nb: 1\n"), { a: null, b: 1 }));
t("sequence of maps with extra indent", () => assert.deepEqual(parseYaml("items:\n    - a: 1\n      b: 2\n    - a: 3\n"), { items: [{ a: 1, b: 2 }, { a: 3 }] }));
t("nested sequence", () => assert.deepEqual(parseYaml("a:\n  -\n    - 1\n    - 2\n"), { a: [[1, 2]] }));
t("plain scalar with a quote mid-value: ' #' still starts a comment", () => assert.deepEqual(parseYaml("note: say \"hi # there\"\n"), { note: "say \"hi" }));
t("tab indentation is rejected", () => yamlError("a:\n\t- 1\n", /tab indentation/));
t("block scalar is rejected", () => yamlError("a: |\n  x\n", /block scalar/));
t("multiple documents are rejected", () => yamlError("---\na: 1\n", /multiple documents/));
t("flow mapping is rejected", () => yamlError("a: {b: 1}\n", /flow mapping/));
t("JSON-invalid escape is a located YamlError, not a SyntaxError", () => yamlError("a: \"x\\e\"\n", /deps\.yaml:1: unsupported escape/));
t("integer beyond 2^53 is rejected", () => yamlError("a: 9007199254740993\n", /integer out of range/));
t("__proto__ is kept as an own key", () => { const d = parseYaml("__proto__: 1\n"); assert.equal(Object.hasOwn(d, "__proto__"), true); assert.equal(Object.getPrototypeOf(d), null); });
t("duplicate key is rejected", () => yamlError("a: 1\na: 2\n", /duplicate key/));

// ---- validator ----
const relPaths = { $ref: "#/$defs/relPaths" };
const v = (schema, value) => validate(schema, value, "", SCHEMA);
t("uniqueItems", () => assert.match(v(relPaths, ["a", "a"]).join(), /unique/));
t("relPath rejects a leading /", () => assert.match(v(relPaths, ["/abs"]).join(), /does not match/));
t("relPath rejects whitespace", () => assert.match(v(relPaths, ["a b"]).join(), /does not match/));
t("minItems", () => assert.match(v({ type: "array", minItems: 1 }, []).join(), /at least 1/));
t("wrong type", () => assert.match(v({ type: "integer" }, "3").join(), /must be integer \(got string\)/));
t("additionalProperties as a sub-schema validates the extras", () => assert.match(v({ type: "object", properties: {}, additionalProperties: { type: "integer" } }, { x: "s" }).join(), /\/x: must be integer/));
t("additionalProperties: true allows extras", () => assert.deepEqual(v({ type: "object", additionalProperties: true }, { x: 1 }), []));
t("const compares structurally", () => assert.deepEqual(v({ const: [1, { a: 2 }] }, [1, { a: 2 }]), []));
t("enum compares structurally", () => assert.match(v({ enum: [[1]] }, [2]).join(), /must be one of/));
t("not", () => assert.match(v({ not: { required: ["x"] } }, { x: 1 }).join(), /must not match/));
t("anyOf", () => assert.deepEqual(v({ anyOf: [{ type: "string" }, { type: "integer" }] }, 3), []));
t("unsupported keyword is a tool error", () => assert.throws(() => v({ oneOf: [] }, 1), /not supported/));

// ---- schema drift: every keyword the schema uses must be one the validator implements ----
t("the schema uses only supported keywords", () => {
  const ANNOTATIONS = new Set(["$schema", "$id", "$defs", "title", "description"]);
  const seen = new Set();
  const walk = (s, nameMap = false) => {
    if (Array.isArray(s)) return s.forEach((x) => walk(x));
    if (!s || typeof s !== "object") return;
    for (const [k, x] of Object.entries(s)) {
      if (nameMap) { walk(x); continue; }
      if (!ANNOTATIONS.has(k)) seen.add(k);
      if (k === "properties" || k === "$defs") walk(x, true);
      else if (typeof x === "object") walk(x);
    }
  };
  walk(SCHEMA);
  const missing = [...seen].filter((k) => !SUPPORTED_KEYWORDS.has(k));
  assert.deepEqual(missing, []);
});
t("deps.yaml passes its schema", () => {
  const doc = parseYaml(fs.readFileSync(path.join(HERE, "..", "..", "deps.yaml"), "utf8"));
  assert.deepEqual(validate(SCHEMA, doc), []);
});

if (failures.length) { console.log(failures.map((f) => `FAIL ${f}`).join("\n")); console.log(`${passed} passed, ${failures.length} failed`); process.exitCode = 1; }
else console.log(`${passed} passed`);
