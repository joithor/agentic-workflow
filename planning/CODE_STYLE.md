# Code Style Guide

Two codebases live in this repo during the port (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md`
§4a):

- **C#** (`src/`, `tests/`) is where new code goes. Its conventions of record are `.agents/rules/csharp.md` and
  `.agents/rules/csharp-testing.md`, enforced by analyzers, `dotnet format` and the architecture tests. This guide
  doesn't repeat them.
- **TypeScript** (`scorer/`, `judge/`, `sindri/`, `skills/ui-evidence/`, `skills/bugFixOrchestrator/`) takes bug fixes
  only until the switch. The rules below keep those fixes consistent with the code around them.

---

## TypeScript Strict Mode

Every package compiles with `"strict": true` (`npm run typecheck`). Shared compiler settings:

| Flag | Value | Effect |
|------|-------|--------|
| `strict` | `true` | Enables all strict type-checking options |
| `target` | `ES2022` | Top-level await, `Array.at()`, etc. |
| `module` / `moduleResolution` | `Node16` | ESM with `.js` extensions in imports |
| `forceConsistentCasingInFileNames` | `true` | Prevents cross-platform casing bugs |

Each `package.json` declares `"type": "module"`, so **relative imports use the `.js` extension**, even when importing
`.ts` source files:

```ts
// Correct
import { openLedger } from "./ledger/db.js";

// Wrong - fails at runtime
import { openLedger } from "./ledger/db";
```

No `any`: use `unknown` with runtime narrowing, or a real type (merge gate 7, `node scripts/ci/no-any.mjs`; existing
uses are held in `scripts/ci/no-any-baseline.json`, which only shrinks).

---

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Files and directories | `kebab-case` | `format-health.ts`, `prompt-sort/` |
| Interfaces and type aliases | `PascalCase` | `LoadedProfile`, `FormatHealth` |
| Functions | `camelCase` | `newHealth`, `openLedger` |
| Constants | `UPPER_SNAKE_CASE` for fixed values and tables | `ERRORS`, `PRE_COMMIT_MARKER` |
| Zod schemas | `PascalCase` + `Schema` suffix, with a companion `type` of the inferred shape | `ProfileSchema`, `type Profile = z.infer<typeof ProfileSchema>` |

---

## Imports

Use `import type` for symbols used only in type position. Order: external packages, then internal modules, type-only
imports first within each group.

---

## Tests

Vitest with v8 coverage at 100% lines, functions, branches and statements (`npm run test:coverage`); `/* v8 ignore */`
is prohibited. A bug fix comes with the regression test that failed before it. Test helpers and fixtures per package are
described in `.agents/rules/testing.md`.

---

## Comments

Use `/** JSDoc */` on exported interfaces and functions. Inside a long file, separate logical sections with a header:

```ts
// ── Row types ──────────────────────────────────────────────
```
