// scripts/scaffold/lib/checks.mjs — the dotnet and repo checks the scaffolds run after writing, in the order the
// scaffold specs list them. A failed check throws a ScaffoldError naming the rule, what was measured and the fix.

import { ScaffoldError, run } from "./common.mjs";

const tail = (out, n = 40) => out.trimEnd().split("\n").slice(-n).join("\n");

export function mustPass(root, rule, cmd, args, fix, log) {
  const r = run(root, cmd, args, { log });
  if (r.code !== 0) throw new ScaffoldError(rule, `\`${[cmd, ...args].join(" ")}\` exited ${r.code}\n${tail(r.out)}`, fix);
  return r.out;
}

/** dotnet format's RS0016 code fix: writes PublicAPI.Unshipped.txt lines for a library project's public symbols. */
export function publicApiFix(root, project, log) {
  return mustPass(root, "SCAFFOLD-PUBLICAPI", "dotnet", ["format", "analyzers", project, "--diagnostics", "RS0016", "--severity", "error"], "run the same command by hand and read its output", log);
}

/**
 * The public API lines the RS0016 code fix skips (source-generated members, e.g. Vogen's): read from a build of
 * the project (docs/csharp/typed-ids.md "Public ids and PublicAPI files"). Returns the sorted, unique symbols.
 */
export function generatedPublicApi(root, project, log) {
  const r = run(root, "dotnet", ["build", project, "-p:TreatWarningsAsErrors=false"], { log });
  const symbols = new Set();
  for (const m of r.out.matchAll(/RS0016: Symbol '(.*?)' is not part of the declared public API/g)) symbols.add(m[1]);
  return [...symbols].sort();
}

export function build(root, log) {
  return mustPass(root, "SCAFFOLD-BUILD", "dotnet", ["build", "AgenticWorkflow.slnx", "-warnaserror"], "fix the template or the root settings it disagrees with; the generator reverted its files", log);
}

/**
 * Runs the unit lane and returns the names of the failing tests. dotnet test (Microsoft.Testing.Platform) prints
 * one "failed <name> (<time>)" line per failing test.
 */
export function unitLaneFailures(root, log) {
  const r = run(root, "dotnet", ["test", "--solution", "AgenticWorkflow.slnx", "--filter-trait", "Lane=unit", "--ignore-exit-code", "8"], { log });
  const failed = [...r.out.matchAll(/^\s*failed (\S+)/gm)].map((m) => m[1]);
  const summary = r.out.match(/^\s*failed: (\d+)/gm);
  return { code: r.code, failed: [...new Set(failed)], out: r.out, summary };
}

/** The lane run must fail on exactly the one red test the scaffold wrote, and nothing else. */
export function expectOnlyRed(root, redTest, log) {
  const lane = unitLaneFailures(root, log);
  const others = lane.failed.filter((name) => !name.endsWith(redTest));
  // The red test fails for the right reason: the parameterless stub's NotSupportedException, not a build or DI problem.
  const slice = redTest.split(".")[0].replace(/Tests$/, "");
  const sawRed = lane.failed.some((name) => name.endsWith(redTest)) && lane.out.includes(`NotSupportedException : ${slice}: make ${slice}Tests pass`);
  if (others.length > 0 || !sawRed) {
    throw new ScaffoldError(
      "SCAFFOLD-RED",
      `expected exactly one failing test (${redTest}); failing: ${lane.failed.length ? lane.failed.join(", ") : "none"}\n${tail(lane.out)}`,
      "every other test must pass and the red test must fail with the stub's NotSupportedException",
    );
  }
  return lane;
}

export function formatClean(root, log) {
  return mustPass(root, "SCAFFOLD-FORMAT", "dotnet", ["format", "AgenticWorkflow.slnx", "--verify-no-changes"], "the templates must already be formatted; run `dotnet format` and copy the result into the template", log);
}

export function depsClean(root, log) {
  return mustPass(root, "SCAFFOLD-DEPS", "node", ["scripts/check-deps.mjs"], "declare the package and its edges in deps.yaml", log);
}
