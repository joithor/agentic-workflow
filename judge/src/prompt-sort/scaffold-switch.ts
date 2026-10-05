// judge/src/prompt-sort/scaffold-switch.ts
import fs from "node:fs";
import path from "node:path";

import { loadConfig, resolvePromptSort, type JudgeConfig } from "../config.js";
import type { CmdResult } from "./run.js";
import { SCAFFOLD_IDS, type ScaffoldId } from "./scaffolds.js";

export const SCAFFOLD_USAGE = `usage: judge prompt-sort scaffold <${SCAFFOLD_IDS.join("|")}> <on|off>`;

export function runScaffoldSwitch(id: string, value: string, file: string): CmdResult {
  if (!(SCAFFOLD_IDS as readonly string[]).includes(id) || (value !== "on" && value !== "off")) {
    return { exitCode: 1, stdout: "", stderr: SCAFFOLD_USAGE };
  }
  const current: JudgeConfig = fs.existsSync(file) ? loadConfig(file) : { questions: {} };
  const resolved = resolvePromptSort(current);
  const next: JudgeConfig = {
    ...current,
    promptSort: { ...current.promptSort, scaffolds: { ...resolved.scaffolds, [id as ScaffoldId]: value === "on" } },
  };
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(next, null, 2));
  return { exitCode: 0, stdout: JSON.stringify(resolvePromptSort(next).scaffolds) };
}
