# Onyx P1 — pins, preflight, `onyx status` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `sindri onyx status [--json]` reports whether this box can run the pinned Onyx (Docker, Compose v2, Docker memory, free disk, port) against a pins file in the repo, with `SND-ONYX-001/002/003/006` for each failure.

**Architecture:** A pins file `context/onyx.lock` (checked by a bash script in CI) is embedded into the Sindri build and parsed in Core. A Core port `IContainerRuntime` (Docker facts) and two small probes (free disk, port) feed a pure `OnyxPreflight.Evaluate` decision in Core; the `OnyxStatus` slice in Features returns its rows; a direct `onyx status` arm in `CommandTable` prints them.

**Tech Stack:** C# (.NET 10, nullable, warnings as errors), xUnit v3 lanes, bash for the lock check.

**Spec:** `docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md` (§4 `onyx status`, §4.1 step 1, §6, §8). Repo rules: `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`, AGENTS.md "C# slice recipe".

## Global Constraints

- Onyx version pinned: `v4.9.0`. Never `latest`.
- Platforms in the pins: `linux/arm64`, `linux/amd64` (images); the Sindri host runs on macOS arm64 and Linux.
- Default port `3000`; fallback range `3001`–`3010`, loopback only (`127.0.0.1`).
- `onyx status` is readable by anyone (an agent session too); it never changes anything and never starts the control plane.
- Exit codes: 0 every check ok, 1 a check needs attention (any preflight failure), 2 usage or tool error.
- External commands run through `IToolRunner` (argument lists, never a shell string), each with a 5 s timeout.
- Error codes: `SND-ONYX-001` Docker CLI missing or daemon not answering; `SND-ONYX-002` Compose v2 missing; `SND-ONYX-003` not enough disk or Docker memory; `SND-ONYX-006` port taken by something not ours and no free fallback. Message text carries the fix (there is no fix field).
- One heavy job at a time: run `dotnet build`/`dotnet test` once per commit, not per edit.

## Review Focus

1. Docker installed but Docker Desktop not started: `docker version` prints the client and fails on the server → `SND-ONYX-001` whose fix says *start* Docker, not install it. Test in Task 3.
2. Docker daemon starting up and `docker info` hangs → the 5 s timeout → treated as "daemon not answering" (`SND-ONYX-001`), status still returns within ~15 s. Test in Task 3.
3. Only legacy `docker-compose` (v1) present, `docker compose version` fails → `SND-ONYX-002`. Test in Task 3.
4. `$AW_SINDRI_HOME` doesn't exist yet (fresh box) → the disk check measures the nearest existing ancestor, never errors. Test in Task 4.
5. Port 3000 taken by another process → status shows `taken` and the fallback it would use (first free of 3001–3010); all taken → `SND-ONYX-006`. Test in Task 4.

---

### Task 1: The pins file and its check

**Files:**
- Create: `context/onyx.lock`
- Create: `scripts/onyx-lock.sh`
- Create: `scripts/tests/onyx-lock.test.sh`
- Modify: `AGENTS.md` (Bash tests list; Commands: one line for `scripts/onyx-lock.sh check`)

**Interfaces:**
- Produces: the file format below, which Task 2 parses. Row kinds (whitespace-separated, `#` comments, blank lines ignored):
  - `version <tag> <commit-sha-40>` — exactly one row
  - `file <repo-relative path under deployment/docker_compose/> <sha256-64>` — one per deployment file P2 fetches
  - `image <repository> <platform> <digest sha256:…>` — every Onyx image × `linux/arm64` and `linux/amd64`
  - `need disk-bytes <n>` and `need docker-memory-bytes <n>` — exactly one each
- Produces: `scripts/onyx-lock.sh check [--file F]` → exit 0 ok; 1 with lines `ONYX-LOCK-00n <line>: <what>`; 2 usage.

- [ ] **Step 1: Write the failing bash test** `scripts/tests/onyx-lock.test.sh` in the style of `scripts/tests/binaries-lock.test.sh` (`check` helper, temp dir): the real `context/onyx.lock` passes; fixtures fail with their code: no `version` row (`ONYX-LOCK-001`), two `version` rows (001), a bad sha256 (002), an image row missing one of the two platforms (003), a digest not `sha256:` + 64 hex (002), a missing `need` row (004), an unknown row kind (005), a tag other than `v4.9.0`-shaped `vX.Y.Z` (001).
- [ ] **Step 2: Run it:** `bash scripts/tests/onyx-lock.test.sh` → FAIL (no script).
- [ ] **Step 3: Write `scripts/onyx-lock.sh`** (bash 3.2 compatible, header documents the format as above, `check` only).
- [ ] **Step 4: Measure and write `context/onyx.lock`** with real values, no pulls (disk is tight):
  - commit: `gh api repos/onyx-dot-app/onyx/git/ref/tags/v4.9.0` (deref an annotated tag to its commit).
  - files: the compose file `deployment/docker_compose/docker-compose.yml`, `env.template`, and every file the compose mounts from the repo (read the compose file at that commit; e.g. the nginx config under `deployment/data/nginx/`); sha256 each, downloaded to the scratchpad from `raw.githubusercontent.com/onyx-dot-app/onyx/<commit>/…`.
  - images: each `onyxdotapp/*` image the compose uses at `IMAGE_TAG=v4.9.0`, plus the third-party ones (postgres, opensearch, nginx, redis, minio) at the tags the compose names; per platform digest from `docker buildx imagetools inspect <image:tag> --raw` (the platform manifest's digest), no pull.
  - needs: `disk-bytes` = sum of the compressed layer sizes of the linux/arm64 manifests × 2.5 + 10 GiB (models and data headroom); `docker-memory-bytes` = 6 GiB (spec / `docs/sindri/onyx.md` one-user sizing). Write the method in a header comment; P2 re-measures on a real pull and adjusts.
- [ ] **Step 5: Run** `bash scripts/tests/onyx-lock.test.sh` → all ok; `bash scripts/tests/ci-bash-tests.test.sh` → ok.
- [ ] **Step 6: Commit** `feat: context/onyx.lock, the Onyx v4.9.0 pins, and its check`.

### Task 2: Pins in the build, parsed in Core

**Files:**
- Create: `src/Sindri/Aw.Sindri.Core/Onyx/OnyxPins.cs` (record + `static Result<OnyxPins> Parse(string text)`)
- Create: `src/Sindri/Aw.Sindri.Core/Onyx/OnyxImagePin.cs`, `OnyxFilePin.cs`
- Modify: `src/Sindri/Aw.Sindri.Infrastructure/Aw.Sindri.Infrastructure.csproj` (`<EmbeddedResource Include="../../../context/onyx.lock" LogicalName="onyx.lock" />`)
- Create: `src/Sindri/Aw.Sindri.Infrastructure/Onyx/EmbeddedOnyxPins.cs` (reads the resource, implements Core port `IOnyxPinsSource`)
- Create: `src/Sindri/Aw.Sindri.Core/Onyx/IOnyxPinsSource.cs`
- Test: `tests/Aw.Sindri.Tests/OnyxPinsTests/OnyxPinsParseTests.cs`, `EmbeddedOnyxPinsTests.cs`

**Interfaces:**
- Produces: `public sealed record OnyxPins(string Version, string Commit, IReadOnlyList<OnyxFilePin> Files, IReadOnlyList<OnyxImagePin> Images, long DiskBytes, long DockerMemoryBytes)`; `OnyxFilePin(string Path, string Sha256)`; `OnyxImagePin(string Repository, string Platform, string Digest)`; `public interface IOnyxPinsSource { OnyxPins Pins { get; } }`.
- `Parse` returns `SND-ONYX-004` (add it now: "the Onyx pins are malformed: <line>"; P2 reuses the code for a pin mismatch) on any row the bash check would reject.

- [ ] **Step 1: Failing tests** (`[Fact, Trait("Lane", "unit")]`): `Parse_reads_every_row_kind` (a fixture string with one of each → exact record values); `Parse_refuses_a_missing_version_row` (→ `SND-ONYX-004`); `Embedded_pins_parse_and_name_v4_9_0` (`new EmbeddedOnyxPins().Pins.Version == "v4.9.0"`, `Images` non-empty, every repository has both platforms).
- [ ] **Step 2:** `dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=unit"` → red.
- [ ] **Step 3:** implement; register `IOnyxPinsSource` as a singleton in `AddSindriInfrastructure`; add `SndOnyx004` to `SindriErrors` and its `All` list.
- [ ] **Step 4:** tests green; `dotnet build -warnaserror` zero warnings.
- [ ] **Step 5: Commit** `feat: the Onyx pins embedded in the Sindri build`.

### Task 3: Docker facts — `IContainerRuntime` and the CLI adapter

**Files:**
- Create: `src/Sindri/Aw.Sindri.Core/Onyx/IContainerRuntime.cs`, `ContainerRuntimeFacts.cs`
- Create: `src/Sindri/Aw.Sindri.Infrastructure/Onyx/DockerCliRuntime.cs`
- Create: `tests/Aw.Sindri.Testing/FakeContainerRuntime.cs`, `ContainerRuntimeContract.cs`
- Test: `tests/Aw.Sindri.Tests/OnyxStatus/DockerCliRuntimeTests.cs`, `FakeContainerRuntimeContractTests.cs`

**Interfaces:**
- Produces: `public interface IContainerRuntime { ContainerRuntimeFacts Inspect(CancellationToken cancellationToken); }`
- Produces: `public sealed record ContainerRuntimeFacts(bool CliFound, bool DaemonAnswers, string? ServerVersion, bool ComposeV2, string? ComposeVersion, long? MemoryBytes)`
- `DockerCliRuntime(IToolRunner runner)`: `docker version --format {{.Server.Version}}` (exit 0 and non-empty → daemon answers), `docker compose version --short`, `docker info --format {{.MemTotal}}`; each 5 s; `ToolRun.Missing` → `CliFound=false`; `TimedOut` or non-zero → that fact false/null. Never throws.

- [ ] **Step 1: Failing tests** with `FakeToolRunner` answering per command (extend the fake to answer by argument list if it only takes one answer — keep its contract test passing): `Inspect_reports_missing_cli` (Missing → `CliFound false`); `Inspect_reports_daemon_down_when_server_version_fails` (Review Focus 1: version exits 1 → `CliFound true, DaemonAnswers false`); `Inspect_treats_a_timeout_as_daemon_down` (Review Focus 2); `Inspect_reports_compose_v1_only` (Review Focus 3: compose exits 1 → `ComposeV2 false`); `Inspect_reads_memory_bytes` (`"16818978816"` → 16818978816); and the asserted argument lists. Contract: `ContainerRuntimeContract` bound to the fake.
- [ ] **Step 2:** red. **Step 3:** implement; register `IContainerRuntime` → `DockerCliRuntime(new ProcessToolRunner())` in `AddSindriInfrastructure` (as `CreateSecretPointers` does). **Step 4:** green, zero warnings.
- [ ] **Step 5: Commit** `feat: Docker facts for the Onyx preflight`.

### Task 4: The preflight decision, disk and port probes

**Files:**
- Create: `src/Sindri/Aw.Sindri.Core/Onyx/OnyxPreflight.cs` (pure: `static PreflightReport Evaluate(ContainerRuntimeFacts docker, DiskFacts disk, PortFacts port, OnyxPins pins)`)
- Create: `src/Sindri/Aw.Sindri.Core/Onyx/PreflightReport.cs`, `PreflightCheck.cs`, `DiskFacts.cs`, `PortFacts.cs`, `IHostProbes.cs`
- Create: `src/Sindri/Aw.Sindri.Infrastructure/Onyx/HostProbes.cs`
- Create: `tests/Aw.Sindri.Testing/FakeHostProbes.cs`
- Modify: `src/Sindri/Aw.Sindri.Core/SindriErrors.cs` (`SND-ONYX-001`, `002`, `003`, `006`, into `All`)
- Test: `tests/Aw.Sindri.Tests/OnyxStatus/OnyxPreflightTests.cs`, `HostProbesTests.cs`

**Interfaces:**
- Produces: `public sealed record PreflightCheck(string Name, CheckState State, string Detail, Error? Problem)` (`CheckState` from `Aw.Sindri.Core.Doctor`); names exactly `docker`, `compose`, `memory`, `disk`, `port`, in that order.
- Produces: `public sealed record PreflightReport(IReadOnlyList<PreflightCheck> Checks, int Port)` with `bool Ok` (all `CheckState.Ok`); `Port` is the port setup would use (3000, or the first free fallback, or 3000 when none).
- Produces: `public sealed record DiskFacts(string MeasuredPath, long FreeBytes)`; `public sealed record PortFacts(int Preferred, bool PreferredFree, int? FirstFreeFallback)`.
- Produces: `public interface IHostProbes { DiskFacts FreeDisk(string path); PortFacts Ports(int preferred, int fallbackFirst, int fallbackLast); }` — `FreeDisk` walks up to the nearest existing ancestor (`DriveInfo` of it); `Ports` tries to bind `127.0.0.1:<p>` with a `TcpListener` and releases it.
- Rules: docker fails `001` when `!CliFound` (fix: install Docker Desktop or Engine) or `!DaemonAnswers` (fix: start Docker Desktop / the docker service); compose fails `002` when `!ComposeV2` (skipped as Warn "needs docker" when docker failed); memory fails `003` when `MemoryBytes < pins.DockerMemoryBytes` (fix names Docker Desktop → Settings → Resources and the needed GiB; Warn when unknown); disk fails `003` when `FreeBytes < pins.DiskBytes` (detail: free GiB at the path, needed GiB); port Ok when preferred free, Ok with detail "3000 taken; would use <p>" when a fallback exists, fails `006` otherwise. GiB printed with one decimal.

- [ ] **Step 1: Failing tests:** one per rule above with exact `Detail` strings (e.g. `"11.0 GiB free at /h, needs 30.0 GiB"`), `Evaluate_all_ok_returns_port_3000`, `Evaluate_uses_the_first_free_fallback` (Review Focus 5), `Evaluate_fails_006_when_no_port_is_free`; `FreeDisk_measures_the_nearest_existing_ancestor` (Review Focus 4: a non-existent path under a temp dir → `MeasuredPath` is the temp dir); `Ports_reports_a_bound_port_as_taken` (integration lane: bind a listener on a free port, probe it).
- [ ] **Step 2:** red. **Step 3:** implement; register `IHostProbes` → `HostProbes`. **Step 4:** green.
- [ ] **Step 5: Commit** `feat: the Onyx preflight decision`.

### Task 5: The `OnyxStatus` slice and the `onyx status` verb

**Files:**
- Create: `src/Sindri/Aw.Sindri.Features/OnyxStatus/OnyxStatusRequest.cs`, `OnyxStatusResponse.cs`, `OnyxStatusService.cs`
- Modify: `src/Sindri/Aw.Sindri.Features/SindriFeaturesServiceCollectionExtensions.cs` (register)
- Modify: `src/Sindri/Aw.Sindri.Host/Commands/CommandTable.cs` (arm `["onyx", "status", .. var rest] when Json(rest) is { } json`), `Verbs.cs` (`OnyxStatus(bool json, CancellationToken)`), `Usage.cs`
- Modify: `src/Sindri/Aw.Sindri.Host/SindriJsonContext.cs` (`[JsonSerializable(typeof(OnyxStatusResponse))]`)
- Modify: `docs/csharp/sindri.md` (command row; `## Errors` rows for `SND-ONYX-001..004, 006`)
- Test: `tests/Aw.Sindri.Tests/OnyxStatus/OnyxStatusTests.cs`, `tests/Aw.Sindri.Tests/Commands/OnyxStatusCommandTests.cs`

**Interfaces:**
- Consumes: `IOnyxPinsSource`, `IContainerRuntime`, `IHostProbes`, `OnyxPreflight.Evaluate`; the Sindri home path as other slices get it (`$AW_SINDRI_HOME` default `$AW_STATE_DIR/sindri-app`).
- Produces: `public sealed record OnyxStatusRequest;` `public sealed record OnyxStatusResponse(string State, string PinnedVersion, string PinnedCommit, IReadOnlyList<PreflightCheck> Checks, int Port, int ExitCode)` — `State` is `"not-set-up"` in P1 (P2 adds `running`, `stopped`); `ExitCode` 0 when the report is Ok, else 1.
- Text output (exact, one row per line, name padded to 10 like `status`):
  ```
  onyx      not set up (run `sindri-app onyx up`)
  pinned    v4.9.0 (<first 7 of commit>)
  docker    ok 29.4.0
  compose   ok v2.40.3
  memory    ok 15.7 GiB for Docker, needs 6.0 GiB
  disk      fail 11.0 GiB free at <path>, needs <n> GiB (SND-ONYX-003: <message>)
  port      ok 3000 free
  ```
  (`ok`/`warn`/`fail` as doctor prints them.) `--json` is the response through `Output.Json`. Works with the control plane down; an agent session is allowed.

- [ ] **Step 1: Failing tests:** `Status_is_not_set_up_with_the_pins_version` (fakes all ok → `State "not-set-up"`, `ExitCode 0`, five checks); `Status_exit_1_when_disk_is_short`; command tests (`[Collection(ProcessEnvironment.Name)]`, console swap as `CommandTableTests`): `onyx status` prints the seven lines above for a fixed fake set; `onyx status --json` parses as `OnyxStatusResponse`; `onyx status --bogus` is a usage error, exit 2; works with no control plane recorded.
- [ ] **Step 2:** red. **Step 3:** implement and wire. **Step 4:** `dotnet build -warnaserror`; unit + integration lanes for `tests/Aw.Sindri.Tests`; `dotnet format --verify-no-changes`; `node scripts/check-deps.mjs`; `scripts/check-env-example.sh` (ignore only the untracked `.worktrees/` names); `node scripts/check-rules.mjs`.
- [ ] **Step 5: Commit** `feat: sindri onyx status, the Onyx preflight`.
