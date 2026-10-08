import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { renderErrorsDoc } from "./docs/errors-doc.js";
import { renderProfileDoc, renderSchemas } from "./docs/profile-doc.js";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
function write(rel: string, text: string): void {
  const file = path.join(repoRoot, rel);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text);
  console.log(`wrote ${rel}`);
}

write("docs/sindri/errors.md", renderErrorsDoc());

const schemas = renderSchemas();
write("sindri/schema/profile.schema.json", schemas.profile);
write("sindri/schema/repo.schema.json", schemas.repo);
write("docs/sindri/profile.md", renderProfileDoc());
