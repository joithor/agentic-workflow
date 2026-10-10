// Unit table for the C# scaffold generators (scripts/scaffold/new-package.mjs, new-slice.mjs). No dotnet: each case
// copies the repo files the generators read into a temp tree and runs them with --files-only, then checks the
// refusal (rule id, nothing written), the files written, the edits, and that a failure leaves the tree as it was.
// Run by scripts/tests/scaffold.test.sh; prints "<n> passed" and exits 1 on any failure. The dotnet half (build,
// lanes, format, check-deps on a scaffolded package) is scripts/scaffold/e2e.sh, run by CI's scaffold job.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, "..", "..");
const NEW_PACKAGE = path.join(REPO, "scripts/scaffold/new-package.mjs");
const NEW_SLICE = path.join(REPO, "scripts/scaffold/new-slice.mjs");
const SEED = ["deps.yaml", "Directory.Packages.props", "AgenticWorkflow.slnx", "tests/Aw.Architecture.Tests/Packages.cs", "tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj", "src/Aw.Kernel/KernelErrors.cs"];

let passed = 0;
const failures = [];
const t = (name, fn) => {
  try {
    fn();
    passed++;
  } catch (e) {
    failures.push(`${name}: ${e.message}`);
  }
};

const trees = [];
function tree() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-"));
  trees.push(dir);
  for (const rel of SEED) {
    fs.mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true });
    fs.copyFileSync(path.join(REPO, rel), path.join(dir, rel));
  }
  return dir;
}

function hashTree(dir) {
  const h = crypto.createHash("sha256");
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true }).sort((a, b) => (a.name < b.name ? -1 : 1))) {
      const p = path.join(d, e.name);
      h.update(path.relative(dir, p));
      if (e.isDirectory()) walk(p);
      else h.update(fs.readFileSync(p));
    }
  };
  walk(dir);
  return h.digest("hex");
}

const run = (script, dir, args) => {
  const r = spawnSync("node", [script, "--root", dir, "--files-only", ...args], { encoding: "utf8", env: { ...process.env, AW_HEAVY_LOCK_HELD: "1" } });
  return { code: r.status, out: `${r.stdout}${r.stderr}` };
};
const pkg = (dir, extra = {}) => {
  const base = { name: "Readiness", tier: "1", "error-prefix": "READINESS", shape: "small", host: "cli", binary: "aw-readiness", "first-slice": "CheckRepo", behavior: "reports_level_one_for_an_empty_repo", ...extra };
  const args = Object.entries(base).flatMap(([k, v]) => (v === undefined ? [] : [`--${k}`, v]));
  return run(NEW_PACKAGE, dir, args);
};
const slice = (d, extra) => run(NEW_SLICE, d, Object.entries(extra).flatMap(([k, v]) => [`--${k}`, v]));
const read = (dir, rel) => fs.readFileSync(path.join(dir, rel), "utf8");
const exists = (dir, rel) => fs.existsSync(path.join(dir, rel));
const refused = (r, rule) => {
  assert.equal(r.code, 1, r.out);
  assert.match(r.out, new RegExp(`^${rule}: `, "m"));
  assert.match(r.out, /^fix: /m);
};
const untouched = (fn) => {
  const dir = tree();
  const before = hashTree(dir);
  const r = fn(dir);
  assert.equal(hashTree(dir), before, "the tree changed");
  return r;
};

// ---- /new-package refusals: rule id, a fix, nothing written ----
t("NP-INPUT: a missing input", () => untouched((d) => refused(pkg(d, { tier: undefined }), "NP-INPUT")));
t("NP-NAME: a reserved name", () => untouched((d) => refused(pkg(d, { name: "Utils" }), "NP-NAME")));
t("NP-NAME: not PascalCase", () => untouched((d) => refused(pkg(d, { name: "readiness" }), "NP-NAME")));
t("NP-EXISTS: src/<Name> exists", () =>
  untouched((d) => {
    fs.mkdirSync(path.join(d, "src/Readiness"));
    const r = pkg(d);
    fs.rmdirSync(path.join(d, "src/Readiness"));
    refused(r, "NP-EXISTS");
  }));
t("NP-TIER: tier 0 is reserved", () => untouched((d) => refused(pkg(d, { tier: "0", name: "Newthing", note: "x" }), "NP-TIER")));
t("NP-DEPS-MISMATCH: tier differs from deps.yaml", () => untouched((d) => refused(pkg(d, { tier: "2" }), "NP-DEPS-MISMATCH")));
t("NP-INPUT: an undeclared package needs a note", () => untouched((d) => refused(pkg(d, { name: "Newthing", "error-prefix": "NEWTHING" }), "NP-INPUT")));
t("NP-PREFIX: malformed", () => untouched((d) => refused(pkg(d, { "error-prefix": "rd" }), "NP-PREFIX")));
t("NP-PREFIX: taken by the kernel", () => untouched((d) => refused(pkg(d, { "error-prefix": "KERNEL" }), "NP-PREFIX")));
t("NP-SMALL-HTTP", () => untouched((d) => refused(pkg(d, { host: "http" }), "NP-SMALL-HTTP")));
t("NP-DEPENDS: not a lower tier", () => untouched((d) => refused(pkg(d, { depends: "Packets" }), "NP-DEPENDS")));
t("NP-DEPENDS: unknown package", () => untouched((d) => refused(pkg(d, { depends: "Nope" }), "NP-DEPENDS")));
t("NP-FANOUT: more than 4 references", () =>
  untouched((d) => refused(pkg(d, { name: "Fanout", tier: "3", note: "x", "error-prefix": "FANOUT", depends: "Judge,Bridge,Packets" }), "NP-FANOUT")));
// A small App is its own host, so it also references Aw.Kernel.Telemetry: Core + Kernel + Telemetry + 2 depends = 5.
t("NP-FANOUT: a small App with 2 depends counts the telemetry reference", () =>
  untouched((d) => {
    const r = pkg(d, { name: "Fanout", tier: "2", note: "x", "error-prefix": "FANOUT", binary: "aw-fanout", depends: "Readiness,Packets" });
    refused(r, "NP-FANOUT");
    assert.match(r.out, /App would hold 5 in-repo references \(Core, Aw\.Kernel, Aw\.Kernel\.Telemetry and 2 depends\)/);
  }));
t("NP-FANOUT: a small App with 1 depend, and full Features with 2, stay within 4", () => {
  const ported = () => {   // a tree where Readiness and Packets are ported (their Core projects exist)
    const d = tree();
    for (const n of ["Readiness", "Packets"]) {
      const rel = `src/${n}/Aw.${n}.Core/Aw.${n}.Core.csproj`;
      fs.mkdirSync(path.dirname(path.join(d, rel)), { recursive: true });
      fs.copyFileSync(path.join(REPO, rel), path.join(d, rel));
    }
    return d;
  };
  const small = pkg(ported(), { name: "Fanout", tier: "2", note: "x", "error-prefix": "FANOUT", binary: "aw-fanout", depends: "Packets" });
  assert.equal(small.code, 0, small.out);
  const full = pkg(ported(), { name: "Fanout", tier: "2", note: "x", "error-prefix": "FANOUT", binary: "aw-fanout", shape: "full", depends: "Readiness,Packets" });
  assert.equal(full.code, 0, full.out);
});
t("full cli: the Host starts telemetry (Program, csproj, deps edge)", () => {
  const d = tree();
  const r = pkg(d, { name: "Democli", tier: "1", note: "a scaffold probe", "error-prefix": "DEMOCLI", shape: "full", binary: "aw-democli" });
  assert.equal(r.code, 0, r.out);
  const host = "src/Democli/Aw.Democli.Host";
  assert.match(read(d, `${host}/Composition.cs`), /\.AddKernelTelemetry\(TelemetryOptions\.FromEnvironment\("aw-democli", environment, clock\)\)/);
  assert.match(read(d, `${host}/Program.cs`), /_ = provider\.GetRequiredService<TelemetryRuntime>\(\);/);
  assert.match(read(d, `${host}/Aw.Democli.Host.csproj`), /<ProjectReference Include="\.\.\/\.\.\/Aw\.Kernel\.Telemetry\/Aw\.Kernel\.Telemetry\.csproj" \/>/);
  assert.match(read(d, "deps.yaml"), /- from: Democli\n {4}to: KernelTelemetry\n {4}kind: project\n {4}status: current/);
});
t("NP-PINS: a missing pin stops with the pin commit to make", () =>
  untouched((d) => {
    const props = path.join(d, "Directory.Packages.props");
    const before = fs.readFileSync(props, "utf8");
    fs.writeFileSync(props, before.replace(/\s*<PackageVersion Include="Microsoft\.Extensions\.DependencyInjection" [^>]*\/>/, ""));
    const r = pkg(d);
    fs.writeFileSync(props, before);
    refused(r, "NP-PINS");
    assert.match(r.out, /Microsoft\.Extensions\.DependencyInjection\b/);
    assert.match(r.out, /pin commit/);
  }));
t("a bad first slice reverts the whole package (NS-NAME)", () => untouched((d) => refused(pkg(d, { "first-slice": "x" }), "NS-NAME")));
t("a refusal after the package files are written reverts them (NS-ROUTE)", () =>
  untouched((d) => refused(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{nope}" }), "NS-ROUTE")));
t("NP-SMALL-NONE: a small package needs an entry point", () => untouched((d) => refused(pkg(d, { host: "none" }), "NP-SMALL-NONE")));
t("NP-DEPENDS: a declared but unported package", () => untouched((d) => refused(pkg(d, { name: "Scorerx", tier: "2", note: "x", "error-prefix": "SCORERX", depends: "Judge" }), "NP-DEPENDS")));
t("NP-EXISTS: the tests project already exists (Architecture)", () => untouched((d) => refused(pkg(d, { name: "Architecture", note: "x", "error-prefix": "ARCHX" }), "NP-EXISTS")));
t("NS-IDS: an id listed twice is refused, not a crash", () => untouched((d) => refused(pkg(d, { ids: "ThingId:string,ThingId:string" }), "NS-IDS")));
t("NS-ROUTE: a GET with an id outside the route", () =>
  untouched((d) => refused(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "GetItem", behavior: "gets_an_item", route: "GET /items", ids: "ItemId:string" }), "NS-ROUTE")));
t("NS-ROUTE: the same id twice in a route", () =>
  untouched((d) => refused(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "GetItem", behavior: "gets_an_item", route: "GET /a/{itemId}/{itemId}", ids: "ItemId:string" }), "NS-ROUTE")));
t("usage error: unknown option exits 2", () => untouched((d) => assert.equal(run(NEW_PACKAGE, d, ["--nope", "x"]).code, 2)));

// ---- /new-package output: small cli ----
t("small cli: the projects, the deps edge, the registry and the red test", () => {
  const d = tree();
  const r = pkg(d);
  assert.equal(r.code, 0, r.out);
  for (const f of [
    "src/Readiness/Aw.Readiness.Core/Aw.Readiness.Core.csproj",
    "src/Readiness/Aw.Readiness.Core/VogenDefaults.cs",
    "src/Readiness/Aw.Readiness.Core/ReadinessErrors.cs",
    "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj",
    "src/Readiness/Aw.Readiness.App/Composition.cs",
    "src/Readiness/Aw.Readiness.App/Commands/CheckRepoCommand.cs",
    "src/Readiness/Aw.Readiness.App/Features/CheckRepo/CheckRepoService.cs",
    "tests/Aw.Readiness.Tests/HostTests.cs",
    "tests/Aw.Readiness.Tests/CheckRepo/CheckRepoTests.cs",
    "tests/Aw.Readiness.Testing/CheckRepoRequestBuilder.cs",
  ])
    assert.ok(exists(d, f), `missing ${f}`);
  assert.ok(!exists(d, "src/Readiness/Aw.Readiness.Host"), "a small package has no Host");
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<AssemblyName>aw-readiness<\/AssemblyName>/);
  // PublishAot only under a RID, so the committed packages.lock.json stays RID-free (csharp.md "AOT scope").
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<AwAotEntryPoint>true<\/AwAotEntryPoint>/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<PublishAot Condition="'\$\(RuntimeIdentifier\)' != ''">true<\/PublishAot>/);
  assert.match(read(d, "deps.yaml"), /- from: Readiness\n {4}to: Kernel\n {4}kind: project\n {4}status: current/);
  // the host starts Kernel telemetry (spec §4b.4): Compose registers it, Program resolves it, the host references it
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Composition.cs"), /\.AddKernelTelemetry\(TelemetryOptions\.FromEnvironment\("aw-readiness", environment, clock\)\)/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Program.cs"), /_ = provider\.GetRequiredService<TelemetryRuntime>\(\);/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<ProjectReference Include="\.\.\/\.\.\/Aw\.Kernel\.Telemetry\/Aw\.Kernel\.Telemetry\.csproj" \/>/);
  assert.match(read(d, "deps.yaml"), /- from: Readiness\n {4}to: KernelTelemetry\n {4}kind: project\n {4}status: current/);
  assert.match(read(d, "tests/Aw.Readiness.Tests/HostTests.cs"), /public void Telemetry_is_composed_and_exports_nothing\(\)/);
  assert.match(read(d, "tests/Aw.Architecture.Tests/Packages.cs"), /"Aw\.Readiness\.Core",\n {8}"Aw\.Readiness\.App",\n {4}\];/);
  assert.match(read(d, "tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj"), /src\/Readiness\/Aw\.Readiness\.App\/Aw\.Readiness\.App\.csproj/);
  assert.match(read(d, "AgenticWorkflow.slnx"), /<Folder Name="\/src\/Readiness\/">/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/PublicAPI.Unshipped.txt"), /^#nullable enable\n$/);
  assert.match(read(d, "tests/Aw.Readiness.Testing/.editorconfig"), /dotnet_diagnostic\.MA0182\.severity = none/);
  assert.doesNotMatch(read(d, "src/Readiness/Aw.Readiness.Core/Aw.Readiness.Core.csproj"), /RS0041/, "no Guid/int id yet");
  const red = read(d, "tests/Aw.Readiness.Tests/CheckRepo/CheckRepoTests.cs");
  assert.match(red, /public async Task CheckRepo_reports_level_one_for_an_empty_repo\(\)/);
  assert.match(red, /Assert\.Fail\("red: assert reports_level_one_for_an_empty_repo"\)/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Features/CheckRepo/CheckRepoRequest.cs"), /internal sealed record CheckRepoRequest/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Commands/CommandTable.cs"), /CheckRepoCommand\.Name => await CheckRepoCommand\.Run/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Commands/CheckRepoCommand.cs"), /Name = "check-repo"/);
  assert.match(r.out, /^Commit: test: readiness package scaffold with a red CheckRepo test$/m);
  assert.match(r.out, /Make CheckRepoTests pass: replace the Assert\.Fail with the real assertion/);
});

// ---- /new-package output: full http with an id ----
t("full http: Host, loopback, endpoint, id, route binding test, undeclared node added", () => {
  const d = tree();
  const r = pkg(d, { name: "Demohttp", note: "a scaffold probe", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{itemId}/start", ids: "ItemId:string,OwnerId:Guid" });
  assert.equal(r.code, 0, r.out);
  const host = "src/Demohttp/Aw.Demohttp.Host";
  for (const f of [`${host}/LoopbackBinding.cs`, `${host}/LoopbackGuard.cs`, `${host}/ValueObjectValidation.cs`, `${host}/HttpResults.cs`, `${host}/Endpoints/StartItemEndpoint.cs`, `${host}/Endpoints/StartItemBody.cs`, "src/Demohttp/Aw.Demohttp.Core/Ids/ItemId.cs", "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs", "tests/Aw.Demohttp.Tests/Ids/ItemIdRouteBindingTests.cs", "tests/Aw.Demohttp.Tests/Ids/ItemIdTests.cs"])
    assert.ok(exists(d, f), `missing ${f}`);
  assert.match(read(d, `${host}/Aw.Demohttp.Host.csproj`), /Sdk="Microsoft\.NET\.Sdk\.Web"/);
  assert.match(read(d, `${host}/Aw.Demohttp.Host.csproj`), /<AwAotEntryPoint>true<\/AwAotEntryPoint>/);
  assert.match(read(d, `${host}/Endpoints/EndpointTable.cs`), /MapGroup\("\/demohttp"\)\.MapStartItem\(\);/);
  assert.match(read(d, `${host}/Endpoints/StartItemEndpoint.cs`), /MapPost\("\/items\/\{itemId\}\/start", static async \(ItemId itemId, StartItemBody body,/);
  assert.match(read(d, "deps.yaml"), /- id: Demohttp\n {4}kind: package\n {4}tier: 1\n/);
  assert.match(read(d, "deps.yaml"), /- from: Demohttp\n {4}to: Kernel\n {4}kind: project\n {4}status: current/);
  assert.doesNotMatch(read(d, `${host}/Composition.cs`), /using Microsoft\.Extensions\.DependencyInjection;/, "the Web SDK's implicit usings cover it");
  assert.match(read(d, "src/Demohttp/Aw.Demohttp.Core/Ids/ItemId.cs"), /\[JsonConverter\(typeof\(StringIdJsonConverter<ItemId>\)\)\]/);
  assert.match(read(d, "src/Demohttp/Aw.Demohttp.Core/Aw.Demohttp.Core.csproj"), /<NoWarn>\$\(NoWarn\);RS0041<\/NoWarn>/, "OwnerId is a Guid id");
  assert.ok(!exists(d, "tests/Aw.Demohttp.Testing/.editorconfig"), "full-shape builders are public");
  assert.match(read(d, `${host}/Program.cs`), /Pipeline\.Configure\(app\)/);
  // telemetry: the binary name defaults to the lowercase package name; ASP.NET's own providers are cleared
  assert.match(read(d, `${host}/Composition.cs`), /\.AddKernelTelemetry\(TelemetryOptions\.FromEnvironment\("demohttp", environment, clock\)\)/);
  assert.match(read(d, `${host}/Program.cs`), /_ = app\.Services\.GetRequiredService<TelemetryRuntime>\(\);/);
  assert.match(read(d, `${host}/LoopbackBinding.cs`), /_ = builder\.Logging\.ClearProviders\(\);/);
  assert.match(read(d, `${host}/Aw.Demohttp.Host.csproj`), /<ProjectReference Include="\.\.\/\.\.\/Aw\.Kernel\.Telemetry\/Aw\.Kernel\.Telemetry\.csproj" \/>/);
  assert.match(read(d, "deps.yaml"), /- from: Demohttp\n {4}to: KernelTelemetry\n {4}kind: project\n {4}status: current/);
  for (const layer of ["Core", "Features", "Infrastructure"])
    assert.doesNotMatch(read(d, `src/Demohttp/Aw.Demohttp.${layer}/Aw.Demohttp.${layer}.csproj`), /Aw\.Kernel\.Telemetry/, `only the host references telemetry, not ${layer}`);
  assert.match(read(d, "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs"), /Pipeline\.Configure\(app\)/);
  assert.match(read(d, "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs"), /"ASPNETCORE_PREFERHOSTINGURLS=true;ASPNETCORE_URLS=http:\/\/0\.0\.0\.0:0"/);
  // a second http slice: GET by a route id that already exists, chained on the same group, its own JSON lines
  const r2 = slice(d, { package: "Demohttp", slice: "GetItem", behavior: "gets_an_item", surface: "http", route: "GET /items/{itemId}" });
  assert.equal(r2.code, 0, r2.out);
  assert.match(read(d, `${host}/Endpoints/EndpointTable.cs`), /MapGroup\("\/demohttp"\)\.MapStartItem\(\)\.MapGetItem\(\);/);
  assert.match(read(d, `${host}/Endpoints/GetItemEndpoint.cs`), /MapGet\("\/items\/\{itemId\}", static async \(ItemId itemId, IService<GetItemRequest, GetItemResponse> service, CancellationToken ct\)/);
  assert.match(read(d, `${host}/Endpoints/GetItemEndpoint.cs`), /new GetItemRequest\(itemId\)/);
  assert.ok(!exists(d, `${host}/Endpoints/GetItemBody.cs`), "GET has no body");
  assert.match(read(d, `${host}/DemohttpJsonContext.cs`), /typeof\(GetItemResponse\)[\s\S]*typeof\(StartItemBody\)|typeof\(StartItemBody\)[\s\S]*typeof\(GetItemResponse\)/);
  assert.equal((read(d, "tests/Aw.Demohttp.Tests/Ids/IdsJsonContext.cs").match(/JsonSerializable/g) ?? []).length, 2, "ItemId reused, not redeclared");
});

// ---- /new-slice ----
t("new-slice: a second cli slice adds one command arm, one registration and one red test", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const r = slice(d, { package: "Readiness", slice: "ListRepos", behavior: "lists_nothing_for_an_empty_profile", surface: "cli", command: "list", ids: "RepoId:string" });
  assert.equal(r.code, 0, r.out);
  const table = read(d, "src/Readiness/Aw.Readiness.App/Commands/CommandTable.cs");
  assert.match(table, /CheckRepoCommand\.Name[\s\S]*ListReposCommand\.Name[\s\S]*_ => CommandOutput\.Usage/);
  const reg = read(d, "src/Readiness/Aw.Readiness.App/Features/ReadinessFeaturesServiceCollectionExtensions.cs");
  assert.equal((reg.match(/new ValidatingService</g) ?? []).length, 2);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Commands/ListReposCommand.cs"), /RepoId\.TryParse\(args\[0\], CultureInfo\.InvariantCulture, out var repoId\)/);
  assert.match(r.out, /^Commit: test: red ListRepos test \(lists_nothing_for_an_empty_profile\)$/m);
});
const sliceRefused = (extra, rule) => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const before = hashTree(d);
  refused(slice(d, extra), rule);
  assert.equal(hashTree(d), before, "the tree changed");
};
const base = { package: "Readiness", slice: "Other", behavior: "does_something", surface: "cli" };
t("NS-NAME (new-slice)", () => sliceRefused({ ...base, slice: "other" }, "NS-NAME"));
t("NS-BEHAVIOR (new-slice)", () => sliceRefused({ ...base, behavior: "Does Something" }, "NS-BEHAVIOR"));
t("NS-IDS: malformed", () => sliceRefused({ ...base, ids: "Thing:string" }, "NS-IDS"));
t("NS-IDS: an existing id with another primitive", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  assert.equal(slice(d, { ...base, ids: "ThingId:string" }).code, 0);
  const before = hashTree(d);
  refused(slice(d, { ...base, slice: "Third", command: "third", ids: "ThingId:Guid" }), "NS-IDS");
  assert.equal(hashTree(d), before);
});
t("NS-PORTS: not an interface name", () => sliceRefused({ ...base, ports: "ThingStore" }, "NS-PORTS"));
t("NS-PORTS: listed twice", () => sliceRefused({ ...base, ports: "IThingStore,IThingStore" }, "NS-PORTS"));
t("NS-EDIT: a hand-edited anchor is refused, not skipped", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const table = path.join(d, "src/Readiness/Aw.Readiness.App/Commands/CommandTable.cs");
  fs.writeFileSync(table, fs.readFileSync(table, "utf8").replace("_ => CommandOutput.Usage", "_ => Fallback"));
  const before = hashTree(d);
  refused(slice(d, base), "NS-EDIT");
  assert.equal(hashTree(d), before);
});
t("NS-EXISTS: the slice folder exists", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const before = hashTree(d);
  refused(slice(d, { package: "Readiness", slice: "CheckRepo", behavior: "again", surface: "cli" }), "NS-EXISTS");
  assert.equal(hashTree(d), before);
});
t("NS-SURFACE: surface doesn't match the host", () => sliceRefused({ ...base, surface: "http", route: "GET /x" }, "NS-SURFACE"));
t("NS-DEPS: ports + TimeProvider over 5", () => sliceRefused({ ...base, ports: "IA1x,IB1x,IC1x,ID1x,IE1x" }, "NS-DEPS"));
t("NS-COMMAND: the command is taken", () => sliceRefused({ ...base, command: "check-repo" }, "NS-COMMAND"));
t("NS-PACKAGE: no such package", () => untouched((d) => refused(slice(d, { package: "Nothere", slice: "Other", behavior: "does_something", surface: "cli" }), "NS-PACKAGE")));
t("new-slice: a new port gets its interface, fake and contract base", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const r = slice(d, { package: "Readiness", slice: "Score", behavior: "scores_a_repo", surface: "cli", ports: "IRepoReader" });
  assert.equal(r.code, 0, r.out);
  assert.ok(exists(d, "src/Readiness/Aw.Readiness.Core/IRepoReader.cs"));
  assert.ok(exists(d, "tests/Aw.Readiness.Testing/InMemoryRepoReader.cs"));
  assert.ok(exists(d, "tests/Aw.Readiness.Testing/RepoReaderContract.cs"));
});

t("NP-DEPENDS: a package listed twice", () => untouched((d) => refused(pkg(d, { depends: "Kernel,Kernel" }), "NP-DEPENDS")));
t("SIGINT while waiting on the heavy lock reverts everything and exits 1 (SCAFFOLD-INTERRUPTED)", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const state = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-state-"));
  trees.push(state);
  fs.mkdirSync(path.join(state, "locks", "heavy-job.lock"), { recursive: true });   // someone else's build
  const before = hashTree(d);
  const env = { ...process.env, AW_STATE_DIR: state };
  delete env.AW_HEAVY_LOCK_HELD;
  // spawnSync stops collecting a pipe once its timeout fires, so the child writes to a file instead.
  const log = path.join(state, "out.txt");
  const fd = fs.openSync(log, "w");
  const r = spawnSync("node", [NEW_SLICE, "--root", d, "--package", "Readiness", "--slice", "ListRepos", "--behavior", "lists_nothing", "--surface", "cli", "--command", "list"], { env, stdio: ["ignore", fd, fd], timeout: 1500, killSignal: "SIGINT" });
  fs.closeSync(fd);
  const out = fs.readFileSync(log, "utf8");
  assert.equal(r.status, 1, out);
  assert.match(out, /^SCAFFOLD-INTERRUPTED: /m);
  assert.match(out, /reverted \d+ files/);
  assert.equal(hashTree(d), before, "the tree changed");
});

t("SIGTERM to the generator alone stops a running command and what it started, and fails it (SCAFFOLD-INTERRUPTED)", () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-sig-"));
  trees.push(dir);
  const script = path.join(dir, "probe.mjs");
  const token = `31.${process.pid}${Date.now() % 100000}`;   // a sleep no other process on the box is running
  fs.writeFileSync(script, `import { run, trapSignals } from ${JSON.stringify(path.join(REPO, "scripts/scaffold/lib/common.mjs"))};
trapSignals(() => {});
const start = Date.now();
try {
  await run(".", "sh", ["-c", "sleep ${token} & wait"], { log: () => {} });   // a grandchild, like dotnet's build nodes
  console.log("not interrupted");
} catch (e) {
  // A shell's background job ignores SIGINT, so this probe waits for the SIGKILL 10 s later; still well under 31 s.
  console.log(e.message, Date.now() - start < 20000 ? "fast" : "slow");
}
`);
  const out = path.join(dir, "out.txt");
  const fd = fs.openSync(out, "w");
  // timeout + killSignal: SIGTERM goes to node's pid only, not its process group, so the sleep never sees it directly.
  spawnSync("node", [script], { cwd: dir, stdio: ["ignore", fd, fd], timeout: 1000, killSignal: "SIGTERM" });
  fs.closeSync(fd);
  assert.match(fs.readFileSync(out, "utf8"), /^SCAFFOLD-INTERRUPTED: interrupted \(SIGTERM\) fast$/m);
  const left = spawnSync("pgrep", ["-f", `^sleep ${token}$`], { encoding: "utf8" });
  assert.equal(left.stdout.trim(), "", "the grandchild outlived the interrupt");
});

const common = await import(path.join(REPO, "scripts/scaffold/lib/common.mjs"));
const colored = await common.run(REPO, "printf", ["\u001b[31mfailed Aw.X.Tests.T.T_does (1ms)\u001b[m\n  failed: 1\n"], { log: () => {} });
t("run() strips ANSI colors (CI colors MTP's 'failed' lines, which the red-test check parses)", () => {
  assert.equal(colored.out, "failed Aw.X.Tests.T.T_does (1ms)\n  failed: 1\n");
});

for (const dir of trees) fs.rmSync(dir, { recursive: true, force: true });
if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`${passed} passed, ${failures.length} failed`);
  process.exit(1);
}
console.log(`${passed} passed`);
