#!/usr/bin/env node
// scripts/scaffold/new-slice.mjs — the /new-slice generator (docs/csharp/scaffolds/new-slice.md).
//
//   node scripts/scaffold/new-slice.mjs --package <Pkg> --slice <Slice> --behavior <snake_case> --surface cli|http|none
//        [--command <name>] [--route "<METHOD> /path/{id}"] [--ids Name:string,Other:Guid] [--ports IItemStore,...]
//        [--root DIR] [--files-only]
//
// Adds one use case to an existing package, red: the failing test that names the behavior, the request and response
// records, a parameterless service stub that throws NotSupportedException, the validator, the registration, the CLI
// command or endpoint, ids and ports with their fakes and contract bases. Then runs the checks (under the heavy-job
// lock) and reverts every file it wrote if one fails. --files-only writes the files and skips the checks (the
// generator's own unit tests use it; never use it on a real package).
// Exit codes: 0 done (the new test is red, everything else green), 1 refused or a check failed, 2 usage error.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Journal, ScaffoldError, UsageError, camel, kebab, list, parseArgs, withHeavyLock } from "./lib/common.mjs";
import { byNamespace, usingsBlock } from "./lib/package-templates.mjs";
import { nodeOf, packageShape, readDeps } from "./lib/repo.mjs";
import * as T from "./lib/slice-templates.mjs";
import { build, expectOnlyRed, formatClean, generatedPublicApi, publicApiFix } from "./lib/checks.mjs";

const PRIMS = ["string", "Guid", "int"];
const METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];
export const KNOWN = ["package", "slice", "behavior", "surface", "command", "route", "ids", "ports", "root"];

/** Validates the inputs against the repo and returns the resolved slice (`s` in slice-templates.mjs). */
export function resolveSlice(journal, input) {
  const { package: pkg, slice, behavior, surface } = input;
  if (!pkg || !/^[A-Z][A-Za-z0-9]{2,30}$/.test(pkg)) throw new ScaffoldError("NS-PACKAGE", `'${pkg ?? ""}' is not a package name`, "pass --package <Name> of an existing src/<Name>/");
  const shape = packageShape(journal, pkg);
  const deps = readDeps(journal);
  if (!shape || !nodeOf(deps, pkg)) throw new ScaffoldError("NS-PACKAGE", `src/${pkg}/ is not a scaffolded package declared in deps.yaml`, "create it with /new-package first");
  if (!slice || !/^[A-Z][A-Za-z0-9]{2,40}$/.test(slice)) throw new ScaffoldError("NS-NAME", `'${slice ?? ""}' is not a PascalCase use case name (^[A-Z][A-Za-z0-9]{2,40}$)`, "pass a verb-first name such as CheckRepo");
  if (!behavior || !/^[a-z][a-z0-9_]{2,100}$/.test(behavior)) throw new ScaffoldError("NS-BEHAVIOR", `'${behavior ?? ""}' is not a snake_case behavior`, "pass e.g. reports_level_one_for_an_empty_repo");
  if (surface !== shape.host) throw new ScaffoldError("NS-SURFACE", `surface '${surface ?? ""}' doesn't match the package's host '${shape.host}'`, `pass --surface ${shape.host}`);

  const small = shape.shape === "small";
  const app = `src/${pkg}/Aw.${pkg}.App`;
  const featuresDir = small ? `${app}/Features/${slice}` : `src/${pkg}/Aw.${pkg}.Features/${slice}`;
  if (journal.exists(featuresDir)) throw new ScaffoldError("NS-EXISTS", `${featuresDir} already exists`, "pick another slice name, or extend the existing slice by hand");

  const coreDir = `src/${pkg}/Aw.${pkg}.Core`;
  const existingIds = new Map();
  if (journal.exists(`${coreDir}/Ids`)) {
    for (const f of fs.readdirSync(journal.abs(`${coreDir}/Ids`))) {
      const m = journal.read(`${coreDir}/Ids/${f}`).match(/\[ValueObject<(\w+)>/);
      if (m) existingIds.set(f.replace(/\.cs$/, ""), m[1]);
    }
  }
  const ids = list(input.ids).map((spec) => {
    const m = spec.match(/^([A-Z][A-Za-z0-9]{1,40}Id):(\w+)$/);
    if (!m || !PRIMS.includes(m[2])) throw new ScaffoldError("NS-IDS", `'${spec}' is not Name:primitive with a Name ending in Id and a primitive of ${PRIMS.join(", ")}`, "pass e.g. --ids ItemId:string");
    if (existingIds.has(m[1]) && existingIds.get(m[1]) !== m[2]) throw new ScaffoldError("NS-IDS", `${m[1]} already exists as a ${existingIds.get(m[1])} id`, `pass ${m[1]}:${existingIds.get(m[1])} or drop it (existing ids are reused)`);
    return { name: m[1], prim: m[2], isNew: !existingIds.has(m[1]) };
  });
  const known = new Map([...existingIds].map(([name, prim]) => [name, prim]));
  for (const id of ids) known.set(id.name, id.prim);

  let command = null;
  let method = null;
  let routePath = null;
  const routeIds = [];
  if (surface === "cli") {
    command = input.command ?? kebab(slice);
    if (!/^[a-z][a-z0-9-]{1,40}$/.test(command)) throw new ScaffoldError("NS-COMMAND", `'${command}' is not a kebab-case command name`, "pass e.g. --command check");
    const table = journal.read(small ? `${app}/Commands/CommandTable.cs` : `src/${pkg}/Aw.${pkg}.Host/Commands/CommandTable.cs`);
    const commandsDir = small ? `${app}/Commands` : `src/${pkg}/Aw.${pkg}.Host/Commands`;
    const taken = fs.readdirSync(journal.abs(commandsDir)).filter((f) => f.endsWith("Command.cs")).some((f) => journal.read(`${commandsDir}/${f}`).includes(`Name = "${command}"`));
    if (taken || table.includes(`"${command}"`)) throw new ScaffoldError("NS-COMMAND", `command '${command}' is already in the command table`, "pass another --command");
  }
  if (surface === "http") {
    const m = (input.route ?? "").match(/^([A-Z]+) (\/[A-Za-z0-9/{}._-]*)$/);
    if (!m || !METHODS.includes(m[1])) throw new ScaffoldError("NS-ROUTE", `'${input.route ?? ""}' is not "<METHOD> /path"`, 'pass e.g. --route "POST /items/{itemId}/start"');
    method = m[1];
    routePath = m[2].replace(/\{([A-Za-z][A-Za-z0-9]*)\}/g, (_, seg) => {
      const pascal = seg[0].toUpperCase() + seg.slice(1);
      const named = known.has(pascal) ? pascal : seg === "id" && ids.length === 1 ? ids[0].name : null;
      if (!named) throw new ScaffoldError("NS-ROUTE", `route segment {${seg}} names no typed id (known: ${[...known.keys()].join(", ") || "none"})`, `name it after an id ({itemId} for ItemId) and pass the id in --ids`);
      routeIds.push(named);
      return `{${camel(named)}}`;
    });
  }

  const requestIds = [...ids.map(({ name, prim }) => ({ name, prim }))];
  for (const r of routeIds) if (!requestIds.some((i) => i.name === r)) requestIds.push({ name: r, prim: known.get(r) });

  const ports = list(input.ports);
  for (const port of ports) if (!/^I[A-Z][A-Za-z0-9]{2,40}$/.test(port)) throw new ScaffoldError("NS-PORTS", `'${port}' is not an interface name`, "pass e.g. --ports IItemStore");
  if (ports.length + 1 > 5) throw new ScaffoldError("NS-DEPS", `${ports.length + 1} constructor dependencies (ports + TimeProvider); the limit is 5`, "split the use case");
  const newPorts = ports.filter((port) => !journal.exists(`${coreDir}/${port}.cs`));

  return {
    pkg,
    shape: shape.shape,
    host: shape.host,
    slice,
    behavior,
    surface,
    command,
    method,
    path: routePath,
    routeIds,
    ids,
    requestIds,
    ports,
    newPorts,
    small,
    visibility: small ? "internal" : "public",
    featuresNs: small ? `Aw.${pkg}.App.Features.${slice}` : `Aw.${pkg}.Features.${slice}`,
    commandsNs: small ? `Aw.${pkg}.App.Commands` : `Aw.${pkg}.Host.Commands`,
    testsRoot: `Aw.${pkg}.Tests`,
    testsNs: `Aw.${pkg}.Tests.${slice}`,
    dirs: {
      core: coreDir,
      features: featuresDir,
      registration: small ? `${app}/Features/${pkg}FeaturesServiceCollectionExtensions.cs` : `src/${pkg}/Aw.${pkg}.Features/${pkg}FeaturesServiceCollectionExtensions.cs`,
      commands: small ? `${app}/Commands` : `src/${pkg}/Aw.${pkg}.Host/Commands`,
      endpoints: `src/${pkg}/Aw.${pkg}.Host/Endpoints`,
      json: small ? `${app}/${pkg}JsonContext.cs` : `src/${pkg}/Aw.${pkg}.Host/${pkg}JsonContext.cs`,
      tests: `tests/Aw.${pkg}.Tests`,
      testing: `tests/Aw.${pkg}.Testing`,
      featuresProject: small ? null : `src/${pkg}/Aw.${pkg}.Features/Aw.${pkg}.Features.csproj`,
      coreProject: `${coreDir}/Aw.${pkg}.Core.csproj`,
    },
  };
}

/** Replaces a file's using block with the sorted union of its usings and `add`. */
export function addUsings(text, add) {
  const lines = text.split("\n");
  const present = lines.filter((l) => /^using [A-Za-z0-9_.]+;$/.test(l)).map((l) => l.slice(6, -1));
  const merged = [...new Set([...present, ...add])].sort(byNamespace);
  const rest = lines.filter((l) => !/^using [A-Za-z0-9_.]+;$/.test(l));
  while (rest.length && rest[0] === "") rest.shift();
  return `${usingsBlock(merged)}\n\n${rest.join("\n")}`;
}

/** Writes the slice's files and edits (no checks). */
export function writeSlice(journal, s) {
  const d = s.dirs;
  journal.create(`${d.features}/${s.slice}Request.cs`, T.request(s));
  journal.create(`${d.features}/${s.slice}Response.cs`, T.response(s));
  journal.create(`${d.features}/${s.slice}Service.cs`, T.service(s));
  journal.create(`${d.features}/${s.slice}Validator.cs`, T.validator(s));

  journal.edit(d.registration, (text) => addUsings(text.replace(/^        return services;$/m, `${T.registration(s)}        return services;`), ["Aw.Kernel.Services", s.featuresNs]));

  for (const id of s.ids.filter((i) => i.isNew)) {
    journal.create(`${d.core}/Ids/${id.name}.cs`, T.typedId(s, id));
    journal.create(`${d.tests}/Ids/${id.name}Tests.cs`, T.idTests(s, id));
    journal.create(`${d.tests}/Ids/${id.name}Holder.cs`, T.idHolder(s, id));
    const ctx = `${d.tests}/Ids/IdsJsonContext.cs`;
    if (journal.exists(ctx)) journal.edit(ctx, (text) => text.replace(/(\ninternal sealed partial class IdsJsonContext)/, `\n[JsonSerializable(typeof(${id.name}Holder))]$1`));
    else journal.create(ctx, T.idsJsonContext(s, [`${id.name}Holder`]));
  }
  for (const port of s.newPorts) {
    journal.create(`${d.core}/${port}.cs`, T.port(s, port));
    journal.create(`${d.testing}/InMemory${port.slice(1)}.cs`, T.fake(s, port));
    journal.create(`${d.testing}/${port.slice(1)}Contract.cs`, T.contract(s, port));
  }

  const jsonTypes = [`${s.slice}Request`, `${s.slice}Response`];
  const jsonUsings = [s.featuresNs];
  if (s.surface === "cli") {
    journal.create(`${d.commands}/${s.slice}Command.cs`, T.command(s));
    journal.edit(`${d.commands}/CommandTable.cs`, (text) => text.replace(/^(\s*_ => CommandOutput\.Usage)/m, `${T.commandArm(s)}$1`));
  }
  if (s.surface === "http") {
    journal.create(`${d.endpoints}/${s.slice}Endpoint.cs`, T.endpoint(s));
    if (T.hasBody(s)) {
      journal.create(`${d.endpoints}/${s.slice}Body.cs`, T.body(s));
      jsonTypes.push(`${s.slice}Body`);
      jsonUsings.push(`Aw.${s.pkg}.Host.Endpoints`);
    }
    journal.edit(`${d.endpoints}/EndpointTable.cs`, (text) => text.replace(/(_ = app\.MapGroup\("[^"]*"\)[^;]*);/, `$1${T.endpointCall(s)};`));
    for (const id of s.routeIds) {
      const rel = `${d.tests}/Ids/${id}RouteBindingTests.cs`;
      if (!journal.exists(rel)) journal.create(rel, T.routeBindingTests(s, { name: id, prim: s.requestIds.find((r) => r.name === id).prim }));
    }
  }
  if (s.surface !== "none") {
    journal.edit(d.json, (text) => addUsings(text.replace(/(\n(?:internal|public) sealed partial class)/, `\n${jsonTypes.map((t) => `[JsonSerializable(typeof(${t}))]`).join("\n")}$1`), jsonUsings));
  }

  journal.create(`${d.tests}/${s.slice}/${s.slice}Tests.cs`, T.redTest(s));
  journal.create(`${d.testing}/${s.slice}RequestBuilder.cs`, T.requestBuilder(s));
  journal.edit(`${d.testing}/A.cs`, (text) => text.replace(/\{\n\}\n?$|\n\}\n?$/, (end) => (end.startsWith("{") ? `{\n${T.builderFactory(s)}}\n` : `\n\n${T.builderFactory(s)}}\n`)));
}

/** The public API steps (before the build): the RS0016 code fix, then Vogen's generated members from the build. */
export function sliceApi(journal, s, log) {
  const root = journal.root;
  if (s.ids.some((i) => i.isNew) || s.newPorts.length > 0) {
    const api = `${s.dirs.core}/PublicAPI.Unshipped.txt`;
    publicApiFix(root, s.dirs.coreProject, log);
    const generated = generatedPublicApi(root, s.dirs.coreProject, log);
    if (generated.length) journal.edit(api, (text) => `${text.replace(/\n*$/, "\n")}${generated.join("\n")}\n`);
  }
  if (s.dirs.featuresProject) publicApiFix(root, s.dirs.featuresProject, log);
}

export const nextSteps = (s) =>
  [
    `Next (make ${s.slice}Tests pass):`,
    `  1. replace the Assert.Fail in tests/Aw.${s.pkg}.Tests/${s.slice}/${s.slice}Tests.cs with the real assertion`,
    `  2. add the ports to ${s.slice}Service's constructor: ${[...s.ports, "TimeProvider clock"].join(", ")} (fakes in tests/Aw.${s.pkg}.Testing)`,
    `  3. implement ${s.slice}Service; pure decisions go on the domain type in Core, not in the service`,
    "  4. add an integration test if the slice touches SQL",
    '  5. run the lane: dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8',
  ].join("\n");

function main(argv) {
  let input;
  try {
    input = parseArgs(argv, KNOWN, ["filesOnly"]);
  } catch (e) {
    if (e instanceof UsageError) {
      console.error(`new-slice: ${e.message}`);
      return 2;
    }
    throw e;
  }
  const root = path.resolve(input.root ?? process.cwd());
  const journal = new Journal(root);
  const log = (line) => console.error(line);
  try {
    const s = resolveSlice(journal, input);
    writeSlice(journal, s);
    if (!input.filesOnly) {
      withHeavyLock(() => {
        sliceApi(journal, s, log);
        build(root, log);
        expectOnlyRed(root, `${s.slice}Tests.${s.slice}_${s.behavior}`, log);
        formatClean(root, log);
      });
    }
    console.log(`new-slice: wrote ${journal.touched().length} files for ${s.pkg}.${s.slice}`);
    for (const f of journal.touched()) console.log(`  ${f}`);
    console.log(`Commit: test: red ${s.slice} test (${s.behavior})`);
    console.log(nextSteps(s));
    return 0;
  } catch (e) {
    journal.revert();
    if (e instanceof ScaffoldError) {
      console.error(e.message);
      if (e.fix) console.error(`fix: ${e.fix}`);
      if (journal.touched().length) console.error(`reverted ${journal.touched().length} files`);
      return 1;
    }
    throw e;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = main(process.argv.slice(2));
