import fs from "node:fs";
import path from "node:path";
import YAML from "yaml";

import { parseFlags } from "../args.js";
import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { requireProfile, sanitizeName } from "../profile/commands.js";
import { PROFILE_SCHEMA_VERSION } from "../profile/schema.js";
import { renderSteps } from "./commands.js";
import { installTemplate, nudgeLine, onboard, repoState } from "./onboard.js";

// Edits the LIVE profile; the change takes effect after `sindri profile approve` (spec §8.7).
// The mirror is not created here: every full `sindri index build` creates or refreshes it.
// From a linked worktree (its git dir differs from the common dir, <main>/.git) it adds the main
// checkout, as repoState matches it: a worktree is ephemeral, and its path breaks once it is removed.
export async function repoAdd(deps: Deps, target: string, name?: string): Promise<{ name: string; path: string; added: boolean }> {
  const loaded = requireProfile(deps);
  const r = await deps.git.run(["rev-parse", "--path-format=absolute", "--show-toplevel", "--git-dir", "--git-common-dir"], path.resolve(deps.cwd, target));
  if (!r.ok) throw new SindriError("SND-PROFILE-009", `${target} is not inside a git repo`);
  const [top, gitDir, common] = r.stdout.trim().split("\n");
  const repoPath = fs.realpathSync(gitDir === common ? top : path.dirname(common));
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
  // The loader already refuses a repos/<name>.yaml that profile.yaml doesn't list; "wx" still never
  // overwrites one, so a concurrent add of the same name loses with EEXIST.
  const repoFile = path.join(loaded.root, "repos", `${repoName}.yaml`);
  fs.mkdirSync(path.dirname(repoFile), { recursive: true, mode: 0o700 });
  try {
    fs.writeFileSync(repoFile, YAML.stringify({ schemaVersion: PROFILE_SCHEMA_VERSION, name: repoName, path: repoPath, defaultBranch: "main", protectedPaths: [] }), { mode: 0o600, flag: "wx" });
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== "EEXIST") throw e;
    throw new SindriError("SND-PROFILE-013", `${repoFile} appeared while adding ${repoName} (another repo add?)`, { fix: "rerun sindri repo add, or pass --name <another name>" });
  }
  const file = path.join(loaded.root, "profile.yaml");
  // A unique tmp name ("wx": never through a symlink); on any failure the repos file goes too,
  // since an orphan repos/<name>.yaml makes every profile command fail.
  const tmp = `${file}.tmp-${deps.system.pid}-${ulid(deps.now())}`;
  try {
    const doc = YAML.parseDocument(fs.readFileSync(file, "utf8"));
    doc.addIn(["repos"], repoName);
    fs.writeFileSync(tmp, doc.toString(), { mode: 0o600, flag: "wx" });
    fs.renameSync(tmp, file);
  } catch (e) {
    fs.rmSync(tmp, { force: true });
    fs.rmSync(repoFile, { force: true });
    throw e;
  }
  return { name: repoName, path: repoPath, added: true };
}

function statusText(s: Awaited<ReturnType<typeof repoState>>): string {
  if (s.kind === "onboarded") return `${s.name} is onboarded.`;
  if (s.kind === "outside-git") return "Not inside a git repo.";
  return nudgeLine(s) || "No approved profile yet: sindri profile init, then sindri profile approve.";
}

export const repoCommand: Command = async (args, deps) => {
  const [sub, ...rest] = args;
  const json = rest.includes("--json");
  try {
    if (sub === "onboard") {
      const { values, positionals } = parseFlags(rest, { name: { type: "string" }, "no-build": { type: "boolean" }, template: { type: "boolean" }, json: { type: "boolean" } });
      if (values.template === true) {
        if (positionals.length > 0 || values.name !== undefined) throw new SindriError("SND-CLI-002", "--template takes no path or --name", { fix: "sindri repo onboard --template" });
        const step = await installTemplate(deps);
        return success(renderSteps([step]), { steps: [step] }, values.json === true);
      }
      const r = await onboard(deps, positionals[0] ?? ".", { name: values.name, build: values["no-build"] !== true });
      return success(renderSteps(r.steps), r, values.json === true, r.exitCode);
    }
    if (sub === "status") {
      const { values, positionals } = parseFlags(rest, { nudge: { type: "boolean" }, json: { type: "boolean" } });
      if (values.nudge === true) {
        // Never fails and never blocks a session: any error is silence, and silence is no output at all
        // (success("") would print a bare newline).
        const quiet = (text: string): CommandResult => ({ exitCode: 0, stdout: text === "" ? "" : `${text}\n`, stderr: "" });
        try {
          return quiet(nudgeLine(await repoState(deps, positionals[0] ?? ".")));
        } catch {
          return quiet("");
        }
      }
      // 0 onboarded, 1 not; every error is 2 (a SindriError here whatever its own exit code, anything
      // else SND-CLI-900), so the template hook can tell "not onboarded" from "could not tell".
      try {
        const s = await repoState(deps, positionals[0] ?? ".");
        return success(statusText(s), s, values.json === true, s.kind === "onboarded" ? 0 : 1);
      } catch (e) {
        if (!(e instanceof SindriError)) throw e;
        return failure(e.code, e.message, values.json === true, { fix: e.fix, details: e.details, exitCode: 2 });
      }
    }
    if (sub !== "add") return failure("SND-CLI-002", `unknown repo subcommand: ${sub ?? "(none)"}; use add, onboard or status`, json, { fix: "sindri repo --help" });
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
