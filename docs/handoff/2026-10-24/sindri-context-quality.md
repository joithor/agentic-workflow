# Sindri context-quality — remaining work (2026-10-24)

Take over from here. The original findings are below. Part of them is already on
[pull request 938](https://github.com/jthorare/agentic-workflow/pull/938). This file is the rest.

Push to `jfork` (`joithor/agentic-workflow-jthorare`) and open pull requests with
`gh pr create -R jthorare/agentic-workflow --head joithor:<branch>`.

Main when this was written: `51f5158a3`. Do not commit on `main`. The checkout at
`/Users/joi/personal/agentic-workflow` stays on `main` and has an untracked `.playwright-mcp/`; leave that directory out of every commit.

## Already on #938 — do not redo

Branch `bugfix/sindri-context-quality-a3`, head `68710ffbc`, remote `jfork`. Base new code on that branch until #938 merges, then base on `main`. Another session is championing #938. Do not merge it, review it, or force-push it.

| Already done | Where |
|---|---|
| Tests, specs, fixtures, demos, icons, deprecated trees, shells, routers, and crons are not feature implementers and do not claim surfaces. The globs are case-insensitive. `**/` is any directories and `*` stays in one segment. | `ClusterFeatures._sharedRoles` |
| A query budget is split across the sections that have items. Unused share rolls forward. Each overflow cite is listed once. Fill order stays graph, affects, decisions, code, records. | `Packer` |
| After exact key, alias, and one exact label, a feature matches when every significant word (length at least 3, two or more words) sits in exactly one feature label. Suggestions sort test, spec, and fixture keys and labels after other nodes that share those words. | `AnchorMatch` |
| Every MCP tool takes `limit` and `offset`. Omit both for the whole answer. Pass them and each list keeps that window, `page.more` says another page follows, and counts stay the full totals. `sections` and `groups` stay whole. `context_lenses` pages in the service so HTTP, CLI, and MCP share one window. The 48,000-character cap was removed on purpose. Do not put it back. | `McpPages`, `LensPages`, `QueryRules.CheckPage` |

Those role globs stay. Do not replace them with `vitalizecare/web-app` paths.

A map rebuild is required before the role globs show up in the live graph. `sindri context map build` is the owner's command (`SND-AUTH-002` refuses a session).

## Still to fix

### 1. Catch-all files the globs miss

#938 skips the roles above. These paths from the live map still fall outside the globs, so they can still own a feature:

- `routes/_user-only/-components/dashboard-layout.tsx`
- `FractionDisplay`

Shared admin shells whose path contains `shell` are already skipped. Confirm that against a rebuilt map rather than adding web-app paths.

### 2. No configurable ignore list

There is no way to exclude files by prefix, suffix, or filename regex. The role globs are fixed in code and run only when a cluster is proposed. Queries do not apply an ignore list.

The ignore config has to support prefix, suffix, and regex, and it has to apply both when the map is built and when a query is answered. Keep it general. The examples that motivated it are `packages/icons/**`, `packages/ui-deprecated/**`, `**/__tests__/**`, `*.unit.test.ts(x)`, `*.test.ts`, `**/Demo/**`, `*fixture*`, routers, and cron registries. Those examples are not the config.

### 3. Auto-clustered names and confirmed labels

Many features are named `root.package-NN` (14 through 36). Labels do not match their keys, and they are stored as `provenance: human, confirmed: true`:

| Key | Label |
|---|---|
| `centralstaffingaiassistantpanel` | Unit card metrics view |
| `bonusdialog` | Unit scheduling step configuration |
| `root.package-18` | Rule engine editor |
| `demo-open-shift-post-metrics-fixture` | Hospital staffing grid |
| `fixtures` | Staffing groups admin |
| `useronlyroutepending` | Staffing levels grid |

Find what marks an auto-clustered label confirmed. Do not hand-edit this repo's map.

### 4. Ranking, and coverage

#938 shares the budget and dedupes `more`. It does not rank product surfaces ahead of generic ones before the budget is spent. Within a section, items stay in the order they were given.

`coverage.gaps == coverage.scope` (292/292 on `feature:hospitalshifttimes`) was left as intent uncovered. Confirm that before changing coverage.

`context_lenses` without `limit` still returns the whole answer and can exceed the MCP transport. That is the paging contract. Callers pass `limit` and `offset`. Do not add a silent character cap.

### 5. What the token budget consumes

Answer these in the code or the docs the query command points at:

- What the `tokens` estimate on each item counts.
- Whether any model call is involved. `provenance: deterministic` suggests none. Confirm it.
- The default when `budget` is omitted.
- The crowding of records by graph items is already the even share on #938. Do not re-explain it by changing `FillOrder`.

### 6. Onyx — owner, not a session

Live failure, unchanged by #938:

- `SND-CONTEXT-050`: Onyx v4.9.0 on `127.0.0.1:3000` refused the key on `GET /api/me` (HTTP 403). The key is missing, wrong, or lacks the admin role `POST /api/admin/search` needs.
- The same message says sessions do not search it until `context.repoTeams` maps its teams to a repo.
- `lastSync: null`, `records: 0`.
- Onyx is on port 3000, the same port as the web-app dev server. A Cursor instance has also been seen there. Check what actually answers before treating the 403 as only a bad key.

The owner sets `context.sources.onyx.token` to a dedicated admin service-account key, re-approves the profile (spec §8.8), sets `context.repoTeams`, and uses a port that does not clash with 3000. A session cannot approve the profile. Print the commands and stop.

### 7. Concept nodes

#938 adds the single-feature word match. It does not seed concept nodes. `context_node` for `Core / On Call` and `shift times` failed with `SND-CONTEXT-003` because the graph has no Core/On-Call, Coverage Pattern, or Shift Times concept. Suggestions were code paths.

Seed domain concepts from the repo's `CONTEXT.md` glossary, as a general mechanism. Do not hardcode these three names.

### 8. Suggestions

#938 sorts test-like keys and labels last. They still appear when nothing else shares the words. Prefer source files and product surfaces, and keep tests out of the suggested anchors when a non-test node shares the words.

## Reproduce

Instance when this was found: Sindri build 1.0.0, port 7410, map `v12@d436af7`, repo-docs at `59c5821` (573 docs, 6217 chunks), repo `vitalizecare/web-app`.

- `context_sources`
- `context_query` with `q="Core/On-Call unit behavior in staff app: self-scheduling, request time off, swaps, giveaways, open shifts pickup, mobile schedule"` and `budget=5000`
- `context_lenses` with `anchor=feature:hospitalshifttimes` (pass `limit` and `offset`; the whole answer is about 996,259 characters)
- `context_node` with `key="Core / On Call"` and `key="shift times"`

## Original findings (2026-10-24)

Found while using Sindri MCP `context_*` to list Core/On-Call and Shift Times product surfaces.

### Catch-all files tagged to many features

- `packages/ui-deprecated/src/components/ui/combobox.tsx` (all 9 `Combobox*` surfaces) is `part-of` 4 features: `root.package-16/21/22/28`.
- `apps/web/src/AppShell/components/*` (`ChatWidget`, `ChatNavButton`, `HospitalDropdown`, `UnitDropdown`, `SystemDropdown`, `AppBanners`) and `routes/_user-only/-components/dashboard-layout.tsx` are each in 5–6 features.
- `packages/icons/src/ArrowsDownUp/{Fill,Regular}.tsx` is in `feature:app`, `react-router`, `root.package-16`, and `staffeligibilityactionbar`.
- Shared admin shells (`AdminNestedNav`, `AdminTabSelect`, `AdminTabShell`, `AdminVerticalNav`) and `FractionDisplay` are owned by `feature:hospitalshifttimes`.
- The first code files under `feature:root.package-16` ("Self-scheduling staff schedule") are `apps/server/src/api/api-router.ts`, then `api-treaty.ts`, `health-check-router.ts`, `crons-router.ts`, and `deactivate-profile-cron.ts`.
- Test files count as implementers: `feature:hospitalshifttimes` is implemented-by `CentralStaffing/Demo/ActivitySidebar/__tests__/demo-open-shift-post.unit.test.ts`.

### Query noise

`context_query` with budget 5000 used 4997 and returned `truncated: true`. The budget went on about 60 generic surfaces and left no room for the specific records. The `more` arrays repeated `"C1"` hundreds of times.

`context_lenses` for `feature:hospitalshifttimes` returned 996,259 characters (scope 292, gaps 292, own 292, edges 1,387, neighbors 108) and spilled past the MCP output cap.

## Done when

- Re-running the four repro calls, after the owner rebuilds the map, returns Core/On-Call-specific surfaces first, without primitives, icons, routers, tests, or demo fixtures.
- `context_lenses` with a `limit` stays in one MCP result and sets `page.more` when another page exists. A call that omits `limit` is still the whole answer.
- Onyx shows `state: ok` with `records` greater than 0. That step is the owner's.
- The ignore list is configuration, applied at map build and at query time.
- Auto-clustered `root.package-NN` labels are not stored as human-confirmed until someone confirms them.
- Glossary concept nodes match `Core / On Call` and `shift times` without an exact key.

## Next steps

1. Read #938 and this file. Branch from `bugfix/sindri-context-quality-a3` at `68710ffbc` if that pull request is still open.
2. Fix sections 2, 3, 5, and 7 in code, plus the leftovers in sections 1, 4, and 8. Leave section 6 as commands for the owner.
3. Prove each fix with a failing test first. Keep the role globs and the paging contract.
4. Open a new pull request. Do not add the fixes to #938 and do not merge either pull request.
