import { ERRORS } from "../errors.js";
import { cell } from "./markdown.js";

export function renderErrorsDoc(): string {
  const rows = Object.entries(ERRORS)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([code, d]) => `| \`${code}\` | ${cell(d.summary)} | ${cell(d.fix)} |`);
  return [
    "# Sindri error codes",
    "",
    "Generated from `sindri/src/errors.ts` by `cd sindri && npm run gen`. Do not edit by hand.",
    "",
    "| Code | Meaning | Fix |",
    "|---|---|---|",
    ...rows,
    "",
  ].join("\n");
}
