---
description: C# tests — TDD order, lanes and time budgets, fakes with contract tests, builders, golden files, architecture tests
globs:
  - "tests/**"
  - "**/*Tests.cs"
  - "**/*.Tests.csproj"
  - "**/*.Testing.csproj"
paths:
  - "tests/**"
  - "**/*Tests.cs"
  - "**/*.Tests.csproj"
  - "**/*.Testing.csproj"
alwaysApply: false
---

# C# Testing Rules

Source of truth: spec §4b.5 (`docs/superpowers/specs/2026-10-09-agent-readiness-design.md`). The TypeScript test rules (`.agents/rules/testing.md`) still apply to the TS packages until the cutover.

## Red first

- Every slice starts with a failing test that names the behavior: `StartItem_parks_when_budget_is_exhausted`.
- The commit history shows the red test before the code (the `/specToProvenPR` red-commit rule). `/new-slice` generates that red test first.

## Organization

- Tests by **feature**, not by layer: `StartItemTests`, not `ItemRepositoryTests` + `ItemServiceTests`.
- Per package: `tests/<Pkg>.Tests/` (by feature) and `tests/<Pkg>.Testing/` (fakes, builders, contract bases). One assembly, `tests/Aw.Architecture.Tests/`, holds every §4b.1 rule.

## Lanes

Lanes are xUnit traits named `Lane`. CI fails a test that runs over its lane's budget.

| Lane | What | Budget |
|---|---|---|
| `unit` | `Core` logic and services with in-memory fakes; architecture tests. No I/O | < 10 s per package |
| `integration` | Repositories on a real temp SQLite file; endpoints through `WebApplicationFactory` | < 60 s per package |
| `heavy` | Real containers, browsers, index builds | nightly or run alone (heavy-job lock) |

```csharp
[Fact, Trait("Lane", "unit")]
public void StartItem_parks_when_budget_is_exhausted() { … }
```

Run a lane (xUnit v3 on Microsoft.Testing.Platform, which needs `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`):

```bash
dotnet test --filter-trait "Lane=unit"
dotnet test --filter-not-trait "Lane=heavy"
```

The test framework is xUnit v3 unless the AOT spike picks TUnit (decision R7, `docs/readiness/aot-spike.md`); if it changes, this section changes with it.

## Fakes, not mocks

- Each port has **one** in-memory fake in `<Pkg>.Testing`. No mocking libraries (Moq, NSubstitute, FakeItEasy): they're not in `Directory.Packages.props` and review rejects them.
- A generic contract-test base (`ReadRepositoryContract<TEntity, TId>`, `ItemRepositoryContract`) runs the **same tests against the fake and the SQL implementation**, so the fake can't drift. Every new port gets its contract base in the same PR as its fake.

## Test data

- Builders with sensible defaults: `AnItem().WithSize(S).Build()`.
- Deterministic ids (a fake `IIdGenerator`) and `FakeTimeProvider`; never the wall clock or `Guid.NewGuid` in a test.
- No shared mutable fixtures; each test builds its own state.
- Golden files for CLI output and JSON contracts. Compare after the declared normalization (ids, times), never by loose matching. Golden files recorded from the TS build are the port's contract (§4c): don't edit them to make a C# test pass — a real difference is a bug in the port.

## Architecture tests

ArchUnitNET (not NetArchTest), in the `unit` lane: dependency directions, no slice-to-slice references, `internal` row types, no namespace cycles, naming (no `Common`/`Utils`/`Helpers`). Each test's failure message names the rule id, what it found and the fix.

## Hosts

Every host has a DI validation test that builds the container with `ValidateOnBuild` and `ValidateScopes`.
