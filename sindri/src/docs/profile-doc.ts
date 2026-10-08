import { zodToJsonSchema } from "zod-to-json-schema";

import { ProfileSchema, RepoSchema } from "../profile/schema.js";
import { cell } from "./markdown.js";

export interface JsonSchemaNode {
  type?: string;
  enum?: unknown[];
  const?: unknown;
  anyOf?: JsonSchemaNode[];
  items?: JsonSchemaNode;
  properties?: Record<string, JsonSchemaNode>;
  required?: string[];
  default?: unknown;
  description?: string;
}

function toJson(schema: typeof ProfileSchema | typeof RepoSchema, title: string): JsonSchemaNode {
  return { ...(zodToJsonSchema(schema, { $refStrategy: "none" }) as JsonSchemaNode), description: title };
}

export function renderSchemas(): { profile: string; repo: string } {
  return {
    profile: `${JSON.stringify(toJson(ProfileSchema, "Sindri profile.yaml"), null, 2)}\n`,
    repo: `${JSON.stringify(toJson(RepoSchema, "Sindri repos/<name>.yaml"), null, 2)}\n`,
  };
}

function typeOf(n: JsonSchemaNode): string {
  if (n.enum !== undefined) return n.enum.map((v) => `\`${String(v)}\``).join(" \\| ");
  if (n.const !== undefined) return `\`${String(n.const)}\``;
  if (n.anyOf !== undefined) return `object (one of ${n.anyOf.length} shapes)`;
  if (n.type === "array") return `${typeOf(n.items ?? {})}[]`;
  return n.type ?? "any";
}

export function renderKeyRows(schema: JsonSchemaNode): string[] {
  const required = new Set(schema.required ?? []);
  return Object.entries(schema.properties ?? {}).map(([key, n]) => {
    const def = n.default === undefined ? "" : cell(`\`${JSON.stringify(n.default)}\``);
    return `| \`${key}\` | ${typeOf(n)} | ${required.has(key) ? "yes" : "no"} | ${def} | ${cell(n.description ?? "")} |`;
  });
}

export function renderProfileDoc(): string {
  const header = ["| Key | Type | Required | Default | Meaning |", "|---|---|---|---|---|"];
  return [
    "# Sindri profile reference",
    "",
    "Generated from `sindri/src/profile/schema.ts` by `cd sindri && npm run gen`. Do not edit by hand.",
    "Editor autocomplete: point your YAML extension at `sindri/schema/profile.schema.json` and `repo.schema.json`.",
    "",
    "Precedence: `repos/<repo>.yaml` `overrides` > `profile.yaml` > core default (`sindri profile explain <key> [--repo <name>]`).",
    "A changed profile takes effect only after `sindri profile approve <hash>` (spec §8.7).",
    "",
    "## profile.yaml",
    "",
    ...header,
    ...renderKeyRows(toJson(ProfileSchema, "")),
    "",
    "## `repos/<name>.yaml`",
    "",
    ...header,
    ...renderKeyRows(toJson(RepoSchema, "")),
    "",
  ].join("\n");
}
