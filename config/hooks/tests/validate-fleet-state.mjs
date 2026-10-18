// Validates fleet state files against schemas/fleet-state.schema.json (Ajv2020, from scripts/node_modules).
// Usage: node validate-fleet-state.mjs <file>...   Exit 0 when every file is valid, 1 otherwise (the reasons on stderr).
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..", "..", "..");
const Ajv2020 = createRequire(path.join(root, "scripts", "package.json"))("ajv/dist/2020").default;
const validate = new Ajv2020({ allErrors: true, strict: false }).compile(JSON.parse(fs.readFileSync(path.join(root, "schemas", "fleet-state.schema.json"), "utf8")));
let bad = 0;
for (const file of process.argv.slice(2)) {
  let doc;
  try { doc = JSON.parse(fs.readFileSync(file, "utf8")); } catch (e) { console.error(`${file}: ${e.message}`); bad++; continue; }
  if (!validate(doc)) { console.error(`${file}: ${JSON.stringify(validate.errors)}`); bad++; }
}
process.exit(bad === 0 ? 0 : 1);
