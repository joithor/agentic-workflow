// scripts/scaffold/lib/checks.mjs — the dotnet and repo checks the scaffolds run after writing, in the order the
// scaffold specs list them. A failed check throws a ScaffoldError naming the rule, what was measured and the fix.

import { ScaffoldError, run } from "./common.mjs";

const tail = (out, n = 40) => out.trimEnd().split("\n").slice(-n).join("\n");

export async function mustPass(root, rule, cmd, args, fix, log) {
  const r = await run(root, cmd, args, { log });
  if (r.code !== 0) throw new ScaffoldError(rule, `\`${[cmd, ...args].join(" ")}\` exited ${r.code}\n${tail(r.out)}`, fix);
  return r.out;
}

/** dotnet format's RS0016 code fix: writes PublicAPI.Unshipped.txt lines for a library project's public symbols. */
export async function publicApiFix(root, project, log) {
  return mustPass(root, "SCAFFOLD-PUBLICAPI", "dotnet", ["format", "analyzers", project, "--diagnostics", "RS0016", "--severity", "error"], "run the same command by hand and read its output", log);
}

/**
 * The public API lines the RS0016 code fix skips (source-generated members, e.g. Vogen's): read from a build of
 * the project (docs/csharp/typed-ids.md "Public ids and PublicAPI files"). Returns the sorted, unique symbols.
 */
export async function generatedPublicApi(root, project, log) {
  const r = await run(root, "dotnet", ["build", project, "-p:TreatWarningsAsErrors=false"], { log });
  const symbols = new Set();
  for (const m of r.out.matchAll(/RS0016: Symbol '(.*?)' is not part of the declared public API/g)) symbols.add(m[1]);
  return [...symbols].sort();
}

export async function build(root, log) {
  return mustPass(root, "SCAFFOLD-BUILD", "dotnet", ["build", "AgenticWorkflow.slnx", "-warnaserror"], "fix the template or the root settings it disagrees with; the generator reverted its files", log);
}

/**
 * Runs the unit lane and returns the names of the failing tests. dotnet test (Microsoft.Testing.Platform) prints
 * one "failed <name> (<time>)" line per failing test.
 */
export async function unitLaneFailures(root, log) {
  const r = await run(root, "dotnet", ["test", "--solution", "AgenticWorkflow.slnx", "--filter-trait", "Lane=unit", "--ignore-exit-code", "8"], { log });
  const failed = [...r.out.matchAll(/^\s*failed (\S+)/gm)].map((m) => m[1]);
  // The run summary's "failed: N" line (one per run; per-project summaries print "failed with N error(s)").
  const failedCount = [...r.out.matchAll(/^\s*failed: (\d+)$/gm)].reduce((sum, m) => sum + Number(m[1]), 0);
  return { code: r.code, failed: [...new Set(failed)], failedCount, out: r.out };
}

/** The lane run must fail on exactly the one red test the scaffold wrote, and nothing else. */
export async function expectOnlyRed(root, redTest, log) {
  const lane = await unitLaneFailures(root, log);
  const others = lane.failed.filter((name) => !name.endsWith(redTest));
  // The red test fails for the right reason: the parameterless stub's NotSupportedException, not a build or DI problem.
  const slice = redTest.split(".")[0].replace(/Tests$/, "");
  const sawRed = lane.failed.some((name) => name.endsWith(redTest)) && lane.out.includes(`NotSupportedException : ${slice}: make ${slice}Tests pass`);
  // Exit 2 is "some tests failed"; anything else (a crashed or aborted test host) is not the red test.
  if (others.length > 0 || !sawRed || lane.code !== 2 || lane.failedCount !== 1) {
    throw new ScaffoldError(
      "SCAFFOLD-RED",
      `expected exactly one failing test (${redTest}) and exit 2; failing: ${lane.failed.length ? lane.failed.join(", ") : "none"} (summary failed: ${lane.failedCount}, exit ${lane.code})\n${tail(lane.out)}`,
      "every other test must pass and the red test must fail with the stub's NotSupportedException",
    );
  }
  return lane;
}

export async function formatClean(root, log) {
  return mustPass(root, "SCAFFOLD-FORMAT", "dotnet", ["format", "AgenticWorkflow.slnx", "--verify-no-changes"], "the templates must already be formatted; run `dotnet format` and copy the result into the template", log);
}

export async function depsClean(root, log) {
  return mustPass(root, "SCAFFOLD-DEPS", "node", ["scripts/check-deps.mjs"], "declare the package and its edges in deps.yaml", log);
}

/** Done criterion: git status shows only the files the scaffold wrote. Returns the changed paths it didn't write. */
export async function strayChanges(journal) {
  const r = await run(journal.root, "git", ["status", "--porcelain", "--untracked-files=all"], { log: () => {} });
  if (r.code !== 0) return [];
  const touched = new Set(journal.touched());
  return r.out
    .split("\n")
    .filter(Boolean)
    .map((l) => l.slice(3).replace(/^"|"$/g, ""))
    .filter((f) => !touched.has(f));
}

/** Fails SCAFFOLD-STRAY when a path changed during the run that the scaffold didn't write and wasn't changed before. */
export async function noStrayChanges(journal, before) {
  const stray = (await strayChanges(journal)).filter((f) => !before.includes(f));
  if (stray.length) throw new ScaffoldError("SCAFFOLD-STRAY", `files changed that the scaffold didn't write: ${stray.join(", ")}`, "a command rewrote an existing file (usually a lock file or a PublicAPI file): find out why before committing");
}
