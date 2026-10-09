// Dependency layer (spec §6.2): a new dependency whose purpose overlaps an
// existing one is a reinvention signal. Tags cover common overlaps; the
// eval loop can extend the map through a reuse-hint proposal (spec §7.4).
export const PURPOSE_TAGS: Record<string, readonly string[]> = {
  date: ["moment", "dayjs", "date-fns", "luxon"],
  http: ["axios", "got", "node-fetch", "ky", "superagent", "undici"],
  id: ["uuid", "nanoid", "ulid", "cuid", "cuid2"],
  validation: ["zod", "yup", "joi", "ajv", "valibot", "io-ts", "superstruct"],
  utility: ["lodash", "underscore", "ramda", "remeda", "lodash-es"],
  test: ["jest", "vitest", "mocha", "ava", "jasmine"],
  yaml: ["yaml", "js-yaml"],
  "cli-args": ["commander", "yargs", "minimist", "meow", "cac"],
  sqlite: ["better-sqlite3", "sqlite3", "sql.js"],
  logging: ["winston", "pino", "bunyan", "loglevel"],
  "deep-equal": ["fast-deep-equal", "deep-equal", "dequal"],
  glob: ["glob", "fast-glob", "globby", "minimatch", "micromatch", "picomatch"],
};

export interface DepRow {
  manifest: string;
  name: string;
  version: string;
  kind: "prod" | "dev" | "peer";
  tags: string[];
}

export function tagsFor(name: string): string[] {
  return Object.entries(PURPOSE_TAGS)
    .filter(([, names]) => names.includes(name))
    .map(([tag]) => tag);
}

const SECTIONS: readonly [string, DepRow["kind"]][] = [["dependencies", "prod"], ["devDependencies", "dev"], ["peerDependencies", "peer"]];

export function readManifestDeps(manifest: string, text: string): DepRow[] {
  let json: unknown;
  try {
    json = JSON.parse(text);
  } catch {
    return [];
  }
  if (json === null || typeof json !== "object" || Array.isArray(json)) return [];
  const out: DepRow[] = [];
  for (const [section, kind] of SECTIONS) {
    const entries = (json as Record<string, unknown>)[section];
    if (entries === null || typeof entries !== "object") continue;
    for (const [name, version] of Object.entries(entries as Record<string, unknown>)) {
      if (typeof version === "string") out.push({ manifest, name, version, kind, tags: tagsFor(name) });
    }
  }
  return out;
}
