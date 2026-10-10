# AGENTS.md — Agentic Workflow

> Agentic Workflow — provider-agnostic agent workflow toolkit for Claude Code, Codex, and Cursor: 50 native skills + 3 fetched external packs (impeccable, emil-design-eng, taste-skill), config archive, repo bootstrapper, MCP bridge for multi-agent communication, and token-efficiency tools (rtk + headroom).

Domain-specific rules live in `.agents/rules/` — one file per rule, the only copy. `.claude/rules` and `.cursor/rules/*.mdc` are symlinks to it (auto-loaded by Claude Code and Cursor), and `CLAUDE.md` is a symlink to this file. Codex reads the Rules Index at the bottom of this file. After adding or removing a rule, run `scripts/sync-rules.sh`.

## Required Context

| Document | Purpose |
|----------|---------|
| `planning/ARCHITECTURE.md` | System components and data flow |
| `planning/PROVIDERS.md` | Claude Code / Codex / Cursor support matrix and canonical layout |
| `planning/API_CONTRACT.md` | MCP bridge REST & tool schemas |
| `planning/CODE_STYLE.md` | TypeScript conventions and patterns |
| `planning/TESTING.md` | Test strategy and coverage targets |
| `planning/ERD.md` | SQLite schema and relationships |
| `skills/_shared/capabilities.md` | Capability → tool map used by all skill text |

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | Node.js >= 20, ES2022 target |
| Language | TypeScript 5.7, strict mode |
| HTTP (bridge) | Fastify 5 |
| Database | SQLite via better-sqlite3, WAL mode |
| MCP | @modelcontextprotocol/sdk (stdio transport) |
| LSP (Serena) | Docker (Dockerized Serena MCP server via `scripts/serena-docker`) |
| Validation | Zod 3 |
| Test | Vitest (in-memory SQLite) |
| Build | tsc (ESM, Node16 module resolution) |

**C# build — in progress, not installed until the cutover.** The TypeScript packages are being ported to C# (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a–§4e). The installed toolkit stays TypeScript until every package passes its golden tests against the C# build; then one reversible switch moves the install. Nothing in `setup.sh`, the providers, MCP registration or launchd points at `src/` before that.

| Layer | Technology (C# build) |
|-------|------------|
| Runtime | .NET 10 (LTS), SDK pinned in `global.json`; NativeAOT for hook and CLI entry points |
| Language | C# (latest), nullable on, warnings as errors |
| HTTP (bridge) | ASP.NET Core minimal APIs (`CreateSlimBuilder`) |
| Database | `Microsoft.Data.Sqlite`, same files and schemas, WAL mode |
| MCP | Official MCP C# SDK |
| JSON | System.Text.Json, one source-generated context per package |
| Analyzers | NetAnalyzers, Meziantou, Roslynator, BannedApiAnalyzers, PublicApiAnalyzers (one owner per rule, `.editorconfig`) |
| Test | xUnit v3 on Microsoft.Testing.Platform (default; decision R7 may pick TUnit), lanes `unit` / `integration` / `heavy` |
| Packages | Central Package Management (`Directory.Packages.props`), locked restore, nuget.org only (`nuget.config`) |

## Directory Structure

```
agentic-workflow/
├── skills/        # 49 provider-neutral skills (linked into each provider's skills dir)
├── bootstrap/     # /bootstrap skill — repo documentation generator
├── config/        # Settings, MCP config, statusline, safety hooks, sindri-nudge SessionStart hook (+ hooks/adapters/ per provider)
├── providers/     # Per-provider installers (claude, codex, cursor)
├── mcp-bridge/    # MCP bridge + REST API (Fastify, SQLite)
├── scorer/        # Daily cost/involvement report from provider session transcripts
├── judge/         # Cheap-agent-harness judge CLI (decisions, config, health)
├── sindri/        # Sindri core: profile, ledger, lock, scrubber, plan-file tracker, observe, doctor (CLI)
├── mods/          # Claude Code mods (in-process function hooks): aw-live /live pane. Claude-only
├── planning/      # Project documentation
├── src/           # C# build (in progress): Aw.Kernel, Aw.Analyzers, then one package per port — Core · Features · Infrastructure · Host
├── tests/         # C# tests: Aw.<Pkg>.Tests (by feature), Aw.<Pkg>.Testing (fakes, builders, contract bases), Aw.Architecture.Tests
├── spikes/        # Throwaway C# spikes (AOT spike); never part of the solution
├── global.json · AgenticWorkflow.slnx · Directory.Build.props · Directory.Packages.props · nuget.config · BannedSymbols.txt
├── .agents/rules/ # Glob-scoped domain rules (.claude/rules and .cursor/rules link here)
├── .serena/       # Serena LSP project configuration
├── scripts/       # Utility scripts (sync-rules.sh, serena-docker, probe.sh, install-scorer.sh)
└── setup.sh       # One-command setup for every installed provider
```

## Commands

```bash
# TypeScript packages (each has test, test:coverage, typecheck, build)
cd mcp-bridge && npm test               # Vitest, in-memory SQLite
cd scorer && npm test                   # Vitest
cd judge && npm test                    # Vitest
cd sindri && npm test                   # Vitest
(cd skills/ui-evidence && npm test)     # Vitest (skill package; includes a real-browser test)
(cd skills/bugFixOrchestrator && npm test)  # Vitest (bugfix-state CLI)
claude plugin validate mods/aw-live && claude plugin test mods/aw-live  # the aw-live mod (needs claude >= 2.1.289)
scorer --since 7d [--provider claude|codex|cursor|all]  # Report to ~/.agentic-workflow/scorer/reports/
scorer live --session <id> [--cwd DIR] [--json]  # This session's numbers (the aw-live mod calls it)
scorer probe                            # Summarize the hook-input probe
scorer audit [--since 60d; default 1d] [--items FILE] [--max-size XS] [--label N]   # Human-turn baseline → ~/.agentic-workflow/audit/; --label sends sampled turn text to your Claude login's provider

# C# build (in progress; the merge-gate lines below apply once src/ has projects)
dotnet build -warnaserror               # Whole solution (AgenticWorkflow.slnx); zero warnings
dotnet test --filter-trait "Lane=unit"  # Unit lane (< 10 s per package); xUnit v3 on Microsoft.Testing.Platform
dotnet test --filter-trait "Lane=integration"   # Temp SQLite files, WebApplicationFactory (< 60 s per package)
dotnet test --filter-not-trait "Lane=heavy"     # Everything except containers, browsers and index builds
dotnet format --verify-no-changes       # Style and analyzer fixes are applied (CI and pre-commit)
scripts/dotnet-verify-packages.sh       # Lock files present and unchanged, signature-verified locked restore, dotnet nuget verify --all
# Add a package: /new-package (spec docs/csharp/scaffolds/new-package.md)
# Add a use case: /new-slice (spec docs/csharp/scaffolds/new-slice.md) — test, request and response, service, endpoint, registration

# Rules (edit .agents/rules/, then regenerate)
scripts/sync-rules.sh                   # Link .claude/rules, .cursor/rules, CLAUDE.md; refresh Rules Index
scripts/sync-rules.sh --check           # Exit 1 if links or the Rules Index are out of date

# Bash tests
bash providers/tests/install.test.sh
bash scripts/tests/sync-rules.test.sh
bash scripts/tests/probe.test.sh
bash scripts/tests/find-duplicate-skills.test.sh
bash scripts/tests/install-scorer-audit.test.sh
bash scripts/tests/install-sindri.test.sh
bash config/hooks/tests/codex-adapter.test.sh
bash config/hooks/tests/cursor-adapter.test.sh
bash config/hooks/tests/provider-install-hooks.test.sh
bash config/hooks/tests/probe-log.test.sh
bash config/hooks/tests/judge-health.test.sh
bash config/hooks/tests/sindri-nudge.test.sh
for t in config/lib/tests/*.test.sh; do bash "$t" || echo "FAILED: $t"; done  # merge-hook, levers, installers

# Levers and probe (default provider: claude)
scripts/install-<lever>.sh --provider claude|codex|cursor
scripts/probe.sh [--provider claude|codex|cursor] on|off|status
scripts/install-scorer.sh               # Build scorer, install CLI + launchd job
sindri doctor                           # Sindri health checks (ok / warn / fail + fix)
sindri index setup && sindri index build    # code index (Ollama + graphify, offline)
sindri evolve init && sindri evolve check --changed   # registry + module eval suites (heavy: run alone)
sindri evolve status                                   # artifacts, proposals, merge rate, corpus
sindri evolve reflect --pr <n>                         # proposals from a merged PR
sindri evolve weekly                                   # the Monday job (launchd runs it)
sindri evolve publish                                  # staged proposals -> this week's plan file (on a branch)
sindri repo onboard [<path>]            # add + approval check + pre-commit hook + first index build (never approves)
sindri scope <brief.md> --out DIR            # cited scope map; --backtest linear:<project> for recall, precision and a baseline
sindri shape report                      # record-only shape signals and their outcomes (read-only)
sindri shape reconcile                   # move the spool into the ledger and label outcomes now (observe does it hourly)
sindri observe                          # Backlog with sizes; records to the ledger when the profile is approved
scripts/install-sindri.sh               # Build sindri, install the CLI wrapper (or ./setup.sh --with-sindri)

# Setup (from repo root)
./setup.sh --providers claude,codex,cursor --dry-run   # Print every change, write nothing
./setup.sh [--providers ...]            # Default: detect installed CLIs. Links skills, installs
                                        # hooks + levers + MCP servers per provider, builds bridge,
                                        # judge, scorer, Serena image
```

After installing for Codex, open `codex` and run `/hooks` to trust the `aw:*` hooks.

## Merge Gate

Before merging any PR:
1. `npm run typecheck` passes with zero errors in `mcp-bridge`, `scorer`, `judge`, `sindri`, `skills/ui-evidence`, and `skills/bugFixOrchestrator`
2. `npm test` passes in `mcp-bridge`, `scorer`, `judge`, `sindri`, `skills/ui-evidence`, and `skills/bugFixOrchestrator`, every bash test listed above passes, and `claude plugin validate mods/aw-live` and `claude plugin test mods/aw-live` pass
3. `scripts/sync-rules.sh --check` passes (rule links and Rules Index match `.agents/rules/`)
4. `./setup.sh --providers claude,codex,cursor --dry-run` runs cleanly
5. No `/* v8 ignore */` annotations in source files (prohibited — write the test instead)
6. No `any` types outside of Fastify integration boundaries
7. C# build (active once `src/` has projects): `dotnet build -warnaserror` reports zero warnings, `dotnet test` passes in every lane CI runs, `dotnet format --verify-no-changes` is clean, `scripts/dotnet-verify-packages.sh` passes, and the ratchet baseline block at the end of `.editorconfig` didn't grow

## C# slice recipe

To add a use case to a C# package: **test, request and response, service, endpoint, registration.**

1. Write the failing test that names the behavior (`StartItem_parks_when_budget_is_exhausted`) in `tests/Aw.<Pkg>.Tests/<Slice>/`, together with the request and response records and a service stub with no constructor parameters whose `Handle` throws `NotSupportedException`, in `src/<Pkg>/Aw.<Pkg>.Features/<Slice>/`. Commit it red: it compiles with zero warnings and fails when run.
2. Write the real assertion; add the ports the service needs to its constructor.
3. Implement the service (`IService<TRequest, TResponse>`); pure decisions live on the domain type in `Core`.
4. Map the endpoint (`Host/Endpoints/`) or CLI command (`Host/Commands/`): bind, call one service, map the `Result`.
5. Register it explicitly in the project's `Add…()` method; add the types to the package's JSON context.

`/new-slice` generates step 1 and the stubs for steps 4–5. Full rules: `.agents/rules/csharp.md` and `.agents/rules/csharp-testing.md`.

## Commit Conventions

Format: `type: short description`

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`

Keep commits atomic — one logical change per commit. See `planning/COMMIT_STRATEGY.md` for details.

<!-- === RULES INDEX START (generated by sync-rules.sh — edit .agents/rules/, then run scripts/sync-rules.sh) === -->
## Rules Index

Domain rules live in `.agents/rules/`. Before editing a file, read every rule below whose globs match it ("always" rules apply to every task). Claude Code and Cursor load them automatically through `.claude/rules` and `.cursor/rules/` links; Codex does not — read them yourself.

| Rule | Description | Applies to | Path |
|------|-------------|------------|------|
| `bridge-services` | MCP bridge service layer — AppResult, service contracts, MCP tools, route registration | `mcp-bridge/src/application/**`, `mcp-bridge/src/routes/**`, `mcp-bridge/src/server.ts`, `mcp-bridge/src/mcp.ts`, `mcp-bridge/src/index.ts` | `.agents/rules/bridge-services.md` |
| `bridge-transport` | MCP bridge transport layer — typed routes, controller factories, Zod schemas | `mcp-bridge/src/transport/**` | `.agents/rules/bridge-transport.md` |
| `csharp-testing` | C# tests — TDD order, lanes and time budgets, fakes with contract tests, builders, golden files, architecture tests | `tests/**`, `**/*Tests.cs`, `**/*.Tests.csproj`, `**/*.Testing.csproj` | `.agents/rules/csharp-testing.md` |
| `csharp` | C# build (in progress) — package shape, dependency rules, types, errors, DI, JSON, AOT scope, analyzers | `src/**`, `**/*.cs`, `**/*.csproj`, `**/*.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `nuget.config`, `BannedSymbols.txt`, `deps.yaml`, `schemas/deps.schema.json`, `**/.editorconfig`, `CodeMetricsConfig.txt`, `**/PublicAPI.*.txt`, `**/packages.lock.json` | `.agents/rules/csharp.md` |
| `database` | Bridge SQLite database — client, prepared statements, schema, transactions | `mcp-bridge/src/db/**` | `.agents/rules/database.md` |
| `design` | Design pipeline artifacts — tokens, .impeccable.md, DESIGN_SYSTEM.md, design-* skills | `design-tokens.json`, `.impeccable.md`, `planning/DESIGN_SYSTEM.md`, `skills/design-*/**` | `.agents/rules/design.md` |
| `hooks` | Safety and context hooks — per-provider adapters, protocols, installation | `config/hooks/**`, `config/settings.json` | `.agents/rules/hooks.md` |
| `mcp-servers` | MCP servers registered globally with every provider — available in every session | always | `.agents/rules/mcp-servers.md` |
| `skills` | Skill structure, preamble, output directories, pipeline, and installation | `skills/**`, `bootstrap/**` | `.agents/rules/skills.md` |
| `testing` | Test infrastructure, shared helpers, and coverage policy | `**/*.test.ts`, `**/*.spec.ts`, `**/vitest.config.ts`, `mcp-bridge/tests/helpers.ts` | `.agents/rules/testing.md` |
<!-- === RULES INDEX END === -->
