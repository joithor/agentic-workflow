import { ERRORS } from "../errors.js";

const esc = (s: string): string => s.replace(/\|/g, "\\|");

export function renderErrorsDoc(): string {
  const rows = Object.entries(ERRORS)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([code, d]) => `| \`${code}\` | ${esc(d.summary)} | ${esc(d.fix)} |`);
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
