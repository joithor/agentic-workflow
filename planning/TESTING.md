# Testing Strategy

## Overview

All tests run against **in-memory SQLite** databases. Each test gets a fresh database instance via `beforeEach`, so tests are fully isolated, deterministic, and fast -- no filesystem cleanup or shared state.

The test suite uses **Vitest** with explicit imports (globals are disabled in the config).

## Coverage Policy

**`/* v8 ignore */` annotations are prohibited.** Never use `/* v8 ignore next */`, `/* v8 ignore start */`, or any v8 ignore variant to reach coverage targets. Coverage must be earned through real tests, not hidden with annotations.

`scorer`, `judge`, and `sindri` each set 100% line/function/branch/statement thresholds in `vitest.config.ts`, so `npm run test:coverage` fails below 100%. Gaps are closed by writing the missing tests, not by ignoring lines.

| Metric | Target (all three packages) |
|--------|----------------------|
| Statements | 100% (through real tests) |
| Branches | 100% (through real tests) |
| Functions | 100% (through real tests) |
| Lines | 100% (through real tests) |

Entry-point files are excluded from coverage collection: `src/cli.ts` in `scorer` and `judge`, and `src/cli.ts`, `src/gen.ts`, `src/system-real.ts` and `src/git-real.ts` in `sindri`.

## Running Tests

```bash
cd scorer              # or judge, sindri
npm test               # Vitest single run (CI-friendly)
npm run test:coverage  # Run with coverage report (fails below 100%)
npm run typecheck      # tsc --noEmit

```

## Test Patterns

### Coverage Gaps

Some paths are hard to exercise in unit tests (process entry points, real timers, filesystem races). Keep them out of `src/**` logic where possible (entry points are excluded above) and test the rest directly — never annotate them away.

`/* v8 ignore */` annotations are prohibited in this codebase.

## Writing New Tests

1. Add test files to the package's `tests/` directory with `.test.ts` suffix.
2. Import from `vitest` explicitly (globals are off).
3. Never add `/* v8 ignore */` annotations — write the test instead.
