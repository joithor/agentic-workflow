// scripts/deps/schema.mjs — a JSON Schema validator for the keywords schemas/deps.schema.json uses.
// Any other keyword is a tool error, so the schema can't quietly rely on a keyword this file skips
// (scripts/tests/check-deps.unit.mjs also walks the schema and fails on an unsupported keyword).

export class SchemaError extends Error {}

const ANNOTATIONS = new Set(["$schema", "$id", "$defs", "title", "description"]);
export const SUPPORTED_KEYWORDS = new Set([
  "$ref", "const", "enum", "type", "required", "properties", "additionalProperties", "items", "minItems",
  "uniqueItems", "pattern", "minLength", "minimum", "maximum", "allOf", "anyOf", "not", "if", "then",
]);

const typeOf = (v) => (v === null ? "null" : Array.isArray(v) ? "array" : Number.isInteger(v) ? "integer" : typeof v);
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const isObj = (v) => typeOf(v) === "object";

export function validate(schema, value, at = "", root = schema) {
  if (schema === true) return [];
  if (schema === false) return [`${at || "/"}: not allowed`];
  const errs = [];
  const err = (msg) => errs.push(`${at || "/"}: ${msg}`);
  const sub = (s, v, p = at) => validate(s, v, p, root);
  for (const [k, s] of Object.entries(schema)) {
    if (ANNOTATIONS.has(k) || k === "then") continue;
    switch (k) {
      case "$ref": errs.push(...sub(s.replace(/^#\//, "").split("/").reduce((o, p) => o[p], root), value)); break;
      case "const": if (!same(value, s)) err(`must be ${JSON.stringify(s)}`); break;
      case "enum": if (!s.some((x) => same(x, value))) err(`must be one of ${s.map((x) => JSON.stringify(x)).join(", ")} (got ${JSON.stringify(value)})`); break;
      case "type": {
        const t = typeOf(value);
        if (![s].flat().some((x) => x === t || (x === "number" && t === "integer"))) err(`must be ${[s].flat().join(" or ")} (got ${t})`);
        break;
      }
      case "required": if (isObj(value)) for (const r of s) if (!Object.hasOwn(value, r)) err(`missing required property ${r}`); break;
      case "properties": if (isObj(value)) for (const [p, ps] of Object.entries(s)) if (Object.hasOwn(value, p)) errs.push(...sub(ps, value[p], `${at}/${p}`)); break;
      case "additionalProperties":
        if (isObj(value)) {
          for (const p of Object.keys(value).filter((x) => !Object.hasOwn(schema.properties ?? {}, x))) {
            if (s === false) err(`unknown property ${p}`);
            else errs.push(...sub(s, value[p], `${at}/${p}`));
          }
        }
        break;
      case "items": if (Array.isArray(value)) value.forEach((v, n) => errs.push(...sub(s, v, `${at}/${n}`))); break;
      case "minItems": if (Array.isArray(value) && value.length < s) err(`needs at least ${s} items`); break;
      case "uniqueItems": if (s && Array.isArray(value) && new Set(value.map((v) => JSON.stringify(v))).size !== value.length) err("items must be unique"); break;
      case "pattern": if (typeof value === "string" && !new RegExp(s, "u").test(value)) err(`${JSON.stringify(value)} does not match ${s}`); break;
      case "minLength": if (typeof value === "string" && value.length < s) err(`must be at least ${s} characters`); break;
      case "minimum": if (typeof value === "number" && value < s) err(`${value} is below minimum ${s}`); break;
      case "maximum": if (typeof value === "number" && value > s) err(`${value} is above maximum ${s}`); break;
      case "allOf": for (const x of s) errs.push(...sub(x, value)); break;
      case "anyOf": if (!s.some((x) => sub(x, value).length === 0)) err("must match at least one of anyOf"); break;
      case "not": if (sub(s, value).length === 0) err(`must not match ${JSON.stringify(s)}`); break;
      case "if": if (sub(s, value).length === 0 && schema.then) errs.push(...sub(schema.then, value)); break;
      default: throw new SchemaError(`schema keyword "${k}" is not supported by check-deps.mjs`);
    }
  }
  return errs;
}
