import { createRequire } from "node:module";

import type { Deps } from "./deps.js";
import { doctorCommand } from "./doctor/doctor.js";
import { makeIndexCommand } from "./index/commands.js";
import { realIndexIo } from "./index/sandbox-real.js";
import { makeShapeCommand } from "./index/shape.js";
import { ledgerCommand, observeCommand } from "./observe/observe.js";
import { profileCommand } from "./profile/commands.js";
import { failure, success, type CommandResult } from "./output.js";
import { scrubCommand } from "./scrub/commands.js";
import { makeScrubber } from "./scrub/scrub.js";

export type Command = (args: string[], deps: Deps) => Promise<CommandResult>;

export interface CommandDef {
  summary: string;
  usage: string;
  run: Command;
}

export const COMMANDS: Record<string, CommandDef> = {
  profile: {
    summary: "init | validate | explain <key> | migrate | approve [hash]",
    usage: [
      "Usage:",
      "  sindri profile init [--ring0 [--plans <pattern>]...] [--dir DIR] [--force]",
      "  sindri profile validate [--profile DIR] [--json]",
      "  sindri profile explain <key> [--repo NAME] [--profile DIR] [--json]",
      "  sindri profile migrate [--dry-run] [--profile DIR] [--json]",
      "  sindri profile approve [<hash>] [--profile DIR] [--json]   (approving needs an interactive terminal)",
    ].join("\n"),
    run: profileCommand,
  },
  observe: {
    summary: "List the backlog with sizes and what would start; record it when approved",
    usage: "Usage: sindri observe [--no-record] [--profile DIR] [--json]",
    run: observeCommand,
  },
  scrub: {
    summary: "Scrub stdin, check staged changes, or install the secret-scan pre-commit hook",
    usage: "Usage: sindri scrub [--json] < text | sindri scrub --staged [--json] | sindri scrub --install-pre-commit [--repo PATH]",
    run: scrubCommand,
  },
  doctor: { summary: "Health checks, one line each: ok / warn / fail plus a fix", usage: "Usage: sindri doctor [--json]", run: doctorCommand },
  index: {
    summary: "Build and inspect the per-repo code index",
    usage: [
      "Usage:",
      "  sindri index build [--repo NAME] [--quick] [--full] [--json]",
      "  sindri index status [--repo NAME] [--json]   (exit 1 when an index is missing or stale)",
      "  sindri index query <name> [--repo NAME] [--json]",
      "  sindri index setup [--dry-run] [--json]",
    ].join("\n"),
    run: makeIndexCommand(realIndexIo()),
  },
  shape: {
    summary: "Record shape signals for staged changes (pre-commit), or report them",
    usage: [
      "Usage:",
      "  sindri shape --record --staged [--repo NAME] [--size XS|S|M|L|XL]   (always exits 0)",
      "  sindri shape report [--recent N] [--json]",
    ].join("\n"),
    run: makeShapeCommand(realIndexIo()),
  },
  ledger: { summary: "Show ledger events", usage: "Usage: sindri ledger [--item ID] [--since 7d|12h] [--json]", run: ledgerCommand },
};

function version(): string {
  const require = createRequire(import.meta.url);
  return (require("../package.json") as { version: string }).version;
}

function help(): string {
  const rows = Object.entries(COMMANDS)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([name, c]) => `  ${name.padEnd(10)} ${c.summary}`);
  return ["Usage: sindri <command> [flags]", "", "Commands:", "  help       Show this list", ...rows, "", "Every read command takes --json."].join("\n");
}

export async function runCli(argv: string[], deps: Deps): Promise<CommandResult> {
  const [name, ...rest] = argv;
  const json = rest.includes("--json");
  if (name === undefined || name === "help" || name === "--help") return success(help(), null, false);
  if (name === "--version") return success(version(), null, false);
  if (!Object.hasOwn(COMMANDS, name)) return failure("SND-CLI-001", `unknown command: ${name}`, json);
  const command = COMMANDS[name];
  if (rest.includes("--help") || rest.includes("-h")) return success(command.usage, null, false);
  try {
    return await command.run(rest, deps);
  } catch (e) {
    // Commands render SindriErrors themselves; anything reaching here is a bug.
    // Invariant 8: no secrets in output (launchd logs stderr). Built-in patterns
    // only: the profile may be what broke.
    const { scrub } = makeScrubber();
    const message = scrub(e instanceof Error ? e.message : String(e)).text;
    const details = deps.env.SINDRI_DEBUG === "1" && e instanceof Error ? scrub(e.stack ?? "").text.split("\n") : [];
    return failure("SND-CLI-900", `unexpected error: ${message}`, json, { details });
  }
}
