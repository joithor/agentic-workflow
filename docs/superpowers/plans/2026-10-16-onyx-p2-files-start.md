# Onyx P2 — fetch, config, pull, start, `onyx up` / `down`, doctor `onyx:stack` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `sindri onyx up` (a human at a terminal) takes a box from nothing to the pinned Onyx v4.9.0 running on `127.0.0.1` (preflight, verified file fetch, `.env` and compose override, digest-pinned pull, start, readiness), idempotent and resumable; `sindri onyx down` stops it; `onyx status` and doctor's `onyx:stack` report it. Account setup, the key, the profile write and the serve/stop hooks are P3/P4.

**Architecture:** Core holds the pure decisions (image-reference resolution, `.env` and override rendering, the step stamps, `PreflightReport.Blocks`) and the ports (`IContainerRuntime` gains the compose operations, `IDeployFiles`, `IOnyxWorkspace`, `IComposeServices`, `IOnyxHealth`, `IOnyxProgress`). A Features slice `OnyxUp` runs steps 1-5 of spec §4.1 against those ports, recording each completed step with the stamp of its inputs in `$AW_SINDRI_HOME/onyx/state.json`; `OnyxDown` stops the project; `OnyxStatus` and a new `OnyxStackDoctor` read it. Infrastructure has the docker CLI, HTTPS fetch, YAML read, file and HTTP-probe adapters, each with a fake and a contract test; Host adds the `up`/`down` verbs behind the same human-terminal refusal `mcp register --apply` uses.

**Tech Stack:** C# (.NET 10, nullable, warnings as errors), YamlDotNet (read only), xUnit v3 lanes (`unit`, `integration`, `heavy`), bash (aot-smoke).

**Spec:** `docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md` (§4, §4.1 steps 1-5, §4.2, §5.4, §6, §8, §9, §11 P2). Builds on `docs/superpowers/plans/2026-10-16-onyx-p1-pins-preflight.md` and its code (`Core/Onyx/*`, `Features/OnyxStatus`, `Infrastructure/Onyx/*`, `context/onyx.lock`). Repo rules: `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`, AGENTS.md "C# slice recipe". Carried items: `/Users/joi/.agentic-workflow/jthorare-agentic-workflow/plans/onyx-managed/stages.md` "Carried into later stages" (every P2 line is a task below).

## Global Constraints

- Onyx `v4.9.0` pinned, commit `f1738ed539dc69d3bb06520c96fa068f9bd45c31`. Never `latest`, never a tag without a digest.
- Compose project name `sindri-onyx`; files under `$AW_SINDRI_HOME/onyx/` (`state.json`, `deploy/deployment/<lock path>`, `.env` and `docker-compose.override.yml` beside the compose file in `deploy/deployment/docker_compose/`).
- nginx published on `127.0.0.1:<port>:80` only; no port on `0.0.0.0`; `DISABLE_TELEMETRY=true`, `AUTH_TYPE=basic`, `IMAGE_TAG=v4.9.0`, `INDEXING_MODEL_SERVER_HOST=inference_model_server`.
- Fetch: `https://raw.githubusercontent.com/onyx-dot-app/onyx/<commit>/deployment/<lock path>`, https only, no redirects followed, 30 s per file, 1 MiB cap, sha256 checked before anything is written; a mismatch is `SND-ONYX-004` and leaves existing good files alone.
- Every compose `image:` must resolve to a (repository, tag) the lock pins; else `SND-ONYX-004`. Images run as `<repository>@<platform digest>` from the lock.
- Readiness: poll `GET /api/health`, then `GET /api/version` every 2 s, deadline 15 min; wrong version or timeout is `SND-ONYX-005`.
- `up` and `down` are the human's: an agent session or a non-terminal gets `SND-AUTH-002` (exit 1), like `mcp register --apply`. `status` stays readable by anyone and never changes anything.
- Progress on stderr (lines and dots), stdout carries only the summary. Exit codes: 0 ok; 1 a step failed or was refused (the `SND-ONYX-0nn` error names the fix); 2 usage or tool error.
- External commands go through `IToolRunner` (argument lists, never a shell string). Timeouts: 5 s for the read-only facts (as P1), 45 min for `compose pull`, 5 min for `compose up -d`, 30 s for `ps`/`stop`/`image inspect`.
- New codes: `SND-ONYX-005` (not ready or wrong version), `SND-ONYX-010` (Docker points at another machine), `SND-ONYX-011` (a `docker compose` command failed; message ends with the last stderr lines). Messages carry the fix.
- One heavy job at a time: build and run tests once per commit, never per edit. The `heavy` test and the real end-to-end proof are not run in tasks 1-10.

## Review Focus

1. `DOCKER_HOST=tcp://…` or a remote `docker context` is refused before anything is fetched or pulled, with the endpoint in the message (`SND-ONYX-010`). Tests in Tasks 3 and 7.
2. A redirect (even same-scheme), an http base URL, or an oversize body from the file host is refused and the existing good file is untouched. Tests in Task 4.
3. `Ctrl-C` or a crash during pull or readiness leaves `state.json` with only the completed steps; the rerun resumes at the interrupted step and repeats nothing earlier. Tests in Task 7.
4. A rerun while Onyx is running on its own port: the port check must not call it "taken" or move it, `up` changes nothing, `status` shows the port as Onyx's own. Tests in Tasks 2, 7 and 8.
5. A corrupt `state.json`, or a `.env` that lost `USER_AUTH_SECRET`, must not crash `up`: state is treated as empty (every step re-checked, all idempotent); a present secret is always kept, a missing one generated, never two different secrets across reruns. Tests in Tasks 5 and 7.

---

### Task 1: Tool runner to `Infrastructure.Processes`, with long-run and streaming variants

**Files:**
- Move (`git mv`, namespace `Aw.Sindri.Infrastructure.Secrets` → `Aw.Sindri.Infrastructure.Processes`): `Infrastructure/Secrets/{IToolRunner,ProcessToolRunner,ToolRun,ToolOutcome}.cs` → `Infrastructure/Processes/`; `tests/Aw.Sindri.Testing/{FakeToolRunner,ToolRunnerContract}.cs` keep their folder, new `using`; tests `tests/Aw.Sindri.Tests/Secrets/{FakeToolRunnerContractTests,ProcessToolRunnerContractTests,ProcessToolRunnerTests}.cs` → `tests/Aw.Sindri.Tests/Processes/`.
- Modify: every `using Aw.Sindri.Infrastructure.Secrets;` that only wanted the runner (`PointerSecrets.cs`, `DockerCliRuntime.cs`, the registration extension, tests); `PublicAPI`/baseline files if any name the old namespace.

**Interfaces:**
- Produces: `internal interface IToolRunner { ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, Action<string>? standardErrorLine = null, CancellationToken cancellationToken = default); }` and `ToolOutcome.Cancelled = 4`, `ToolRun.Cancelled`. Existing 3-argument calls still compile. `standardErrorLine` is called once per stderr line while the tool runs (the pull progress); stdout is still captured whole; cancellation kills the process tree and returns `ToolRun.Cancelled`.

- [ ] **Step 1:** `git mv` the four files and three tests, change namespaces and usings, nothing else. Run `dotnet build -warnaserror` (zero warnings) and `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8` (same results as before). Commit `refactor: IToolRunner and ToolRun move to Infrastructure.Processes`.
- [ ] **Step 2: Failing contract tests** in `ToolRunnerContract` (runs against fake and `ProcessToolRunner`): `Standard_error_lines_reach_the_callback_while_the_tool_runs` (`sh -c "echo a >&2; echo b >&2"` → callback saw `["a","b"]`, run is `Exited` 0 with empty output); `A_cancelled_run_is_cancelled_and_the_tool_is_killed` (`sleep 30`, token cancelled after 200 ms → `ToolRun.Cancelled`, returns within 5 s). The fake binding scripts the stderr lines and honours a cancelled token the same way.
- [ ] **Step 3:** implement both variants in `ProcessToolRunner` (read stderr line by line on the same async drain that exists today; register the token to `Kill(entireProcessTree: true)`); update `FakeToolRunner` to record the extra arguments (`Calls` gains `HadStderrCallback`) and take scripted stderr lines.
- [ ] **Step 4:** build zero warnings; unit lane green. Commit `feat: IToolRunner streams stderr and honours cancellation`.

### Task 2: Core decisions: errors, `Blocks`, own-port, image references, `.env` and override rendering

**Files:**
- Modify: `Core/SindriErrors.cs` (add `OnyxNotReady` = `SND-ONYX-005` Unavailable, `OnyxRemoteDocker` = `SND-ONYX-010` Unavailable, `OnyxComposeFailed` = `SND-ONYX-011` Unavailable; append to `All`; update the `OnyxPinsMalformed` doc comment: also "a downloaded file or image failed its pin, or a compose image has no pin"), `Core/Onyx/PreflightReport.cs`, `Core/Onyx/OnyxPreflight.cs`, `Core/Onyx/ContainerRuntimeFacts.cs`, `Core/Onyx/OnyxState.cs`.
- Create: `Core/Onyx/ImageRef.cs`, `Core/Onyx/ComposeImageRef.cs`, `Core/Onyx/ComposeService.cs`, `Core/Onyx/OnyxConfig.cs`, `Core/Onyx/OnyxPaths.cs`, `Core/Onyx/OnyxSetupState.cs`, `Core/Onyx/OnyxSteps.cs`.
- Test: `tests/Aw.Sindri.Tests/OnyxUp/ComposeImageRefTests.cs`, `OnyxConfigTests.cs`, `OnyxPreflightBlocksTests.cs` (and extend `tests/Aw.Sindri.Tests/OnyxStatus/OnyxPreflightTests.cs`).

**Interfaces:**
- `ContainerRuntimeFacts` gains `string? Endpoint` (last positional field: the daemon endpoint from `DOCKER_HOST`, else `docker context inspect --format {{.Endpoints.docker.Host}}`; null when unreadable). `OnyxPreflight.Evaluate(ContainerRuntimeFacts docker, HostFacts host, OnyxPins pins, int? ownPort = null)`; the docker row order is: CLI missing (001), Endpoint not starting `unix://` or `npipe://` — including null (010, message "Docker points at another machine (<endpoint>): Sindri only runs Onyx on this one; switch to the local context" with `unknown` for null), daemon down (001). With `ownPort` set the port row is `Ok("port", "<n> (Onyx's own)")` and `Port` is `ownPort`.
- `PreflightReport.Blocks` (bool): true when any check is `Fail`; a `Warn` row never blocks (the "unknown Docker memory" warn is printed by `up`, not blocking). Document this on the property.
- `public sealed record ImageRef(string Repository, string Tag)` (Repository normalized: `docker.io/` added when no registry, `library/` added for a bare name). `ComposeImageRef.Resolve(string raw, IReadOnlyDictionary<string,string> env) → Result<ImageRef>`: expands `${NAME}` and `${NAME:-default}` (defaults may nest `${…}`; `env` wins), strips a trailing `@sha256:…`, splits the tag; anything else (`:?`, `$$`, no tag) is `SND-ONYX-004`.
- `public sealed record ComposeService(string Name, string? Image, bool HasProfiles)`.
- `public sealed record EnvSettings(string ImageTag, int Port, string AuthSecret)`; `OnyxConfig.RenderEnv(string template, EnvSettings s) → string` (sets or appends, one line each: `IMAGE_TAG`, `DISABLE_TELEMETRY=true`, `AUTH_TYPE=basic`, `USER_AUTH_SECRET`, `INDEXING_MODEL_SERVER_HOST=inference_model_server`, `HOST_PORT`; replaces an uncommented `KEY=…` line or an commented `# KEY=…` line in place, else appends; LF, trailing newline); `OnyxConfig.ReadSecret(string? envText) → string?`; `OnyxConfig.RenderOverride(IReadOnlyList<ComposeService> services, EnvSettings s, OnyxPins pins, string platform) → Result<string>`; `OnyxConfig.ActiveImages(services, s, pins, platform) → Result<IReadOnlyList<string>>` (the `<repository>@<digest>` of every service without profiles in compose or override).
- `public sealed record OnyxPaths(string Root, string State, string DeployDir, string Compose, string Env, string Override)` from `OnyxPaths.For(string sindriHome)` per the Global Constraints layout.
- `public sealed record OnyxSetupState(int Port, IReadOnlyDictionary<string,string> Steps)` (step name → stamp of the inputs it ran with); `OnyxSteps` consts `Fetch="fetch"`, `Config="config"`, `Pull="pull"`, `Start="start"`; `OnyxSteps.Stamp(params string[] parts) → string` (sha256 hex of the parts joined with `\n`).
- `OnyxState` gains `Running` (kebab `running`) and `Stopped` (`stopped`).

- [ ] **Step 1: Failing tests** (`[Fact, Trait("Lane","unit")]`):
  - `Resolve_expands_defaults_and_env`: `${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}` with `IMAGE_TAG=v4.9.0` → `ImageRef("docker.io/onyxdotapp/onyx-backend","v4.9.0")`; `${BASE_IMAGE_REGISTRY:-docker.io}/library/postgres:15.2-alpine` → `docker.io/library/postgres`/`15.2-alpine`; `onyxdotapp/code-interpreter:${CODE_INTERPRETER_IMAGE_TAG:-0.4.8}` → `docker.io/onyxdotapp/code-interpreter`/`0.4.8`; the minio line with `@sha256:7330…` → tag `RELEASE.2025-07-23T15-54-02Z-cpuv1`; `redis` without a tag and `${X:?m}` → `SND-ONYX-004`.
  - `RenderEnv_sets_every_key_once_and_keeps_the_rest`: against the committed v4.9.0 `env.template` (Task 5 adds the fixture; until then an inline template with `IMAGE_TAG=latest`, `# DISABLE_TELEMETRY=`, `USER_AUTH_SECRET=""`) → each of the six keys appears exactly once with the exact value; running it twice on its own output is a no-op.
  - `ReadSecret_returns_the_existing_secret` / `ReadSecret_is_null_when_missing_or_empty`.
  - `RenderOverride_binds_nginx_to_loopback_and_pins_every_image`: for the fixture services (inline list of the 11 compose services in Task 5; here a 4-service list) and platform `linux/arm64` → the exact expected text (golden file `tests/Aw.Sindri.Tests/OnyxUp/Golden/override.arm64.yml`, written by hand once from the spec sizing; amd64 variant differs only in digests); contains `ports: !override` then `- "127.0.0.1:3000:80"`; `image: docker.io/onyxdotapp/onyx-backend@sha256:7c3877…` for every service sharing that image; `opensearch` heap `-Xms1g -Xmx1g` and `mem_limit: 2g`; `indexing_model_server` and `code-interpreter` `profiles: ["indexing-model-server"]` / `["code-interpreter"]`; `background` `depends_on.indexing_model_server {condition: service_started, required: false}` and the three `CELERY_WORKER_*_CONCURRENCY=1`; byte-identical on a second call (services sorted ordinally).
  - `RenderOverride_refuses_an_unpinned_image` (a service whose image resolves to `docker.io/library/mysql:8` → `SND-ONYX-004` naming the service and image) and `RenderOverride_refuses_a_compose_without_a_sized_service` (no `opensearch` → `SND-ONYX-004`, "the compose layout changed").
  - `Blocks_is_true_only_for_a_fail`: all ok → false; a memory `Warn` alone → false; a disk `Fail` → true.
  - Preflight: `A_tcp_endpoint_fails_the_docker_row_with_010`, `A_null_endpoint_fails_with_010_unknown`, `An_unix_and_an_npipe_endpoint_pass`, `Own_port_makes_the_port_row_ok_even_when_taken`.
- [ ] **Step 2:** `dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=unit"` → red (types missing).
- [ ] **Step 3:** implement. Bots-off decision (put it in the `OnyxConfig` doc comment): the `background` service gets `command: ["/bin/sh","-c","sed -e '/^\\[program:slack_bot\\]/a autostart=false' -e '/^\\[program:discord_bot\\]/a autostart=false' /etc/supervisor/conf.d/supervisord.conf > /tmp/supervisord.conf && exec /usr/bin/supervisord -c /tmp/supervisord.conf"]` (the image's own config patched in a copy; no extra file to fetch or mount; replaces the manual page's copy-out-and-mount, which needs the image pulled before config is written). The override is built as text, not through a YAML serializer, because `!override` is a tag; spec §4.1 step 3 sizing otherwise as in `docs/sindri/onyx.md` "A one-user dev box".
- [ ] **Step 4:** build zero warnings, unit lane green. Commit `feat: Onyx image resolution, .env and override rendering, Blocks, SND-ONYX-005/010/011`.

### Task 3: `IContainerRuntime` compose operations, docker adapter, fake, contract

**Files:**
- Create: `Core/Onyx/ComposeProject.cs`, `ComposeContainer.cs` (with `ComposePublisher`).
- Modify: `Core/Onyx/IContainerRuntime.cs`, `Infrastructure/Onyx/DockerCliRuntime.cs`, `tests/Aw.Sindri.Testing/FakeContainerRuntime.cs`, `tests/Aw.Sindri.Testing/ContainerRuntimeContract.cs`.
- Test: `tests/Aw.Sindri.Tests/OnyxStatus/DockerCliRuntimeTests.cs` (extend), `DockerCliRuntimeContractTests.cs`, `FakeContainerRuntimeContractTests.cs`.

**Interfaces:**
- Consumes: `IToolRunner` (Task 1).
- Produces: `public sealed record ComposeProject(string ComposeFile, string OverrideFile, string EnvFile) { public const string Name = "sindri-onyx"; }`; `ComposeContainer(string Service, string State, string? Health, string Image, IReadOnlyList<ComposePublisher> Publishers)`; `ComposePublisher(string Address, int PublishedPort, int TargetPort)`. `IContainerRuntime` keeps `Inspect` and gains: `Result<string> ServerPlatform(CancellationToken)` (`linux/arm64` form), `Result<Unit> Pull(ComposeProject, Action<string> progress, CancellationToken)`, `Result<Unit> Up(ComposeProject, Action<string> progress, CancellationToken)`, `Result<Unit> Stop(ComposeProject, CancellationToken)`, `Result<IReadOnlyList<ComposeContainer>> Ps(ComposeProject, CancellationToken)`, `Result<IReadOnlyDictionary<string, IReadOnlyList<string>>> RepoDigests(IReadOnlyList<string> images, CancellationToken)` (image reference → its `RepoDigests`, normalized by dropping `docker.io/` and `library/`; an image not present maps to an empty list). A `Fail` from any is `SND-ONYX-011` (or `SND-ONYX-001` when the CLI is missing or the daemon hung).
- Exact commands: `docker version --format {{.Server.Os}}/{{.Server.Arch}}` (arch `aarch64`→`arm64`, `x86_64`→`amd64`); `docker compose -p sindri-onyx -f <compose> -f <override> --env-file <env> pull` ; `… up -d --no-build` (build contexts don't exist on disk; the images are pulled) ; `… stop` ; `… ps -a --format json` (parse both one-object-per-line and a single JSON array; fields `Service`, `State`, `Health`, `Image`, `Publishers[].URL/TargetPort/PublishedPort`); `docker image inspect --format {{json .RepoDigests}} <image>` per image. `Inspect` additionally reads the endpoint: `DOCKER_HOST` (adapter constructor takes `Func<string,string?> environment`) else `docker context inspect --format {{.Endpoints.docker.Host}}`.

- [ ] **Step 1: Failing tests.** Adapter tests over a scripted `FakeToolRunner`: `ServerPlatform_normalizes_aarch64_and_x86_64`; `Pull_passes_the_exact_argument_list_with_the_45_minute_timeout_and_streams_stderr_to_progress`; `Up_uses_no_build_and_a_5_minute_timeout`; `A_nonzero_compose_exit_is_011_with_the_last_stderr_lines`; `Ps_parses_one_json_object_per_line_and_a_json_array` (Publishers `{"URL":"127.0.0.1","TargetPort":80,"PublishedPort":3000}`, and a `0.0.0.0` one); `RepoDigests_normalizes_and_maps_a_missing_image_to_empty`; `Inspect_reads_DOCKER_HOST_before_the_context` (env `tcp://10.0.0.5:2375` → `Facts.Endpoint` equals it and `docker context inspect` is not called); `Inspect_reads_the_context_endpoint_otherwise`. Contract additions (fake and adapter): `Pull_up_stop_are_ok_on_a_healthy_box`, `Ps_reports_what_up_started` (fake keeps a container list that `Up` fills and `Stop` marks exited), `A_down_daemon_fails_every_compose_operation_with_001`. The fake gets `Containers`, `Calls` (ordered list of operation names) and `FailNext(string operation, Error)` for later tasks.
- [ ] **Step 2:** run, red. **Step 3:** implement. **Step 4:** build zero warnings; unit lane green. Commit `feat: IContainerRuntime compose operations`.

### Task 4: `IDeployFiles`: verified fetch

**Files:**
- Create: `Core/Onyx/IDeployFiles.cs`, `Infrastructure/Onyx/HttpDeployFiles.cs`, `tests/Aw.Sindri.Testing/FakeDeployFiles.cs`, `tests/Aw.Sindri.Testing/DeployFilesContract.cs`, `tests/Aw.Sindri.Testing/LoopbackFileServer.cs` (a small `HttpListener`/Kestrel loopback server serving a path→bytes map, optionally a redirect or oversize body).
- Modify: Infrastructure registration (adds `IDeployFiles` with a `SocketsHttpHandler { AllowAutoRedirect = false }` client).
- Test: `tests/Aw.Sindri.Tests/OnyxUp/FakeDeployFilesContractTests.cs` (unit), `HttpDeployFilesContractTests.cs` (integration).

**Interfaces:**
- Produces: `public interface IDeployFiles { ValueTask<Result<DeployFetch>> Fetch(OnyxPins pins, string deployDir, Action<string> progress, CancellationToken cancellationToken); }` and `public sealed record DeployFetch(int Downloaded, int Present)`. `HttpDeployFiles(HttpClient client, Uri baseUri)`: production base `https://raw.githubusercontent.com/onyx-dot-app/onyx/`; a non-https `baseUri` is accepted only when `baseUri.IsLoopback` (the integration test's server). It requests `<base><commit>/deployment/<path>`, writes `<deployDir>/<path>` (parents created) via temp file and rename after the hash matches, skips a present file whose sha256 matches, replaces a present file whose hash differs only with verified content.

- [ ] **Step 1: Failing contract tests** (`DeployFilesContract`, abstract `Create(IReadOnlyDictionary<string,byte[]> remote, IReadOnlyDictionary<string,byte[]> local)` plus `ReadLocal(path)`; run on the fake and on `HttpDeployFiles` over `LoopbackFileServer`): `Every_pinned_file_is_written_with_its_relative_layout` (`docker_compose/docker-compose.yml`, `data/nginx/run-nginx.sh` land at `<dir>/docker_compose/…` and `<dir>/data/nginx/…`; `Downloaded==N`); `A_present_file_with_the_right_hash_is_not_requested_again` (server request log empty, `Present==N`); `A_hash_mismatch_is_004_and_nothing_is_written_for_that_file` (the other good files are written; the error names the path and both hashes' first 12 hex); `A_mismatch_keeps_an_existing_good_file` (local has the good bytes, remote serves other bytes → file unchanged… only reachable when the local hash is wrong, so the case is: local stale bytes, remote wrong bytes → local stale bytes kept); `A_missing_remote_file_is_004`. HTTP-only cases in `HttpDeployFilesTests` (integration lane): `A_redirect_is_refused` (302 to the same server and to `http://example.invalid/`), `A_body_over_1_MiB_is_refused`, `A_non_https_non_loopback_base_is_refused_at_construction`, `A_slow_server_times_out_per_file` (server sleeps past a 200 ms timeout the test sets through an internal constructor parameter) — each `SND-ONYX-004`, nothing written.
- [ ] **Step 2:** run, red. **Step 3:** implement (compare hashes with `CryptographicOperations.FixedTimeEquals` over lowercase hex bytes; never write before the hash matches). **Step 4:** unit and integration lanes green, zero warnings. Commit `feat: IDeployFiles with sha256-verified fetch from the pinned commit`.

### Task 5: Workspace, compose reader, fixtures

**Files:**
- Create: `Core/Onyx/IOnyxWorkspace.cs`, `Core/Onyx/IComposeServices.cs`, `Infrastructure/Onyx/FileOnyxWorkspace.cs`, `Infrastructure/Onyx/YamlComposeServices.cs`, `tests/Aw.Sindri.Testing/{FakeOnyxWorkspace,OnyxWorkspaceContract,FakeComposeServices,ComposeServicesContract}.cs`.
- Create fixtures (small text files copied from `raw.githubusercontent.com` at the pinned commit): `tests/Aw.Sindri.Tests/Fixtures/onyx-v4.9.0/<each lock path>` (the 8 `file` rows: `docker_compose/docker-compose.yml`, `docker_compose/env.template`, `data/nginx/*`), copied to the output directory in the test csproj.
- Test: `tests/Aw.Sindri.Tests/OnyxUp/{FakeOnyxWorkspaceContractTests,FileOnyxWorkspaceContractTests,FakeComposeServicesContractTests,YamlComposeServicesContractTests,OnyxFixturesTests,RealComposeConfigTests}.cs`.

**Interfaces:**
- Produces: `IOnyxWorkspace { string? Read(string path); bool WriteIfChanged(string path, string content, bool secret); OnyxSetupState? ReadState(); void WriteState(OnyxSetupState state); }` — `ReadState` returns null for a missing or corrupt file (never throws); `WriteIfChanged` returns whether it wrote, writes atomically (temp + rename), a `secret` file is created `0600` from the start (`UnixCreateMode`), parents `0700`; `state.json` written through `AwJson` (add `OnyxSetupState` to the package JSON context). `IComposeServices { Result<IReadOnlyList<ComposeService>> Read(string composeText); }` (YamlDotNet `YamlStream`; `HasProfiles` when the service has a `profiles` key; services without `image:` get `Image=null`; invalid YAML → `SND-ONYX-004`).

- [ ] **Step 1: Failing tests.** `OnyxFixturesTests.Every_fixture_matches_its_lock_row` (sha256 of each fixture file equals the `OnyxFilePin` of `EmbeddedOnyxPins`; the set of fixture files equals the lock's file set). Workspace contract: `Write_if_changed_returns_false_for_identical_content`, `A_secret_file_is_0600_and_its_directory_0700` (adapter; skipped on Windows), `A_corrupt_state_reads_as_null`, `State_round_trips`, `A_write_never_leaves_a_partial_file` (adapter: a second writer racing a reader sees old or new, never partial). Compose reader contract: `Reads_the_services_of_the_real_v4_9_0_compose` (fixture: 11 services, names exactly `api_server, background, web_server, inference_model_server, indexing_model_server, relational_db, opensearch, nginx, cache, minio, code-interpreter`; `minio` and `code-interpreter` have `HasProfiles`; `Image` values are the raw uninterpolated strings). **`RealComposeConfigTests`:** feed the fixture compose and the real pins through `OnyxConfig.RenderOverride` for both platforms: success (every compose image resolves to a pinned repository+tag), `ActiveImages` equals the 7 non-profile services' pinned refs, and `OnyxConfig.RenderEnv` over the fixture `env.template` yields the six keys exactly once. This is the test that proves the plan's "every compose image maps to a pin" against real files.
- [ ] **Step 2:** run, red. **Step 3:** implement; register both adapters. **Step 4:** unit lane green, zero warnings. Commit `feat: Onyx workspace, compose reader and the v4.9.0 fixtures`.

### Task 6: Readiness probe port

**Files:**
- Create: `Core/Onyx/IOnyxHealth.cs`, `Core/Onyx/OnyxRoutes.cs`, `Infrastructure/Onyx/HttpOnyxHealth.cs`, `tests/Aw.Sindri.Testing/{FakeOnyxHealth,OnyxHealthContract}.cs`.
- Test: `tests/Aw.Sindri.Tests/OnyxUp/FakeOnyxHealthContractTests.cs`, `HttpOnyxHealthContractTests.cs` (integration; the `Aw.Context.Testing` `FakeOnyxServer` if it already serves `/api/version`, else `LoopbackFileServer` from Task 4).

**Interfaces:**
- Produces: `OnyxRoutes.Health = "/api/health"`, `OnyxRoutes.Version = "/api/version"` (comment: the `/api` prefix comes from nginx, not the backend; confirm on the running stack, and the JSON field of `/api/version`, `backend_version`, against what `OnyxClient` in `Aw.Context` already parses). `public sealed record OnyxProbe(bool Healthy, string? BackendVersion)`; `IOnyxHealth { ValueTask<OnyxProbe> Probe(int port, CancellationToken cancellationToken); }` — never throws: a refused connection or a non-2xx is `Healthy=false`; `/api/version` is asked only once health answers; 3 s timeout per request; `http://127.0.0.1:<port>` only.

- [ ] **Step 1: Failing contract tests:** `Nothing_listening_is_not_healthy`, `A_healthy_server_reports_its_backend_version`, `A_health_answer_with_a_garbled_version_body_is_healthy_with_a_null_version`, `A_5xx_health_is_not_healthy`. **Step 2:** red. **Step 3:** implement; register. **Step 4:** unit and integration green. Commit `feat: IOnyxHealth readiness probe`.

### Task 7: The `OnyxUp` slice (steps 1-5)

**Files:**
- Create: `Core/Onyx/IOnyxProgress.cs`, `Features/OnyxUp/{OnyxUpRequest,OnyxUpResponse,OnyxUpService,OnyxReadiness}.cs`; `tests/Aw.Sindri.Testing/{CapturingOnyxProgress,OnyxUpRig}.cs` (wires every fake: runtime, deploy files, workspace, compose services, health, probes, pins, `FakeTimeProvider`, `FakeSindriHome`).
- Modify: `Features/SindriFeaturesServiceCollectionExtensions.cs` (register), the Core secret-generation seam `IRandomHex` (`Core/Onyx/IRandomHex.cs`, `string Hex(int bytes)`; adapter uses `RandomNumberGenerator`; fake returns a fixed value) — needed so the secret is testable.
- Test: `tests/Aw.Sindri.Tests/OnyxUp/OnyxUpTests.cs`, `OnyxUpResumeTests.cs`, `OnyxReadinessTests.cs`.

**Interfaces:**
- Consumes: Tasks 2-6 ports; `IOnyxPinsSource`, `IHostProbes`, `ISindriHome`, `TimeProvider`.
- Produces: `public sealed record OnyxUpRequest;` `public sealed record OnyxUpResponse(int Port, string Url, string Version, IReadOnlyList<string> Warnings, IReadOnlyList<string> Ran, IReadOnlyList<string> Skipped)`; `IOnyxProgress { void Line(string text); void Tick(); }`; the service is `IService<OnyxUpRequest, OnyxUpResponse>`.
- Behavior, in order: (0) exclusive lock `onyx/.lock` (`FileShare.None`); a second `up` gets `SindriErrors.CommandBusy`. (1) Preflight: `Inspect`, host facts as `OnyxStatusService` does, `OnyxPreflight.Evaluate(…, ownPort: state?.Port when Ps shows nginx running)`; `Blocks` returns the first `Fail` row's `Problem`; each `Warn` row goes to `Warnings` and `progress.Line`. The port used is the report's `Port`, kept from `state.json` on reruns. (2) fetch: skipped when `Steps[fetch]` equals `Stamp(each file's path+sha256)` and the compose file exists; else `IDeployFiles.Fetch`. (3) config: stamp `Stamp(fetchStamp, port, platform, version)`; secret from `OnyxConfig.ReadSecret(existing .env)` else `IRandomHex.Hex(32)`; `WriteIfChanged(.env, secret: true)` and `WriteIfChanged(override)`; skipped when the stamp is unchanged and both files exist. (4) pull: `Pull`, then `RepoDigests(ActiveImages)` must each contain the pinned digest else `SND-ONYX-004` naming image and digest; stamp `Stamp(configStamp)`. (5) start: skipped when `Ps` shows nginx and api_server running and the probe answers the pinned version; else `Up`, then `OnyxReadiness.Wait` (every 2 s via `Task.Delay(interval, clock, ct)`, `progress.Tick()` per poll, deadline 15 min; version compared ignoring a leading `v`; timeout or mismatch is `SND-ONYX-005` with `Ps` summarized as `service: state (health)` joined by `; `). A step is written to `state.json` only after it succeeds; cancellation anywhere rethrows `OperationCanceledException` after the lock is released.

- [ ] **Step 1: Failing tests** (all `unit`, over `OnyxUpRig`):
  - `A_first_run_runs_all_five_steps_in_order` — `runtime.Calls == ["Inspect","ServerPlatform","Pull","RepoDigests","Up", …]`, `Ran == ["fetch","config","pull","start"]`, response `Url == "http://127.0.0.1:3000"`, `state.json` holds all four stamps and port 3000, `.env` contains `HOST_PORT=3000` and the fixed secret, and the override contains `127.0.0.1:3000:80`.
  - `A_second_run_calls_nothing_and_writes_nothing` — after the first run: no `Pull`/`Up`/`Fetch`, `workspace.Writes` empty, `Skipped == all four`, same port, same secret.
  - `A_fail_row_blocks_before_any_fetch` (disk 1 GiB → first call error `SND-ONYX-003`, `Fetch` never called, nothing written); `A_warn_row_does_not_block_and_is_reported` (`MemoryBytes=null`); `A_remote_docker_endpoint_is_010_before_anything` (endpoint `tcp://10.0.0.5:2375`).
  - Resume: `A_hash_mismatch_stops_at_fetch_with_004_and_the_rerun_retries_fetch`; `A_pull_failure_011_leaves_fetch_and_config_done_and_the_rerun_starts_at_pull`; `A_wrong_digest_after_pull_is_004`; `A_start_timeout_leaves_pull_done_and_the_rerun_starts_at_start`; `Cancel_during_the_pull_records_nothing_for_it_and_releases_the_lock`.
  - Idempotent details: `A_changed_lock_row_reruns_fetch_config_pull` (a different pins fixture → every step runs); `A_rerun_keeps_the_port_when_our_own_nginx_holds_it` (probes say 3000 taken, `Ps` shows nginx running on 3000, `state.Port=3000` → no `SND-ONYX-006`, port stays 3000); `A_stopped_stack_with_done_steps_only_runs_start`; `A_lost_secret_in_env_is_regenerated_once_and_kept` (`.env` deleted → new secret written; second run keeps it); `A_corrupt_state_reruns_every_step_and_writes_a_fresh_state`; `A_second_up_while_the_first_holds_the_lock_is_busy`.
  - Readiness (`OnyxReadinessTests`, `FakeTimeProvider`): `Waits_until_health_then_checks_the_version`; `A_wrong_version_is_005_naming_both`; `A_timeout_after_15_minutes_is_005_with_the_ps_summary` (advance the clock 15 min; message contains `api_server: running (unhealthy)`); `Polls_every_2_seconds` (count probes after advancing 10 s).
- [ ] **Step 2:** run, red. **Step 3:** implement. **Step 4:** unit lane green, zero warnings. Commit `feat: sindri onyx up steps 1-5 as the OnyxUp slice`.

### Task 8: `OnyxDown`, and `OnyxStatus` learns Running and Stopped

**Files:**
- Create: `Features/OnyxDown/{OnyxDownRequest,OnyxDownResponse,OnyxDownService}.cs`.
- Modify: `Features/OnyxStatus/{OnyxStatusService,OnyxStatusResponse}.cs`, registration.
- Test: `tests/Aw.Sindri.Tests/OnyxDown/OnyxDownTests.cs`; extend `tests/Aw.Sindri.Tests/OnyxStatus/OnyxStatusTests.cs`.

**Interfaces:**
- Produces: `OnyxDownRequest`; `OnyxDownResponse(bool WasRunning)`; the service refuses a remote endpoint (`SND-ONYX-010`), does nothing when `state.json` has no `start` step ("not set up", `WasRunning=false`, ok), else `Stop`. `OnyxStatusResponse` gains `string? Url`, `string? RunningVersion`, `string? Detail` (the per-state hint); `State` is `NotSetUp` unless `state.json` shows `start` done AND the compose file exists; then `Running` iff `Ps` shows both `nginx` and `api_server` in state `running`, else `Stopped` (also when docker doesn't answer: the docker row explains). When `Running`: `Url=http://127.0.0.1:<state.Port>`, `RunningVersion` from `IOnyxHealth` (`unreachable` when it doesn't answer), the port row via `ownPort`, and `ExitCode` as before (checks only).

- [ ] **Step 1: Failing tests:** `Down_stops_the_project_and_keeps_the_files` (`runtime.Calls` has `Stop`, workspace untouched, no volume removal argument); `Down_on_a_box_never_set_up_is_a_no_op`; `Down_refuses_a_remote_endpoint_with_010`; `Down_reports_a_failed_stop_as_011`; status: `Status_is_running_with_url_and_version_when_nginx_and_api_server_run`, `Status_is_stopped_after_down`, `Status_is_not_set_up_without_a_done_start_step`, `Status_shows_the_own_port_not_taken_while_running`, `Status_is_stopped_when_docker_is_down_and_the_docker_row_fails`.
- [ ] **Step 2:** run, red. **Step 3:** implement. **Step 4:** unit green. Commit `feat: sindri onyx down, and onyx status shows running and stopped`.

### Task 9: Host: `onyx up`, `onyx down`, status text, errors, usage

**Files:**
- Create: `Host/Commands/OnyxVerbs.cs` (up and down), `Host/Commands/ConsoleOnyxProgress.cs` (stderr lines and dots).
- Modify: `Host/Commands/CommandTable.cs` (arms `["onyx","up"]`, `["onyx","down"]` beside `["onyx","status", …]`), `Host/Commands/Verbs.cs` (`OnyxStatus`), `Host/Commands/Usage.cs`, `Host/SindriJsonContext.cs` (`OnyxUpResponse`, `OnyxDownResponse`, `OnyxSetupState`), Host registration of `IOnyxProgress`.
- Test: `tests/Aw.Sindri.Tests/Commands/OnyxUpCommandTests.cs`; extend `OnyxStatusCommandTests.cs`, `UsageTests` expectations.

**Interfaces:**
- Consumes: `AgentSession` (`Human`, `SessionReason`) as `McpVerbs` uses it; `Output`.
- Produces: `internal sealed class OnyxVerbs(IServiceProvider services, Output output, AgentSession session) { Task<int> Up(CancellationToken); Task<int> Down(CancellationToken); }`. A non-human gets `SindriErrors.AuthRefused` "`sindri onyx up` runs Docker and writes Onyx's files, so only a human at a terminal runs it (this CLI ran as a session: <reason>; fix: ask the human to run `sindri onyx up`)", exit 1, nothing called. `Up` success prints on stdout: `url`, `version`, the steps run and skipped, then exactly "Onyx is running. Account setup (sign-up and Sindri's key) comes next: `sindri onyx up` will continue it once that stage lands." Any `Result` error prints through `output.Error` and exits 1 for `SND-ONYX-*` and `CommandBusy`, 2 otherwise. `Verbs.OnyxStatus` maps `Result` errors the same way (no `.Value` dereference), prints `not set up (run \`sindri onyx up\`)` for `NotSetUp`, `running` / `stopped` otherwise, and `url` and `running` version rows when running.

- [ ] **Step 1: Failing tests** (`[Collection(ProcessEnvironment.Name)]`, same rig as `OnyxStatusCommandTests`): `Up_as_a_session_is_refused_with_SND_AUTH_002_and_calls_nothing` (CLAUDECODE set); `Up_without_a_terminal_is_refused`; `Up_prints_url_version_and_the_P3_hand_over_line_on_stdout_and_progress_on_stderr`; `Up_maps_SND_ONYX_003_to_exit_1_with_the_fix_text`; `Down_as_a_session_is_refused`; `Down_prints_stopped_and_exits_0`; `Status_text_for_not_set_up_ends_with_run_sindri_onyx_up` (replaces the P1 expectation `onyx      not set up`); `Status_running_has_url_and_running_version_rows`; `Status_maps_a_Result_error_to_an_error_line_and_exit_1`; `Usage_lists_onyx_up_and_down`. No catalogue rows (precedent: `mcp register --apply`).
- [ ] **Step 2:** run, red. **Step 3:** implement. **Step 4:** unit lane green, zero warnings. Commit `feat: sindri onyx up and down verbs`.

### Task 10: Doctor `onyx:stack`

**Files:**
- Create: `Features/RunDoctor/OnyxStackDoctor.cs`.
- Modify: `Features/RunDoctor/RunDoctorService.cs` (appended after the `profile.Context` rows, before the harness rows), registration, `docs`-visible check name list in `DoctorRules` if one exists.
- Test: `tests/Aw.Sindri.Tests/Health/OnyxStackDoctorTests.cs`.

**Interfaces:**
- Produces: `internal sealed class OnyxStackDoctor(IOnyxWorkspace workspace, IContainerRuntime runtime, IOnyxPinsSource pins, ISindriHome home) { ValueTask<DoctorCheck?> Check(CancellationToken) }` returns null (no row) when `state.json` doesn't exist. Rows: docker not answering → `Warn` ("docker isn't running; the Onyx stack can't be read", fix: start Docker); stack set up but no container running → `Warn` "stopped" fix `sindri onyx up`; otherwise `Fail` for, in this order: a container not `running` or with `Health` of `unhealthy` (names the service), any publisher whose `Address` isn't `127.0.0.1` (names service, address and port; fix: `sindri onyx down`, then report it — Sindri never writes such a binding), any container whose `Image` isn't the lock's `<repository>@<digest>` for this platform; else `Ok` "N containers running, healthy, bound to 127.0.0.1:<port>, digests match the pins".

- [ ] **Step 1: Failing tests** (fake runtime `Containers`): `No_state_file_no_row`; `Healthy_loopback_pinned_stack_is_ok`; `A_0_0_0_0_publisher_fails_naming_the_service_and_port`; `An_ipv6_wildcard_publisher_fails`; `An_unhealthy_container_fails`; `A_container_on_a_different_digest_fails`; `A_stopped_stack_warns_with_the_fix`; `Docker_down_warns`; `The_doctor_run_includes_the_row_after_context_rows_only_when_state_exists`.
- [ ] **Step 2:** run, red. **Step 3:** implement. **Step 4:** unit green. Commit `feat: sindri doctor onyx:stack`.

### Task 11: `HostProbes.CanBind` and the heavy-lane real run

**Files:**
- Modify: `Infrastructure/Onyx/HostProbes.cs`.
- Create: `tests/Aw.Sindri.Tests/OnyxUp/OnyxRealStackTests.cs` (`heavy`).
- Test: `tests/Aw.Sindri.Tests/OnyxStatus/HostProbesTests.cs` (extend).

**Interfaces:**
- Decision (carried item): `CanBind` uses `ExclusiveAddressUse = OperatingSystem.IsWindows()`: on Unix `false` sets SO_REUSEADDR so a port in TIME_WAIT after `onyx down` reads free, and the two connect probes (which don't depend on it) remain the authority that nothing listens. Document this on the method.

- [ ] **Step 1: Failing test** `A_port_in_time_wait_reads_free` (`integration`, skipped on Windows via `Assert.SkipWhen`): listen on a free loopback port, connect a client, accept, close the server side first (so the server's end is in TIME_WAIT), close the listener, then `HostProbes.Ports(thatPort, …).PreferredFree` is true. Run to see it fail on Linux (macOS may already pass: the test still stays).
- [ ] **Step 2:** implement the one-line change and its comment; green.
- [ ] **Step 3: `OnyxRealStackTests`** (`[Fact, Trait("Lane","heavy")]`): `Assert.SkipWhen` Docker CLI or daemon is missing, or fewer than the pins' `DiskBytes` free at the temp home. On a temp `AW_SINDRI_HOME` (real adapters through `AddSindriInfrastructure`/`AddSindriFeatures`): `OnyxUp` runs, asserts the response url, `GET /api/version` equals `v4.9.0`, `Ps` shows nginx published on `127.0.0.1` only, `OnyxStackDoctor` is `Ok`; a second `OnyxUp` is all-skipped; `OnyxDown` then `Ps` shows nothing running and `OnyxStatus.State == Stopped`; `finally` runs `docker compose -p sindri-onyx down -v` on the temp project (the test's own cleanup, not a product behavior). The test is not run by this plan's tasks; the orchestrator runs it.
- [ ] **Step 4:** unit and integration lanes green (the heavy test compiles and skips or is filtered out), zero warnings. Commit `fix: HostProbes.CanBind reads a TIME_WAIT port free; heavy-lane real onyx up/down test`.

### Task 12: Docs, aot-smoke, deps check

**Files:**
- Modify: `docs/csharp/sindri.md` (Commands table: `onyx up`, `onyx down`, the updated `onyx status` row and `doctor` list gaining `onyx:stack`; the external-executables paragraph: `docker` now also runs `compose pull|up|stop|ps`, `image inspect`, `context inspect`, `version --format`, and the file fetch from `raw.githubusercontent.com`; the errors table: `SND-ONYX-004` now also "a downloaded file or image failed its pin or a compose image has no pin", plus `SND-ONYX-005`, `-010`, `-011`), `docs/sindri/onyx.md` (a short note at the top of §2: `sindri onyx up` does steps 1-5 of this page; sign-up and the key stay manual until P3; the bots-off sed approach replaces the copy-out-and-mount), `AGENTS.md` Commands (one line each for `onyx up|down|status`), `src/Sindri/aot-smoke/run.sh` and `expected.txt`.
- Modify: `context/onyx.lock` header comment only if the measured pull and model download differ from the `need` rows (the orchestrator's real run supplies the numbers; leave the rows unchanged here).

- [ ] **Step 1:** aot-smoke: add `run onyx status --json` and `run onyx status` with `PATH` reduced to a scratch dir holding only `jq`'s directory and coreutils, no `docker` (a `PATH=` prefix on the call as the script already does for `doctor`); assert `.state == "not-set-up"` and the `docker` row `fail` with `SND-ONYX-001` in `expected.txt` through `jq` so the output is deterministic (no disk or port numbers: print only `{state, docker: (.checks[]|select(.name=="docker")|.state)}`). Also `onyx up` as the scripted session (CLAUDECODE set): exit 1 with `SND-AUTH-002`.
- [ ] **Step 2:** `node scripts/check-deps.mjs` (no new package edge expected: zero changes to `deps.yaml`), `scripts/check-env-example.sh` (no new env var; `DOCKER_HOST` is read by the adapter: add it to `.env.example` if the check reports it), `bash scripts/tests/check-profile-doc.test.sh` unaffected.
- [ ] **Step 3:** `bash src/Sindri/aot-smoke/run.sh` once (needs the pinned temporal; if unavailable on the box, say so in the commit body and leave it to CI). Commit `docs: Onyx P2 commands, errors, external executables; aot-smoke runs onyx status with no docker`.

---

## Self-review

- **Spec coverage:** §4.1 steps 1-5 (Tasks 2, 4, 5, 7), §4 `down`/`status` (8, 9), §5.4 pins and digests (2, 4, 7), §8 codes 004/005 plus the new 010/011 (2, 3, 7), doctor `onyx:stack` (10), §10 unit/integration/heavy (every task; 11), §9 ports and fakes with contracts (3-6). Steps 6-10, `open`, `serve`/`stop`, `control.json` are P3/P4 and absent on purpose. Carried items: digest-pinned run (2, 7), non-local context (2, 3, 7), runner move (1), compose-image-to-pin (2, 5), Warn `Blocks` (2), `CanBind` (11), aot-smoke (12), Verbs Result mapping (9), docs (12).
- **Types:** `ComposeProject`, `ComposeContainer`/`ComposePublisher`, `OnyxSetupState`/`OnyxSteps`, `EnvSettings`, `OnyxPaths`, `IOnyxWorkspace`, `IComposeServices`, `IDeployFiles`/`DeployFetch`, `IOnyxHealth`/`OnyxProbe`, `IOnyxProgress`, `IRandomHex` are defined once and used with the same names in Tasks 7-11.
- **Proportion:** signatures, test names and exact values only; the one body given (the bots-off command) is exact copy.
