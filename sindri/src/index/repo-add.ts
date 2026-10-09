import fs from "node:fs";
import path from "node:path";
import YAML from "yaml";

import { parseFlags } from "../args.js";
import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import type { Command } from "../main.js";
import { failure, fromError, success } from "../output.js";
import { requireProfile, sanitizeName } from "../profile/commands.js";
import { PROFILE_SCHEMA_VERSION } from "../profile/schema.js";

// Edits the LIVE profile; the change takes effect after `sindri profile approve` (spec §8.7).
// The mirror is not created here: every full `sindri index build` creates or refreshes it.
export async function repoAdd(deps: Deps, target: string, name?: string): Promise<{ name: string; path: string; added: boolean }> {
  const loaded = requireProfile(deps);
  const top = await deps.git.run(["rev-parse", "--show-toplevel"], path.resolve(deps.cwd, target));
  if (!top.ok) throw new SindriError("SND-PROFILE-009", `${target} is not inside a git repo`);
  const repoPath = fs.realpathSync(top.stdout.trim());
  const repoName = sanitizeName(name ?? path.basename(repoPath));
  // --name becomes a file name and a directory name: it must already be what sanitizing makes of it.
  if (name !== undefined && repoName !== name) {
    throw new SindriError("SND-PROFILE-014", `repo name ${name} must be lowercase letters, digits and dashes (max 39)`, { fix: `use --name ${repoName}` });
  }
  const existing = loaded.repos[repoName];
  if (existing !== undefined) {
    if (fs.realpathSync(existing.path) !== repoPath) throw new SindriError("SND-PROFILE-013", `repo name ${repoName} is already used for ${existing.path}`, { fix: "pass --name <another name>" });
    return { name: repoName, path: repoPath, added: false };
  }
  // The loader already refuses a repos/<name>.yaml that profile.yaml doesn't list; "wx" still never overwrites one.
  const repoFile = path.join(loaded.root, "repos", `${repoName}.yaml`);
  fs.mkdirSync(path.dirname(repoFile), { recursive: true, mode: 0o700 });
  fs.writeFileSync(repoFile, YAML.stringify({ schemaVersion: PROFILE_SCHEMA_VERSION, name: repoName, path: repoPath, defaultBranch: "main", protectedPaths: [] }), { mode: 0o600, flag: "wx" });
  const file = path.join(loaded.root, "profile.yaml");
  const doc = YAML.parseDocument(fs.readFileSync(file, "utf8"));
  doc.addIn(["repos"], repoName);
  fs.writeFileSync(`${file}.tmp`, doc.toString(), { mode: 0o600 });
  fs.renameSync(`${file}.tmp`, file);
  return { name: repoName, path: repoPath, added: true };
}

export const repoCommand: Command = async (args, deps) => {
  const [sub, ...rest] = args;
  const json = rest.includes("--json");
  try {
    if (sub !== "add") return failure("SND-CLI-002", `unknown repo subcommand: ${sub ?? "(none)"}; use add`, json, { fix: "sindri repo --help" });
    const { values, positionals } = parseFlags(rest, { name: { type: "string" }, json: { type: "boolean" } });
    const target = positionals[0];
    if (target === undefined) throw new SindriError("SND-CLI-002", "repo add needs a path", { fix: "sindri repo add <path> [--name NAME]" });
    const r = await repoAdd(deps, target, values.name);
    const text = r.added
      ? `Added ${r.name} (${r.path}). The profile changed: sindri profile approve, then sindri index build --repo ${r.name} (that also mirrors it).`
      : `${r.name} is already in the profile.`;
    return success(text, r, values.json === true);
  } catch (e) {
    return fromError(e, json);
  }
};
